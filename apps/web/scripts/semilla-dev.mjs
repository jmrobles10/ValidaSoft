// Datos de demostración para el frontend contra la API local (solo desarrollo).
//
//   pnpm semilla:dev            (API en http://localhost:5080 con ASPNETCORE_ENVIRONMENT=Development)
//
// Crea un tenant nuevo con dos empresas, un sistema integrado "DEMO" con su par de claves RSA, una API key de ese
// sistema (con los scopes de flujos, expedientes y documentos), usuarios con distintos roles, áreas (Jurídica, Compras
// y Cumplimiento), tipos de vínculo (PROVEEDOR exige OTP en el enlace), el flujo «Habilitación de proveedores» publicado
// en Norte con sus requisitos (RUT lo revisan Compras y Cumplimiento; Cámara de comercio, solo Cumplimiento), los textos
// legales de Norte y vínculos en varios estados. También registra el sistema de Plataforma CONSOLA_DEV (su clave se rota en
// cada ejecución) y un operador para entrar a la consola de Plataforma (/plataforma) desde el formulario de sesión. Las invitaciones llegan a Mailpit (http://localhost:8025) con el enlace del tercero. Escribe las
// credenciales en apps/web/.semilla.local.json (ignorado por git), que Vite sirve solo en `vite dev` para prellenar
// el formulario de sesión y embed-demo.html. Cada ejecución crea un tenant distinto: no reutiliza ni borra nada.
import { createSign, generateKeyPairSync, randomUUID } from 'node:crypto';
import { writeFile } from 'node:fs/promises';

const API = process.env.VALIDASOFT_API_URL ?? 'http://localhost:5080';
const CLAVE_PLATAFORMA = process.env.VALIDASOFT_CLAVE_PLATAFORMA ?? 'dev-plataforma-solo-local-cambiar-0001';
const SISTEMA = 'DEMO';

/** Dígito de verificación del NIT (DIAN). */
function dvNit(numero) {
  const pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];
  const digitos = [...numero].reverse();
  const suma = digitos.reduce((total, d, i) => total + Number(d) * pesos[i], 0);
  const resto = suma % 11;
  return String(resto > 1 ? 11 - resto : resto);
}

function nit(numero) {
  return { pais: 'CO', tipo: 'NIT', numero, digitoVerificacion: dvNit(numero) };
}

async function llamar(metodo, ruta, { llave, empresa, cuerpo, delegado, ifMatch } = {}) {
  const cabeceras = { Accept: 'application/json', 'X-Api-Key': llave };
  if (cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json';
  if (empresa) cabeceras['X-Empresa'] = empresa;
  if (delegado) cabeceras['X-Usuario-Delegado'] = delegado;
  if (ifMatch !== undefined) cabeceras['If-Match'] = `"${ifMatch}"`;
  if (metodo === 'POST') cabeceras['Idempotency-Key'] = randomUUID();

  const respuesta = await fetch(`${API}${ruta}`, { method: metodo, headers: cabeceras, body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo) });
  const texto = await respuesta.text();
  const datos = texto ? JSON.parse(texto) : undefined;
  if (!respuesta.ok) {
    throw new Error(`${metodo} ${ruta} → ${respuesta.status} ${datos?.codigo ?? ''}: ${datos?.detail ?? texto}`);
  }
  return datos;
}

function base64Url(datos) {
  return Buffer.from(datos).toString('base64url');
}

/**
 * Aserción X-Usuario-Delegado (ADR-0011): JWT RS256 de 5 minutos firmado con la clave privada del sistema, con `jti`
 * aleatorio: cada aserción vale una sola vez (SE-4), así que se firma una por petición.
 */
