// Datos de demostración para el frontend contra la API local (solo desarrollo).
//
//   pnpm semilla:dev            (API en http://localhost:5080 con ASPNETCORE_ENVIRONMENT=Development)
//
// Crea un tenant nuevo con dos empresas, un sistema integrado "DEMO" con su par de claves RSA, una API key de ese
// sistema (con los scopes de flujos y expedientes), usuarios con distintos roles, áreas (Jurídica, Compras y
// Cumplimiento), tipos de vínculo y vínculos en varios estados. Los flujos se crean desde la UI. Escribe las
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

/** Aserción X-Usuario-Delegado (ADR-0011): JWT RS256 de 5 minutos firmado con la clave privada del sistema. */
function asercion(clavePrivadaPem, usuario) {
  const ahora = Math.floor(Date.now() / 1000);
  const cabecera = base64Url(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
  const carga = base64Url(
    JSON.stringify({ iss: SISTEMA, aud: 'validasoft', sub: usuario.sub, name: usuario.nombre, email: usuario.correo, iat: ahora, exp: ahora + 300 }),
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
const sistema = await llamar('POST', '/api/v1/sistemas', {
  llave: admin,
  cuerpo: { codigo: SISTEMA, nombre: 'Sistema integrado de demostración', clavePublicaPem: publicKey },
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
  'auditoria:leer',
  'flujos:leer',
  'flujos:escribir',
  'expedientes:leer',
  'expedientes:revisar',
  'expedientes:administrar',
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

for (const [codigo, nombre] of [
  ['PROVEEDOR', 'Proveedor'],
  ['CLIENTE', 'Cliente'],
]) {
  await llamar('POST', '/api/v1/tipos-vinculo', {
    llave: admin,
    cuerpo: { codigo, nombre, atributosPermitidos: [], exigeCuentaDesignada: false },
  }).catch((error) => {
    if (!String(error.message).includes('tipo-vinculo-duplicado')) throw error;
  });
}

for (const [codigo, nombre, miembros] of [
  ['juridica', 'Jurídica', [usuarios[0].id, usuarios[1].id]],
  ['compras', 'Compras', [usuarios[1].id]],
  ['cumplimiento', 'Cumplimiento', [usuarios[2].id]],
]) {
  const area = await llamar('POST', '/api/v1/areas', { llave: admin, empresa: norte.id, cuerpo: { codigo, nombre } });
  await llamar('PUT', `/api/v1/areas/${area.id}/miembros`, { llave: admin, empresa: norte.id, cuerpo: { usuarios: miembros } });
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
};
await writeFile(new URL('../.semilla.local.json', import.meta.url), JSON.stringify(salida, null, 2) + '\n', 'utf8');

console.log(`Listo: ${vinculos.length + 1} vínculos, ${usuarios.length} usuarios, sistema ${SISTEMA}.`);
console.log('Credenciales en apps/web/.semilla.local.json (solo desarrollo; no se versiona).');
