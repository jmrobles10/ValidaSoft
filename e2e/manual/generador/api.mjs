// Referencia de la API generada del contrato OpenAPI (packages/api-client/openapi/v1.json) y, para lo que el contrato no trae (descripción,
// permiso, empresa activa y códigos de error), del código de los endpoints. Sin LLM: todo sale de esos dos archivos.
import { escapar, aSlug, sinTildes } from './comun.mjs';
import { aplanar, ejemplo, nombreDeRef, normalizar, tipoLegible } from './api-esquema.mjs';
import { describirAcceso, normalizarRuta } from './api-fuente.mjs';
import { pagina } from './plantilla.mjs';

const PREFIJOS = ['/api/v1/', '/public/v1/'];
const METODOS = ['get', 'post', 'put', 'patch', 'delete'];

/** Palabras del contrato que llevan tilde o son siglas (los nombres de las clases vienen sin ellas). */
const PALABRAS = {
  vinculo: 'vínculo', vinculos: 'vínculos', configuracion: 'configuración', notificacion: 'notificación', notificaciones: 'notificaciones',
  verificacion: 'verificación', verificaciones: 'verificaciones', invitacion: 'invitación', invitaciones: 'invitaciones', sesion: 'sesión',
  sesiones: 'sesiones', declaracion: 'declaración', declaraciones: 'declaraciones', exportacion: 'exportación', importacion: 'importación',
  integracion: 'integración', auditoria: 'auditoría', campania: 'campaña', campanias: 'campañas', revision: 'revisión', delegacion: 'delegación',
  delegaciones: 'delegaciones', ratificacion: 'ratificación', revalidacion: 'revalidación', revalidaciones: 'revalidaciones', recalculo: 'recálculo',
  boveda: 'bóveda', origenes: 'orígenes', catalogo: 'catálogo', publico: 'público', publicos: 'públicos', publica: 'pública', publicas: 'públicas',
  ia: 'IA', sla: 'SLA', sso: 'SSO', pep: 'PEP', api: 'API', habeas: 'habeas', comun: 'común', cuenta: 'cuenta', informa: 'informa',
  clave: 'clave', arranque: 'arranque', interesado: 'interesado', autorregistro: 'autorregistro', ficha: 'ficha', tercero: 'tercero',
  mi: 'mi', seguridad: 'seguridad', gestion: 'gestión', ingreso: 'ingreso', embebido: 'embebido', embebidas: 'embebidas', actual: 'actual',
  catalogos: 'catálogos', propuestas: 'propuestas', direccion: 'dirección', politica: 'política', politicas: 'políticas', region: 'región',
  atencion: 'atención', operacion: 'operación', informacion: 'información', plantillas: 'plantillas', plazos: 'plazos', ayudas: 'ayudas',
};