function asercion(clavePrivadaPem, usuario, emisor = SISTEMA) {
  const ahora = Math.floor(Date.now() / 1000);
  const cabecera = base64Url(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
  const carga = base64Url(
    JSON.stringify({
      iss: emisor,
      aud: 'validasoft',
      sub: usuario.sub,
      name: usuario.nombre,
      email: usuario.correo,
      jti: randomUUID(),
      iat: ahora,
      exp: ahora + 300,
    }),
  );
  const firma = createSign('RSA-SHA256').update(`${cabecera}.${carga}`).sign(clavePrivadaPem);
  return `${cabecera}.${carga}.${base64Url(firma)}`;
}

const sufijo = Date.now().toString(36);
const slug = `demo-front-${sufijo}`;
console.log(`Creando tenant ${slug} en ${API}…`);

const creado = await llamar('POST', '/api/v1/plataforma/tenants', {
  llave: CLAVE_PLATAFORMA,
  cuerpo: { slug, nombre: 'Grupo Constructor Demo', region: 'co', zonaHoraria: 'America/Bogota' },
});
const admin = creado.apiKeyAdministrador.valor;

const norte = await llamar('POST', '/api/v1/empresas', {
  llave: admin,
  cuerpo: { identificacion: nit('800197268'), nombre: 'Constructora Norte S.A.S.', prefijoFolio: 'NORTE', logoUrl: null, replyTo: null },
});
const sur = await llamar('POST', '/api/v1/empresas', {
  llave: admin,
  cuerpo: { identificacion: nit('899999068'), nombre: 'Constructora Sur S.A.S.', prefijoFolio: 'SUR', logoUrl: null, replyTo: null },
});

const { privateKey, publicKey } = generateKeyPairSync('rsa', {
  modulusLength: 2048,
  publicKeyEncoding: { type: 'spki', format: 'pem' },
  privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
});
// Orígenes desde los que el sistema aloja las pantallas embebidas (SE-1): en desarrollo, la app web de Vite. Entran en
// frame-ancestors y en GET /public/v1/embed/origenes-permitidos, que el puente embebido lee al arrancar.
const ORIGENES_DEMO = (process.env.VALIDASOFT_ORIGENES_DEMO ?? 'http://localhost:5173')
  .split(',')
  .map((o) => o.trim())
  .filter(Boolean);
const sistema = await llamar('POST', '/api/v1/sistemas', {
  llave: admin,
  cuerpo: { codigo: SISTEMA, nombre: 'Sistema integrado de demostración', clavePublicaPem: publicKey, origenesPermitidos: ORIGENES_DEMO },
});

const scopes = [
  'empresas:administrar',
  'api-keys:administrar',
  'sistemas:administrar',
  'usuarios:administrar',
  'configuracion:leer',
  'configuracion:escribir',
  'terceros:leer',
  'terceros:escribir',
  'vinculos:leer',
  'vinculos:escribir',
  'vinculos:bloquear',
  'cuentas:aprobar',
  'auditoria:leer',
  'flujos:leer',
  'flujos:escribir',
  'expedientes:leer',
  'expedientes:revisar',
  'expedientes:administrar',
  'documentos:leer',
  'documentos:descargar',
  'certificados:leer',
  'eventos:leer',
  'webhooks:administrar',
];
const llaveSistema = await llamar('POST', '/api/v1/api-keys', {
  llave: admin,
  cuerpo: { nombre: 'Demo embebido', scopes, empresas: null, expiraEn: null, sandbox: false, sistemaId: sistema.id },
});

const usuarios = [
  { sub: 'u-admin', nombre: 'Ana Administradora', correo: `ana.${sufijo}@demo.co`, roles: [{ rol: 'Administrador', empresaId: null }], descripcion: 'Administradora de todo el tenant' },
  { sub: 'u-revisor', nombre: 'Rubén Revisor', correo: `ruben.${sufijo}@demo.co`, roles: [{ rol: 'Revisor', empresaId: norte.id }], descripcion: 'Revisor en Constructora Norte' },
  {
    sub: 'u-oficial',
    nombre: 'Olga Oficial',
    correo: `olga.${sufijo}@demo.co`,
    roles: [
      { rol: 'OficialCumplimiento', empresaId: norte.id },
      { rol: 'OficialCumplimiento', empresaId: sur.id },
    ],
    descripcion: 'Oficial de cumplimiento en Norte y Sur (puede bloquear)',
  },
  { sub: 'u-auditor', nombre: 'Aurelio Auditor', correo: `aurelio.${sufijo}@demo.co`, roles: [{ rol: 'Auditor', empresaId: null }], descripcion: 'Auditor (solo lectura)' },
];
for (const u of usuarios) {
  const usuario = await llamar('POST', '/api/v1/usuarios', {
    llave: admin,
    cuerpo: { nombre: u.nombre, correo: u.correo, identificacion: null, roles: u.roles, identidades: [{ sistemaId: sistema.id, idExterno: u.sub }] },
  });
  u.id = usuario.id;
}

for (const [codigo, nombre, exigeOtpEnEnlace] of [
  ['PROVEEDOR', 'Proveedor', true],
  ['CLIENTE', 'Cliente', false],
]) {
  await llamar('POST', '/api/v1/tipos-vinculo', {
    llave: admin,
    cuerpo: { codigo, nombre, atributosPermitidos: [], exigeCuentaDesignada: false, exigeOtpEnEnlace },
  }).catch((error) => {
    if (!String(error.message).includes('tipo-vinculo-duplicado')) throw error;
  });
}

const areas = {};
for (const [codigo, nombre, miembros] of [
  ['juridica', 'Jurídica', [usuarios[0].id, usuarios[1].id]],
  ['compras', 'Compras', [usuarios[1].id]],
  ['cumplimiento', 'Cumplimiento', [usuarios[2].id]],
]) {
  const area = await llamar('POST', '/api/v1/areas', { llave: admin, empresa: norte.id, cuerpo: { codigo, nombre } });
  await llamar('PUT', `/api/v1/areas/${area.id}/miembros`, { llave: admin, empresa: norte.id, cuerpo: { usuarios: miembros } });
  areas[codigo] = area;
}

// Flujo por defecto de los proveedores de Norte: Compras y Cumplimiento revisan en paralelo; Cumplimiento puede rechazar.
// Sus requisitos deciden qué documentos recibe Norte (RQ-3): RUT (Compras y Cumplimiento) y Cámara de comercio (solo
// Cumplimiento). Cuando el tercero acepta la invitación se abre su expediente: `Abierto` mientras falte algún documento.
const flujo = await llamar('POST', '/api/v1/flujos', {
  llave: admin,
  empresa: norte.id,
  cuerpo: {
    codigo: 'HABILITACION',
    nombre: 'Habilitación de proveedores',
    tipoVinculo: 'PROVEEDOR',
    etapas: [
      {
        orden: 1,
        areas: [
          { areaId: areas.compras.id, aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: false, slaHoras: null },
          { areaId: areas.cumplimiento.id, aprobadoresRequeridos: 1, puedeRechazarExpediente: true, automatica: false, slaHoras: null },
        ],
      },
    ],
    politicaDevolucion: 'Inmediata',
    separacionDeFunciones: false,
    requisitos: [
      { tipoDocumento: 'RUT', obligatorio: true, areasQueRevisan: [areas.compras.id, areas.cumplimiento.id], aprobadoresRequeridos: null },
      { tipoDocumento: 'CAMARA_COMERCIO', obligatorio: true, areasQueRevisan: [areas.cumplimiento.id], aprobadoresRequeridos: null },
    ],
  },
});
await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: admin, empresa: norte.id });

// Textos legales de Norte (el aviso de la plataforma lo siembra la API al arrancar). Sin ellos, el tercero no puede aceptar.
for (const [tipo, contenido] of [
  [
    'autorizacion',
    'Autorizo a Constructora Norte S.A.S. a tratar mis datos personales y los documentos que comparta para evaluar y ' +
      'administrar la relación comercial, conforme a la Ley 1581 de 2012 y a su política de tratamiento de datos. ' +
      'Puedo conocer, actualizar, rectificar y suprimir mis datos escribiendo a datos@constructoranorte.co. (Texto de demostración.)',
  ],
  [
    'terminos',
    'Términos y condiciones para proveedores de Constructora Norte S.A.S.: la información que entrego es veraz y la ' +
      'mantendré actualizada; acepto que Constructora Norte verifique mis documentos y consulte listas restrictivas. ' +
      '(Texto de demostración.)',
  ],
]) {
  await llamar('PUT', `/api/v1/empresas/${norte.id}/textos-legales/${tipo}`, { llave: admin, cuerpo: { contenido } });
}

const terceros = [
  ['900123456', 'Aceros del Norte S.A.S.'],
  ['900234567', 'Concretos Andinos S.A.'],
  ['900345678', 'Ferretería La Esquina S.A.S.'],
  ['900456789', 'Transportes Rápidos Ltda.'],
  ['900567890', 'Eléctricos del Caribe S.A.S.'],
  ['900678901', 'Maderas y Formaletas S.A.S.'],
  ['900789012', 'Seguridad Integral Ltda.'],
];
const vinculos = [];
for (const [indice, [numero, nombre]] of terceros.entries()) {
  const correo = `contacto${indice + 1}.${sufijo}@proveedor.co`;
  const vinculo = await llamar('POST', '/api/v1/vinculos', {
    llave: llaveSistema.valor,
    empresa: norte.id,
    cuerpo: {
      tercero: {
        identificacion: nit(numero),
        tipoPersona: 'Juridica',
        nombre,
        nombreComercial: null,
        contactos: [{ nombre: 'Contacto comercial', correo, telefono: null, rol: 'Comercial' }],
      },
      tipoVinculo: indice === 5 ? 'CLIENTE' : 'PROVEEDOR',
      atributos: {},
      correoInvitacion: correo,
      referenciaExterna: { sistema: SISTEMA, id: `PRV-${String(indice + 1).padStart(3, '0')}` },
      enviarInvitacion: indice !== 6,
    },
  });
  vinculos.push(vinculo);
}