/** `ContactosDeVinculo` → `Contactos de vínculo`. */
export function nombreLegible(identificador) {
  const palabras = identificador.replace(/([a-z0-9])([A-Z])/g, '$1 $2').split(' ');
  const texto = palabras.map((p) => PALABRAS[p.toLowerCase()] ?? p.toLowerCase()).join(' ');
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

function claseDeEtiqueta(etiqueta) {
  const partes = String(etiqueta ?? '').split('.');
  const clase = (partes.at(-1) ?? '').replace(/Endpoints$/, '');
  const modulo = partes.length >= 2 ? partes.at(-2) : 'General';
  return { modulo: modulo === 'Aplicacion' || modulo === 'Api' ? 'General' : modulo, clase: clase || 'General' };
}

const ESTADOS = {
  200: 'OK', 201: 'Creado', 202: 'Aceptado', 204: 'Sin contenido', 302: 'Redirección', 304: 'Sin cambios', 400: 'Solicitud inválida', 401: 'No autenticado',
  403: 'Sin permiso', 404: 'No encontrado', 409: 'Conflicto', 410: 'Ya no está disponible', 412: 'Versión desactualizada', 413: 'Archivo demasiado grande',
  415: 'Tipo de archivo no soportado', 422: 'No cumple una regla', 429: 'Demasiadas peticiones', 503: 'No disponible',
};

/**
 * Modelo de la referencia: módulos → recursos → operaciones. `bruto` es el contrato tal como lo sirve la API; `transformado` es el mismo
 * contrato con los nombres de operación y las respuestas reales que usa el cliente (puede ser el mismo si no se pudo transformar).
 */
export function construirModeloApi({ bruto, transformado, fuentes }) {
  const componentes = transformado.components?.schemas ?? bruto.components?.schemas ?? {};
  const modulos = new Map();
  let total = 0;

  for (const [ruta, operaciones] of Object.entries(bruto.paths)) {
    if (!PREFIJOS.some((p) => ruta.startsWith(p))) continue;
    for (const metodo of METODOS) {
      const crudo = operaciones[metodo];
      if (!crudo) continue;
      const op = transformado.paths?.[ruta]?.[metodo] ?? crudo;
      const fuente = fuentes.endpoints.get(`${metodo.toUpperCase()} ${normalizarRuta(ruta)}`) ?? null;
      const { modulo, clase } = claseDeEtiqueta(crudo.tags?.[0]);
      if (!modulos.has(modulo)) modulos.set(modulo, { slug: aSlug(modulo), nombre: nombreLegible(modulo), recursos: new Map() });
      const m = modulos.get(modulo);
      if (!m.recursos.has(clase)) {
        const claseCompleta = [...fuentes.clases.keys()].find((c) => c.endsWith(`.${modulo}.${clase}Endpoints`));
        m.recursos.set(clase, { id: aSlug(clase), nombre: nombreLegible(clase), descripcion: (claseCompleta && fuentes.clases.get(claseCompleta)) || null, endpoints: [] });
      }
      const acceso = describirAcceso(fuente?.acceso, fuentes.permisos);
      const parametros = (op.parameters ?? []).map((p) => {
        const resuelto = p.$ref ? (bruto.components?.parameters?.[nombreDeRef(p.$ref)] ?? p) : p;
        const normal = normalizar(resuelto.schema, componentes);
        return {
          nombre: resuelto.name,
          en: resuelto.in,
          requerido: Boolean(resuelto.required) || resuelto.in === 'path',
          tipo: tipoLegible(normal, componentes),
          detalle: [fuente?.parametros?.[resuelto.name], aplanar(resuelto.schema ?? {}, componentes)[0]?.detalle].filter(Boolean).join(' · '),
          esquema: resuelto.schema,
        };
      });
      const cuerpoCrudo = op.requestBody?.content ? Object.entries(op.requestBody.content)[0] : null;
      const cuerpo = cuerpoCrudo
        ? {
            tipoContenido: cuerpoCrudo[0],
            requerido: Boolean(op.requestBody.required),
            esquema: cuerpoCrudo[1].schema,
            campos: cuerpoCrudo[1].schema ? aplanar(cuerpoCrudo[1].schema, componentes) : [],
            nombre: cuerpoCrudo[1].schema ? normalizar(cuerpoCrudo[1].schema, componentes).nombre : null,
          }
        : null;
      const respuestas = Object.entries(op.responses ?? {})
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([codigo, r]) => {
          const contenido = r.content ? Object.entries(r.content)[0] : null;
          const normal = contenido?.[1]?.schema ? normalizar(contenido[1].schema, componentes) : null;
          const sinEsquemaUtil = !normal || normal.nombre === 'IResult';
          return {
            codigo,
            descripcion: ESTADOS[codigo] ?? r.description ?? '',
            tipoContenido: contenido?.[0] ?? null,
            nombre: normal?.nombre ?? null,
            tipo: normal && !sinEsquemaUtil ? tipoLegible(normal, componentes) : null,
            esquema: sinEsquemaUtil ? null : contenido[1].schema,
            campos: sinEsquemaUtil ? [] : aplanar(contenido[1].schema, componentes),
            sinEsquema: Boolean(contenido) && sinEsquemaUtil,
          };
        });
      const endpoint = {
        id: `${metodo}-${aSlug(ruta)}`,
        metodo: metodo.toUpperCase(),
        ruta,
        operacion: op.operationId && !/^(GET|POST|PUT|PATCH|DELETE)_/.test(op.operationId) ? op.operationId : null,
        titulo: op.operationId && !/^(GET|POST|PUT|PATCH|DELETE)_/.test(op.operationId) ? nombreLegible(op.operationId) : null,
        resumen: fuente?.resumen ?? null,
        acceso,
        alcanceEmpresa: Boolean(fuente?.alcanceEmpresa),
        publico: ruta.startsWith('/public/'),
        parametros,
        cuerpo,
        respuestas,
        errores: fuente?.errores ?? [],
      };
      endpoint.ejemplos = ejemplosDe(endpoint, componentes);
      m.recursos.get(clase).endpoints.push(endpoint);
      total++;
    }
  }

  const lista = [...modulos.values()]
    .map((m) => ({ ...m, recursos: [...m.recursos.values()].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')) }))
    .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  for (const m of lista) {
    for (const r of m.recursos) r.endpoints.sort((a, b) => a.ruta.localeCompare(b.ruta) || METODOS.indexOf(a.metodo.toLowerCase()) - METODOS.indexOf(b.metodo.toLowerCase()));
    m.total = m.recursos.reduce((n, r) => n + r.endpoints.length, 0);
    abreviarEsquemasRepetidos(m);
  }
  return { modulos: lista, total, componentes, titulo: bruto.info?.title ?? 'Validasoft API', version: bruto.info?.version ?? '' };
}

/**
 * Un tipo con nombre y muchos campos (ExpedienteDto) que ya se detalló antes en la misma página se reemplaza por una referencia a esa
 * operación: así cada página conserva el detalle completo una vez y no se repite en cada operación que lo devuelve.
 */
function abreviarEsquemasRepetidos(modulo) {
  const vistos = new Map();
  const revisar = (endpoint, parte, clave) => {
    if (!parte?.nombre || parte.campos.length < 15) return;
    const previo = vistos.get(clave + parte.nombre);
    if (previo) {
      parte.referencia = { nombre: parte.nombre, id: previo.id, metodo: previo.metodo, ruta: previo.ruta };
      parte.campos = [];
    } else vistos.set(clave + parte.nombre, endpoint);
  };
  for (const r of modulo.recursos) {
    for (const e of r.endpoints) {
      revisar(e, e.cuerpo, 'cuerpo:');
      for (const resp of e.respuestas) revisar(e, resp, 'respuesta:');
    }
  }
}

function ejemplosDe(endpoint, componentes) {
  const ruta = endpoint.ruta.replace(/\{(\w+)\}/g, (_, nombre) => {
    const p = endpoint.parametros.find((x) => x.nombre === nombre);
    return String(p ? ejemplo(p.esquema ?? { type: 'string' }, componentes, { clave: nombre }) : 'valor');
  });
  const consulta = endpoint.parametros
    .filter((p) => p.en === 'query' && p.requerido)
    .map((p) => `${p.nombre}=${encodeURIComponent(String(ejemplo(p.esquema ?? { type: 'string' }, componentes, { clave: p.nombre })))}`)
    .join('&');
  const cabeceras = [];
  const tipo = endpoint.acceso.etiqueta;
  if (endpoint.publico) {
    if (tipo !== 'Pública (sin sesión)') cabeceras.push('Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO');
  } else if (tipo === 'Cualquier sesión autenticada') cabeceras.push('Authorization: Bearer $TOKEN_DE_SESION');
  else if (tipo === 'Operador de Plataforma') cabeceras.push('X-Api-Key: $CLAVE_DE_PLATAFORMA');
  else cabeceras.push('X-Api-Key: $API_KEY');
  if (endpoint.alcanceEmpresa) cabeceras.push('X-Empresa: $EMPRESA_ID');
  if (endpoint.metodo === 'POST' && !endpoint.publico) cabeceras.push('Idempotency-Key: $(uuidgen)');
  const cuerpo = endpoint.cuerpo?.esquema ? ejemplo(endpoint.cuerpo.esquema, componentes) : null;
  if (cuerpo !== null) cabeceras.push(`Content-Type: ${endpoint.cuerpo.tipoContenido}`);
  const url = `https://{host}${ruta}${consulta ? `?${consulta}` : ''}`;
  const lineas = [`curl -X ${endpoint.metodo} "${url}"`, ...cabeceras.map((c) => `  -H "${c}"`)];
  if (cuerpo !== null) lineas.push(`  -d '${JSON.stringify(cuerpo)}'`);
  const exito = endpoint.respuestas.find((r) => /^2/.test(r.codigo) && r.esquema);
  return {
    curl: lineas.join(' \\\n'),
    peticion: cuerpo,
    respuesta: exito ? { codigo: exito.codigo, cuerpo: ejemplo(exito.esquema, componentes) } : null,
  };
}

// ---------------------------------------------------------------------------------------------------------------------------------
// Convenciones (lo que vale para todas las operaciones). Fuente: docs/05-api-contratos-y-webhooks.md §1 y §3.
// ---------------------------------------------------------------------------------------------------------------------------------

const PROBLEMA = `{
  "type": "https://docs.validasoft.co/errores/requisito-no-en-alcance",
  "title": "El requisito no está en el alcance del área",
  "status": 409,
  "detail": "El área 'financiera' no revisa 'CAMARA_COMERCIO' en el flujo 'habilitacion-proveedores' v3.",
  "instance": "/api/v1/expedientes/01J9…/requisitos/01J9…/veredicto",
  "codigo": "requisito-no-en-alcance",
  "traceId": "00-4bf9…-01",
  "errores": [ { "campo": "motivos", "mensaje": "Debe indicar al menos un motivo para observar." } ]
}`;

export const CONVENCIONES = [
  {
    id: 'base',
    titulo: 'Base, formato y versionado',
    items: [
      'Base: `https://{host}/api/v1` (autenticado) y `https://{host}/public/v1` (sesión de tercero o anónimo). El versionado va en la URL.',
      'Formato: JSON en `camelCase`; fechas ISO-8601 UTC con `Z`; fechas sin hora como `YYYY-MM-DD`; identificadores UUID.',
      'Rutas y campos en español sin tildes. Los enums son textuales en `PascalCase` (por ejemplo `"EnRevision"`).',
      'Campo ausente = no aplica; `null` explícito = sin valor.',
      'La especificación OpenAPI 3.1 se sirve en `/openapi/v1.json`; esta referencia sale de ese contrato.',
    ],
  },
  {
    id: 'autenticacion',
    titulo: 'Autenticación y alcance',
    items: [
      '`X-Api-Key: vsk_<entorno>_<tenant>_<aleatorio>` para integraciones, o `Authorization: Bearer <jwt>` para personas (sesiones embebidas y directas) y terceros. Nunca las dos.',
      'Con la API key de un sistema integrado, la cabecera `X-Usuario-Delegado: <jwt>` actúa en nombre de una persona: JWT RS256/ES256 firmado con la clave privada del sistema, `iss` = código del sistema, `aud` = `validasoft`, `exp` de 5 minutos como máximo, `sub`, `name`, `email` y `jti` obligatorio y de un solo uso. El permiso efectivo es la intersección de lo que permite la llave y los roles de la persona.',
      'El tenant va implícito en la credencial. La **empresa** (razón social del grupo) se indica con `X-Empresa: <empresaId>` en los recursos de empresa; si la credencial solo tiene alcance a una empresa, el encabezado es opcional. `X-Tenant: <slug>` lo usa solo Plataforma en `/plataforma/*`.',
      'Un recurso de otro tenant o de otra empresa responde `404 no-encontrado`: no se revela su existencia.',
    ],
  },
  {
    id: 'peticiones',
    titulo: 'Idempotencia, concurrencia y paginación',
    items: [
      '`Idempotency-Key` (cadena libre de hasta 128 caracteres) acompaña a los `POST` que crean o disparan efectos. Misma clave y mismo cuerpo devuelven la misma respuesta con `Idempotent-Replayed: true`; el mismo cuerpo distinto responde `422 clave-idempotencia-reutilizada`; si falta, `400 clave-idempotencia-requerida`. La memoria dura 24 horas.',
      'Las respuestas que traen un secreto (`valor` de una API key, `secreto` de un webhook, `token` de una sesión) lo entregan una sola vez: al repetir la petición con la misma clave se omite el secreto y se indica en `secretosOmitidos`.',
      'Los agregados con versión devuelven `ETag: "v{n}"`; `If-Match` en `PUT`, `PATCH` y veredictos protege contra cambios simultáneos: `412 version-desactualizada` o, en un veredicto sobre una versión superada, `409 version-superada`.',
      'Paginación por cursor opaco: `?limite=50&cursor=…` (máximo 200). Los filtros son parámetros planos; las listas van separadas por coma y los rangos usan `desde` y `hasta`.',
      'Los binarios nunca viajan por la API: se suben y descargan con URLs prefirmadas de corta duración. Límite de 20 MB; PDF, JPG, PNG o WebP.',
    ],
  },
  {
    id: 'errores',
    titulo: 'Errores (ProblemDetails)',
    items: [
      'Los errores siguen RFC 9457 (`application/problem+json`) con un `codigo` estable para programar contra él, `detail` en español para mostrar y, cuando aplica, `errores` por campo. `traceId` sirve para pedir soporte.',
      'Estados que usa la API: `400` solicitud inválida, `401` no autenticado o aserción inválida, `403` sin permiso, `404` no encontrado (también si es de otro tenant o empresa), `409` conflicto de estado, `410` enlace usado o vencido, `412` versión desactualizada, `413` archivo demasiado grande, `415` tipo de archivo no soportado, `422` regla de negocio o validación, `429` límite de peticiones, `503` servicio no disponible.',
      'Una operación que tuvo éxito nunca devuelve 4xx. Los avisos que no bloquean (por ejemplo, cupo de IA agotado) viajan en el cuerpo `2xx` como `advertencias: [{codigo, mensaje}]` y en la cabecera `Validasoft-Advertencias`.',
      'Cada operación lista debajo los códigos que lanza directamente su código; hay códigos comunes (por ejemplo `no-encontrado`, `sin-permiso`, `estado-invalido`) que pueden salir de reglas compartidas. El catálogo completo está en `docs/05-api-contratos-y-webhooks.md` §3.3.',
    ],
    bloque: PROBLEMA,
  },
  {
    id: 'tiempo-real',
    titulo: 'Eventos, webhooks y tiempo real',
    items: [
      'Las integraciones reciben los eventos públicos por webhooks firmados o en modo pull (`GET /api/v1/eventos`) y por SSE (`/api/v1/eventos/stream`). El catálogo de eventos y el formato de la firma están en `docs/05-api-contratos-y-webhooks.md` §6 y §7.',
      'Las pantallas se mantienen al día con streams SSE de avisos de cambio (`/api/v1/notificaciones/stream` y `/public/v1/notificaciones/stream`): solo avisan qué cambió, nunca datos.',
      'Límites: por credencial (`429`, cabeceras `RateLimit-*`) y, en `/public/v1`, por dirección IP. Cada respuesta lleva `X-Correlation-Id` para rastrear la petición.',
    ],
  },
];

// ---------------------------------------------------------------------------------------------------------------------------------
// HTML
// ---------------------------------------------------------------------------------------------------------------------------------

const inlineMd = (texto) => escapar(texto).replace(/`([^`]+)`/g, '<code>$1</code>').replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

const rutaConParametros = (ruta) => escapar(ruta).replace(/\{(\w+)\}/g, '<span class="param">{$1}</span>');

function tablaDeCampos(campos) {
  if (campos.length === 0) return '<p class="modulo-intro">Sin campos.</p>';
  const filas = campos
    .map((c) => {
      const sangria = c.nivel > 0 ? ` style="padding-left:${String(12 + c.nivel * 18)}px"` : '';
      const marca = c.requerido ? '<span class="req">requerido</span>' : '<span class="opc">opcional</span>';
      return `<tr><td class="campo"${sangria}><code>${escapar(c.ruta)}</code></td><td>${escapar(c.tipo)}${c.nulo ? ' <span class="opc">o null</span>' : ''}</td><td>${marca}</td><td>${inlineMd(c.detalle)}</td></tr>`;
    })
    .join('');
  return `<div class="tabla-envoltura"><table><thead><tr><th>Campo</th><th>Tipo</th><th>Requerido</th><th>Detalle</th></tr></thead><tbody>${filas}</tbody></table></div>`;
}

function tablaDeParametros(parametros) {
  const en = { path: 'ruta', query: 'consulta', header: 'cabecera', cookie: 'cookie' };
  const filas = parametros
    .map((p) => `<tr><td class="campo"><code>${escapar(p.nombre)}</code></td><td>${en[p.en] ?? p.en}</td><td>${escapar(p.tipo)}</td><td>${p.requerido ? '<span class="req">requerido</span>' : '<span class="opc">opcional</span>'}</td><td>${inlineMd(p.detalle)}</td></tr>`)
    .join('');
  return `<div class="tabla-envoltura"><table><thead><tr><th>Nombre</th><th>En</th><th>Tipo</th><th>Requerido</th><th>Detalle</th></tr></thead><tbody>${filas}</tbody></table></div>`;
}

const bloqueCodigo = (texto) => `<pre data-copiable><code>${escapar(texto)}</code></pre>`;

const htmlReferencia = (ref) =>
  `<p class="modulo-intro">Mismos campos de <code>${escapar(ref.nombre)}</code> que se detallan en <a href="#${escapar(ref.id)}"><code>${escapar(ref.metodo)} ${escapar(ref.ruta)}</code></a>.</p>`;

function htmlDeEndpoint(e) {
  const chips = [];
  if (e.acceso.etiqueta) chips.push(`<span class="etiqueta">${escapar(e.acceso.etiqueta)}</span>`);
  else chips.push('<span class="etiqueta neutra">Acceso: ver contrato</span>');
  if (e.alcanceEmpresa) chips.push('<span class="etiqueta neutra">Empresa activa (<code>X-Empresa</code>)</span>');
  chips.push(`<span class="etiqueta neutra">${e.publico ? 'Portal del tercero (/public/v1)' : 'API (/api/v1)'}</span>`);
  if (e.operacion) chips.push(`<span class="etiqueta neutra">SDK: <code>${escapar(e.operacion)}</code></span>`);
  const params = e.parametros.length ? `<h4>Parámetros</h4>${tablaDeParametros(e.parametros)}` : '';
  const cuerpo = e.cuerpo
    ? `<h4>Cuerpo de la petición${e.cuerpo.requerido ? '' : ' (opcional)'} <small>· ${escapar(e.cuerpo.tipoContenido)}${e.cuerpo.nombre ? ` · ${escapar(e.cuerpo.nombre)}` : ''}</small></h4>${e.cuerpo.referencia ? htmlReferencia(e.cuerpo.referencia) : tablaDeCampos(e.cuerpo.campos)}`
    : '';
  const respuestas = e.respuestas
    .map((r) => {
      const clase = /^2/.test(r.codigo) ? 'ok' : 'mal';
      const detalle = r.referencia
        ? htmlReferencia(r.referencia)
        : r.campos.length
        ? tablaDeCampos(r.campos)
        : r.sinEsquema
          ? '<p class="modulo-intro">El contrato publica esta respuesta sin esquema (<code>IResult</code>).</p>'
          : '';
      return `<div class="respuesta-titulo"><span class="estado ${clase}">${escapar(r.codigo)}</span><span>${escapar(r.descripcion)}${r.tipo ? ` · <small>${escapar(r.tipo)}${r.tipoContenido ? ` · ${escapar(r.tipoContenido)}` : ''}</small>` : ''}</span></div>${detalle}`;
    })
    .join('');
  const errores = e.errores.length
    ? `<h4>Errores de negocio que lanza</h4><div class="tabla-envoltura"><table><thead><tr><th>Estado</th><th>Código</th></tr></thead><tbody>${e.errores.map((x) => `<tr><td>${String(x.estado)}</td><td><code>${escapar(x.codigo)}</code></td></tr>`).join('')}</tbody></table></div>`
    : '';
  const ej = e.ejemplos;
  const ejemplos = `<details class="ejemplos"><summary>Ejemplo de petición y respuesta</summary><h5>Petición</h5>${bloqueCodigo(ej.curl)}${
    ej.respuesta ? `<h5>Respuesta ${escapar(ej.respuesta.codigo)}</h5>${bloqueCodigo(JSON.stringify(ej.respuesta.cuerpo, null, 2))}` : ''
  }</details>`;
  return `<article class="endpoint" id="${escapar(e.id)}"><header><span class="metodo ${e.metodo.toLowerCase()}">${e.metodo}</span><code class="ruta">${rutaConParametros(e.ruta)}</code><a class="ancla" href="#${escapar(e.id)}" aria-label="Enlace a esta operación">#</a></header>${
    e.titulo || e.resumen ? `<p class="endpoint-resumen">${e.titulo ? `<strong>${escapar(e.titulo)}.</strong> ` : ''}${e.resumen ? inlineMd(e.resumen) : ''}</p>` : ''
  }<div class="meta">${chips.join('')}</div>${params}${cuerpo}<h4>Respuestas</h4>${respuestas}${errores}${ejemplos}</article>`;
}

function lateralApi(modelo, actual) {
  const items = modelo.modulos
    .map((m) => `<li><a href="${m.slug}.html"${actual === m.slug ? ' aria-current="page"' : ''}>${escapar(m.nombre)}<span class="cuenta">${String(m.total)}</span></a></li>`)
    .join('');
  return `<h2>Referencia</h2><ul><li><a href="index.html"${actual === 'indice' ? ' aria-current="page"' : ''}>Introducción</a></li><li><a href="convenciones.html"${actual === 'convenciones' ? ' aria-current="page"' : ''}>Convenciones</a></li></ul><h2>Recursos</h2><ul>${items}</ul>`;
}

function paginaDeModulo(modelo, m, generado) {
  const unico = m.recursos.length === 1;
  const recursos = m.recursos
    .map(
      (r) =>
        `<section class="recurso" id="${escapar(r.id)}">${unico ? '' : `<h2>${escapar(r.nombre)}</h2>`}${r.descripcion ? `<p>${inlineMd(r.descripcion)}</p>` : ''}${r.endpoints.map(htmlDeEndpoint).join('')}</section>`,
    )
    .join('');
  const indice = `<ul class="indice-api">${m.recursos.map((r) => `<li><a href="#${escapar(r.id)}">${escapar(r.nombre)}</a> <small>(${String(r.endpoints.length)})</small></li>`).join('')}</ul>`;
  const contenido = `<h1>${escapar(m.nombre)}</h1><p class="modulo-intro">${String(m.total)} operaciones en ${String(m.recursos.length)} ${m.recursos.length === 1 ? 'recurso' : 'recursos'}. Los permisos y las descripciones salen del código de cada endpoint; los parámetros, cuerpos y respuestas, del contrato OpenAPI.</p>${unico ? '' : indice}${recursos}`;
  return pagina({
    titulo: `API · ${m.nombre}`,
    descripcion: `Referencia de la API de Validasoft: ${m.nombre}.`,
    raiz: '../',
    seccion: 'api',
    lateral: lateralApi(modelo, m.slug),
    migas: [{ texto: 'Manual', href: '../index.html' }, { texto: 'API', href: 'index.html' }, { texto: m.nombre }],
    contenido,
    generado,
  });
}

function paginaDeConvenciones(modelo, generado) {
  const secciones = CONVENCIONES.map(
    (c) => `<section class="convencion" id="${c.id}"><h2>${escapar(c.titulo)}</h2><ul>${c.items.map((i) => `<li>${inlineMd(i)}</li>`).join('')}</ul>${c.bloque ? `<pre class="bloque"><code>${escapar(c.bloque)}</code></pre>` : ''}</section>`,
  ).join('');
  return pagina({
    titulo: 'API · Convenciones',
    descripcion: 'Convenciones comunes de la API de Validasoft.',
    raiz: '../',
    seccion: 'api',
    lateral: lateralApi(modelo, 'convenciones'),
    migas: [{ texto: 'Manual', href: '../index.html' }, { texto: 'API', href: 'index.html' }, { texto: 'Convenciones' }],
    contenido: `<h1>Convenciones de la API</h1><p class="modulo-intro">Lo que vale para todas las operaciones: autenticación, empresa activa, idempotencia, errores y límites.</p>${secciones}`,
    generado,
  });
}

function paginaDeIndice(modelo, generado) {
  const tarjetas = modelo.modulos
    .map(
      (m) =>
        `<a class="tarjeta" href="${m.slug}.html"><h3>${escapar(m.nombre)}</h3><p>${m.recursos.slice(0, 5).map((r) => escapar(r.nombre)).join(' · ')}${m.recursos.length > 5 ? ' …' : ''}</p><span class="cuenta">${String(m.total)} ${m.total === 1 ? 'operación' : 'operaciones'}</span></a>`,
    )
    .join('');
  const contenido = `<div class="heroe"><h1>Referencia de la API</h1><p>Todas las operaciones del contrato (${String(modelo.total)}), agrupadas por recurso, con su permiso, parámetros, cuerpos, respuestas, errores de negocio y un ejemplo de petición y de respuesta construido del esquema.</p><p><a href="convenciones.html">Convenciones comunes →</a> autenticación, empresa activa, idempotencia y errores.</p></div><div class="rejilla">${tarjetas}</div>`;
  return pagina({ titulo: 'API', descripcion: 'Referencia de la API de Validasoft.', raiz: '../', seccion: 'api', lateral: lateralApi(modelo, 'indice'), migas: [{ texto: 'Manual', href: '../index.html' }, { texto: 'API' }], contenido, generado });
}

/** Páginas HTML de la API y sus entradas de búsqueda (rutas relativas al sitio). */
export function htmlDeApi(modelo, generado) {
  const paginas = [
    { ruta: 'api/index.html', html: paginaDeIndice(modelo, generado) },
    { ruta: 'api/convenciones.html', html: paginaDeConvenciones(modelo, generado) },
    ...modelo.modulos.map((m) => ({ ruta: `api/${m.slug}.html`, html: paginaDeModulo(modelo, m, generado) })),
  ];
  const indice = modelo.modulos.flatMap((m) =>
    m.recursos.flatMap((r) =>
      r.endpoints.map((e) => ({
        t: `${e.metodo} ${e.ruta}`,
        c: `API · ${m.nombre} · ${r.nombre}`,
        u: `api/${m.slug}.html#${e.id}`,
        x: [e.resumen ?? '', e.operacion ?? '', e.acceso.scope ?? '', e.acceso.etiqueta ?? '', ...e.errores.map((x) => x.codigo)].join(' '),
        k: 'api',
      })),
    ),
  );
  indice.push(...CONVENCIONES.map((c) => ({ t: `Convenciones: ${c.titulo}`, c: 'API · Convenciones', u: `api/convenciones.html#${c.id}`, x: c.items.join(' '), k: 'api' })));
  return { paginas, indice };
}