const ana = usuarios[0];
const delegadoAna = () => asercion(privateKey, ana);
const accion = (vinculo, ruta, cuerpo) =>
  llamar('POST', `/api/v1/vinculos/${vinculo.id}/${ruta}`, { llave: llaveSistema.valor, empresa: norte.id, cuerpo, delegado: delegadoAna(), ifMatch: vinculo.version });

vinculos[1] = await accion(vinculos[1], 'bloquear', { motivo: 'Coincidencia en lista restrictiva pendiente de revisar.', hasta: null });
vinculos[2] = await accion(vinculos[2], 'desvincular', { motivo: 'Terminó el contrato marco.' });
vinculos[3] = await accion(vinculos[3], 'anular', { motivo: 'Se creó por error.' });
vinculos[4] = await accion(vinculos[4], 'bloquear', { motivo: 'Documentos vencidos.', hasta: null });
vinculos[4] = await accion(vinculos[4], 'desbloquear', { justificacion: 'Allegó los documentos al día.' });

// Un vínculo en la empresa Sur, para ver el aislamiento entre empresas.
await llamar('POST', '/api/v1/vinculos', {
  llave: llaveSistema.valor,
  empresa: sur.id,
  cuerpo: {
    terceroId: vinculos[0].terceroId,
    tipoVinculo: 'PROVEEDOR',
    atributos: {},
    correoInvitacion: `sur.${sufijo}@proveedor.co`,
    referenciaExterna: { sistema: SISTEMA, id: 'SUR-001' },
    enviarInvitacion: true,
  },
});

// Un proveedor con su expediente aprobado y su certificado (Aceros del Norte en Norte), por el mismo camino que en la
// realidad: el tercero canjea el enlace de la invitación con el código que le llega a Mailpit, sube RUT y Cámara, acepta,
// y Rubén (Compras) y Olga (Cumplimiento) aprueban sus áreas. Así se ven la lista de certificados, el Inicio con
// indicadores, Vencimientos y la página pública de verificación.
const MAILPIT = process.env.VALIDASOFT_MAILPIT_URL ?? 'http://localhost:8025';

async function esperarCorreo(para, asunto) {
  for (let intento = 0; intento < 60; intento++) {
    const respuesta = await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:"${para}"`)}&limit=50`);
    if (respuesta.ok) {
      const { messages } = await respuesta.json();
      const ultimo = messages.filter((m) => asunto.test(m.Subject)).sort((a, b) => b.Created.localeCompare(a.Created))[0];
      if (ultimo) return (await (await fetch(`${MAILPIT}/api/v1/message/${ultimo.ID}`)).json()).Text;
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`No llegó a ${para} un correo con asunto ${String(asunto)} (¿está Mailpit en ${MAILPIT}?).`);
}

async function publico(metodo, ruta, { token, cuerpo } = {}) {
  const cabeceras = { Accept: 'application/json' };
  if (token) cabeceras.Authorization = `Bearer ${token}`;
  if (cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json';
  const respuesta = await fetch(`${API}${ruta}`, { method: metodo, headers: cabeceras, body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo) });
  const texto = await respuesta.text();
  if (!respuesta.ok) throw new Error(`${metodo} ${ruta} → ${respuesta.status}: ${texto}`);
  return { status: respuesta.status, datos: texto ? JSON.parse(texto) : null };
}

function pdf(texto) {
  return Buffer.from(
    `%PDF-1.4\n% ${texto} ${randomUUID()}\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n` +
      `3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n`,
    'latin1',
  );
}

const aprobado = vinculos[0];
const correoAprobado = `contacto1.${sufijo}@proveedor.co`;
const enlace = /\/e\/([A-Za-z0-9_-]{43})/.exec(await esperarCorreo(correoAprobado, /./))?.[1];
if (!enlace) throw new Error('La invitación de Aceros del Norte no trae el enlace /e/{token}.');
let canje = await publico('POST', `/public/v1/enlaces/${enlace}/canjear`);
if (canje.status === 202) {
  const codigo = /\b(\d{6})\b/.exec(await esperarCorreo(correoAprobado, /código/i))?.[1];
  canje = await publico('POST', `/public/v1/enlaces/${enlace}/otp`, { cuerpo: { codigo } });
}
const sesionTercero = canje.datos.accessToken;

for (const [canonico, texto] of [
  ['RUT', 'RUT Aceros del Norte'],
  ['CAMARA_COMERCIO', 'Cámara de comercio Aceros del Norte'],
]) {
  const contenido = pdf(texto);
  const carga = (
    await publico('POST', '/public/v1/boveda/documentos', {
      token: sesionTercero,
      cuerpo: { nombreArchivo: `${canonico.toLowerCase()}.pdf`, tamanoBytes: contenido.length, tipoMime: 'application/pdf', tipoCanonico: canonico },
    })
  ).datos;
  const put = await fetch(carga.carga.url, { method: 'PUT', headers: carga.carga.cabeceras, body: contenido });
  if (!put.ok) throw new Error(`No se pudo subir ${canonico} a MinIO: ${put.status}`);
  await publico('POST', `/public/v1/boveda/documentos/${carga.documentoId}/versiones/${carga.version}/confirmar`, { token: sesionTercero });
}

const invitacionPublica = (await publico('GET', `/public/v1/invitaciones/${aprobado.id}`, { token: sesionTercero })).datos;
await publico('POST', `/public/v1/invitaciones/${aprobado.id}/aceptar`, {
  token: sesionTercero,
  cuerpo: { consentimientos: invitacionPublica.textosPorAceptar.map((t) => ({ tipo: t.tipo, textoVersion: t.textoVersion, aceptado: true })) },
});

// El expediente se abre al aceptar; los documentos le llegan por la cola de la bóveda.
let expediente = null;
for (let intento = 0; intento < 60 && expediente?.estado !== 'EnRevision'; intento++) {
  const actual = await llamar('GET', `/api/v1/vinculos/${aprobado.id}`, { llave: admin, empresa: norte.id });
  if (actual.expedienteAbiertoId)
    expediente = await llamar('GET', `/api/v1/expedientes/${actual.expedienteAbiertoId}`, { llave: admin, empresa: norte.id });
  if (expediente?.estado !== 'EnRevision') await new Promise((r) => setTimeout(r, 500));
}
if (expediente?.estado !== 'EnRevision') throw new Error(`El expediente de Aceros del Norte no pasó a revisión (está ${expediente?.estado ?? 'sin abrir'}).`);