// ---------------------------------------------------------------------------------------------------------------------------------
// Markdown (docs/manual/api/)
// ---------------------------------------------------------------------------------------------------------------------------------

const celda = (texto) => String(texto ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ');

function tablaMd(encabezados, filas) {
  return [`| ${encabezados.join(' | ')} |`, `|${encabezados.map(() => '---').join('|')}|`, ...filas.map((f) => `| ${f.map(celda).join(' | ')} |`)].join('\n');
}

function camposMd(campos) {
  return tablaMd(
    ['Campo', 'Tipo', 'Requerido', 'Detalle'],
    campos.map((c) => [`\`${c.ruta}\``, `${c.tipo}${c.nulo ? ' o null' : ''}`, c.requerido ? 'sí' : 'no', c.detalle]),
  );
}

const referenciaMd = (ref) => `Mismos campos de \`${ref.nombre}\` que se detallan en \`${ref.metodo} ${ref.ruta}\`.`;

function endpointMd(e) {
  const l = [`### \`${e.metodo} ${e.ruta}\``, ''];
  if (e.titulo || e.resumen) l.push(`${e.titulo ? `**${e.titulo}.** ` : ''}${e.resumen ?? ''}`.trim(), '');
  const meta = [e.acceso.etiqueta ?? 'Acceso: ver contrato'];
  if (e.alcanceEmpresa) meta.push('Empresa activa (`X-Empresa`)');
  meta.push(e.publico ? 'Portal del tercero (`/public/v1`)' : 'API (`/api/v1`)');
  if (e.operacion) meta.push(`SDK: \`${e.operacion}\``);
  l.push(`**${meta.join(' · ')}**`, '');
  if (e.parametros.length > 0) {
    l.push('**Parámetros**', '', tablaMd(['Nombre', 'En', 'Tipo', 'Requerido', 'Detalle'], e.parametros.map((p) => [`\`${p.nombre}\``, p.en, p.tipo, p.requerido ? 'sí' : 'no', p.detalle])), '');
  }
  if (e.cuerpo) {
    l.push(`**Cuerpo de la petición${e.cuerpo.requerido ? '' : ' (opcional)'}** (\`${e.cuerpo.tipoContenido}\`${e.cuerpo.nombre ? `, ${e.cuerpo.nombre}` : ''})`, '', e.cuerpo.referencia ? referenciaMd(e.cuerpo.referencia) : camposMd(e.cuerpo.campos), '');
  }
  l.push('**Respuestas**', '');
  for (const r of e.respuestas) {
    l.push(`- \`${r.codigo}\` ${r.descripcion}${r.tipo ? ` — ${r.tipo}` : ''}${r.sinEsquema ? ' — el contrato publica esta respuesta sin esquema (`IResult`)' : ''}`);
  }
  l.push('');
  for (const r of e.respuestas.filter((x) => x.campos.length > 0 || x.referencia)) l.push(`Campos de la respuesta \`${r.codigo}\`:`, '', r.referencia ? referenciaMd(r.referencia) : camposMd(r.campos), '');
  if (e.errores.length > 0) l.push('**Errores de negocio que lanza**', '', ...e.errores.map((x) => `- \`${String(x.estado)}\` \`${x.codigo}\``), '');
  l.push('<details><summary>Ejemplo de petición y respuesta</summary>', '', '```bash', e.ejemplos.curl, '```', '');
  if (e.ejemplos.respuesta) l.push(`Respuesta \`${e.ejemplos.respuesta.codigo}\`:`, '', '```json', JSON.stringify(e.ejemplos.respuesta.cuerpo, null, 2), '```', '');
  l.push('</details>', '');
  return l.join('\n');
}

/** Archivos Markdown de la API: `{ ruta (relativa a docs/manual/api), texto }`. */
export function markdownDeApi(modelo) {
  const archivos = [];
  const indice = [
    '# Referencia de la API',
    '',
    `Todas las operaciones del contrato (${String(modelo.total)}) agrupadas por recurso. Generada de \`packages/api-client/openapi/v1.json\` y del código de los endpoints con \`pnpm manual\`; no se edita a mano.`,
    '',
    '- [Convenciones](convenciones.md): autenticación, empresa activa, idempotencia, errores y límites.',
    '',
    '## Recursos',
    '',
    ...modelo.modulos.map((m) => `- [${m.nombre}](${m.slug}.md) — ${m.recursos.map((r) => r.nombre).join(', ')} (${String(m.total)})`),
    '',
  ];
  archivos.push({ ruta: 'README.md', texto: indice.join('\n') });
  const conv = ['# Convenciones de la API', ''];
  for (const c of CONVENCIONES) {
    conv.push(`## ${c.titulo}`, '', ...c.items.map((i) => `- ${i}`), '');
    if (c.bloque) conv.push('```json', c.bloque, '```', '');
  }
  archivos.push({ ruta: 'convenciones.md', texto: conv.join('\n') });
  for (const m of modelo.modulos) {
    const l = [`# API · ${m.nombre}`, '', `${String(m.total)} operaciones en ${String(m.recursos.length)} ${m.recursos.length === 1 ? 'recurso' : 'recursos'}.`, ''];
    for (const r of m.recursos) {
      l.push(`## ${r.nombre}`, '');
      if (r.descripcion) l.push(r.descripcion, '');
      for (const e of r.endpoints) l.push(endpointMd(e));
    }
    archivos.push({ ruta: `${m.slug}.md`, texto: `${l.join('\n').replace(/\n{3,}/g, '\n\n').trimEnd()}\n` });
  }
  return archivos;
}

export { sinTildes };