for (const [persona, codigoArea] of [
  [usuarios[1], 'compras'],
  [usuarios[2], 'cumplimiento'],
]) {
  const visto = await llamar('GET', `/api/v1/expedientes/${expediente.id}`, { llave: llaveSistema.valor, empresa: norte.id, delegado: asercion(privateKey, persona) });
  const area = visto.etapas.flatMap((e) => e.areas).find((a) => a.codigo.toLowerCase() === codigoArea);
  const versiones = Object.fromEntries((area.documentosPorDecidir ?? []).map((d) => [d.requisitoId, d.versionVigente]));
  await llamar('POST', `/api/v1/expedientes/${expediente.id}/areas/${area.areaId}/veredicto`, {
    llave: llaveSistema.valor,
    empresa: norte.id,
    delegado: asercion(privateKey, persona),
    cuerpo: { veredicto: 'Aprobado', versiones },
  });
}
const certificado = await llamar('GET', `/api/v1/vinculos/${aprobado.id}/certificado`, { llave: admin, empresa: norte.id });
console.log(`Aceros del Norte quedó aprobado en Norte con el certificado ${certificado.actual?.folio ?? '(sin emitir)'}.`);

// Consola de Plataforma (PT-4): el backoffice de desarrollo que firma las aserciones de sus operadores. Se reemplaza su clave
// en cada ejecución (la del archivo anterior deja de servir) y aloja la consola desde la app de Vite.
const SISTEMA_PLATAFORMA = 'CONSOLA_DEV';
const clavesPlataforma = generateKeyPairSync('rsa', {
  modulusLength: 2048,
  publicKeyEncoding: { type: 'spki', format: 'pem' },
  privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
});
await llamar('PUT', `/api/v1/plataforma/sistemas/${SISTEMA_PLATAFORMA}`, {
  llave: CLAVE_PLATAFORMA,
  cuerpo: { nombre: 'Backoffice de Plataforma (desarrollo)', clavePublicaPem: clavesPlataforma.publicKey, origenesPermitidos: ORIGENES_DEMO },
});
const operador = { sub: 'op-paula', nombre: 'Paula Plataforma', correo: 'paula@validasoft.local' };
// Verifica que el operador entra (y lo deja registrado).
await llamar('POST', '/api/v1/plataforma/sesiones', { llave: CLAVE_PLATAFORMA, delegado: asercion(clavesPlataforma.privateKey, operador, SISTEMA_PLATAFORMA) });

const salida = {
  generadoEn: new Date().toISOString(),
  apiUrl: API,
  tenant: { id: creado.tenant.id, slug },
  sistema: { id: sistema.id, codigo: SISTEMA },
  apiKey: llaveSistema.valor,
  clavePrivadaPem: privateKey,
  empresas: [
    { id: norte.id, nombre: norte.nombre },
    { id: sur.id, nombre: sur.nombre },
  ],
  usuarios: usuarios.map(({ sub, nombre, correo, descripcion }) => ({ sub, nombre, correo, descripcion })),
  plataforma: { clavePlataforma: CLAVE_PLATAFORMA, sistema: SISTEMA_PLATAFORMA, clavePrivadaPem: clavesPlataforma.privateKey, persona: operador },
};
await writeFile(new URL('../.semilla.local.json', import.meta.url), JSON.stringify(salida, null, 2) + '\n', 'utf8');

console.log(`Listo: ${vinculos.length + 1} vínculos, ${usuarios.length} usuarios, sistema ${SISTEMA} y operador de Plataforma ${operador.nombre}.`);
console.log('Credenciales en apps/web/.semilla.local.json (solo desarrollo; no se versiona).');
console.log('Invitaciones (enlace del tercero y código OTP) en Mailpit: http://localhost:8025, o en apps/public: /dev/correos.');
