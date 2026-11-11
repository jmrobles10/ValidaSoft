// Prueba de carga de GET /api/v1/consultas/certificacion (docs/04 §12, docs/11 H9): lo que hace Marco antes de pagar.
//
// Mezcla 200 y 304 como lo haría un ERP bien hecho: cada VU recuerda el ETag de cada tercero y lo manda en If-None-Match (304), salvo
// una parte de las consultas que llegan sin copia (200) y unas pocas de terceros que no existen (200 SinVinculo). Umbral: p99 < 100 ms
// a 500 rps durante un minuto, sin errores ni iteraciones descartadas. Un calentamiento de 10 s a 100 rps va antes y no cuenta.
//
//   Contra un entorno ya sembrado:   API_URL=… API_KEY=vsk_… EMPRESA_ID=… NITS=900000001,900000002 k6 run certificacion.k6.js
//   Sembrando uno de desarrollo:     API_URL=http://localhost:5080 PLATAFORMA_KEY=<clave de arranque> k6 run certificacion.k6.js
//   Con Docker (sin instalar k6):    ver tests/carga/README.md
//
// Variables: API_URL (por omisión http://localhost:5080), RPS (500), DURACION (60s), TERCEROS (150 al sembrar), P99_MS (100),
// PORCENTAJE_CON_ETAG (80), PORCENTAJE_DESCONOCIDOS (2).
import http from 'k6/http';
import exec from 'k6/execution';
import { check, fail } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const API_URL = (__ENV.API_URL || 'http://localhost:5080').replace(/\/+$/, '');
const RPS = Number(__ENV.RPS || 500);
const DURACION = __ENV.DURACION || '60s';
const P99_MS = Number(__ENV.P99_MS || 100);
const PORCENTAJE_CON_ETAG = Number(__ENV.PORCENTAJE_CON_ETAG || 80);
const PORCENTAJE_DESCONOCIDOS = Number(__ENV.PORCENTAJE_DESCONOCIDOS || 2);

const duracion200 = new Trend('consulta_200_ms', true);
const duracion304 = new Trend('consulta_304_ms', true);
const respuestas200 = new Counter('consulta_200');
const respuestas304 = new Counter('consulta_304');

export const options = {
  setupTimeout: '10m',
  scenarios: {
    // JIT, caché de la credencial y conexiones a la base: se calientan antes y no cuentan para los umbrales.
    calentamiento: {
      executor: 'constant-arrival-rate',
      rate: Math.min(100, RPS),
      timeUnit: '1s',
      duration: '10s',
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
    marco: {
      executor: 'constant-arrival-rate',
      rate: RPS,
      timeUnit: '1s',
      duration: DURACION,
      startTime: '12s',
      preAllocatedVUs: Math.ceil(RPS / 5),
      maxVUs: RPS,
    },
  },
  thresholds: {
    'http_req_duration{scenario:marco}': [`p(99)<${P99_MS}`],
    'consulta_200_ms{escenario:marco}': [`p(99)<${P99_MS}`],
    'consulta_304_ms{escenario:marco}': [`p(99)<${P99_MS}`],
    'http_req_failed{scenario:marco}': ['rate<0.001'],
    'http_reqs{scenario:marco}': ['count>0'],
    // La mezcla debe tener de las dos: si no hubo 304 (o 200), la prueba no mide lo que dice.
    'consulta_200{escenario:marco}': ['count>0'],
    'consulta_304{escenario:marco}': ['count>0'],
    'checks{escenario:marco}': ['rate>0.999'],
    'dropped_iterations{scenario:marco}': ['count==0'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

const CABECERAS_JSON = { 'Content-Type': 'application/json', Accept: 'application/json' };
const clave = () => `k6-${Math.random().toString(36).slice(2)}-${Date.now()}`;

function revisar(respuesta, metodo, ruta) {
  if (respuesta.status < 200 || respuesta.status >= 300) fail(`${metodo} ${ruta} → ${respuesta.status}: ${respuesta.body}`);
  return respuesta.body ? respuesta.json() : null;
}

function json(metodo, ruta, cuerpo, cabeceras) {
  const respuesta = http.request(metodo, `${API_URL}${ruta}`, JSON.stringify(cuerpo), {
    headers: { ...CABECERAS_JSON, 'Idempotency-Key': clave(), ...cabeceras },
    tags: { fase: 'siembra' },
  });
  return revisar(respuesta, metodo, ruta);
}

/** Diez peticiones a la vez: sembrar un entorno de desarrollo no debe tardar minutos. */
function lote(peticiones, cabeceras) {
  const respuestas = http.batch(
    peticiones.map(([metodo, ruta, cuerpo]) => [
      metodo,
      `${API_URL}${ruta}`,
      JSON.stringify(cuerpo),
      { headers: { ...CABECERAS_JSON, 'Idempotency-Key': clave(), ...cabeceras }, tags: { fase: 'siembra' } },
    ]),
  );
  return respuestas.map((respuesta, i) => revisar(respuesta, peticiones[i][0], peticiones[i][1]));
}

const nitAleatorio = (n) => `9${String(Math.floor(Math.random() * 1e6)).padStart(6, '0')}${String(n).padStart(2, '0')}`;

/** Siembra un grupo de desarrollo con N terceros certificados (flujo de EMPLEADO con un área automática). */
function sembrar() {
  const sufijo = Date.now().toString(36);
  const grupo = json(
    'POST',
    '/api/v1/plataforma/tenants',
    {
      slug: `carga-${sufijo}`,
      nombre: `Grupo de carga ${sufijo}`,
      region: 'co',
      zonaHoraria: 'America/Bogota',
      empresa: { identificacion: { pais: 'CO', tipo: 'NIT', numero: '800197268', digitoVerificacion: '4' }, nombre: 'Constructora Carga S.A.S.', prefijoFolio: 'CARGA', logoUrl: null, replyTo: null },
    },
    { 'X-Api-Key': __ENV.PLATAFORMA_KEY },
  );
  const admin = { 'X-Api-Key': grupo.apiKeyAdministrador.valor, 'X-Empresa': grupo.empresa.id };

  const area = json('POST', '/api/v1/areas', { codigo: 'AUTO', nombre: 'Validación automática' }, admin);
  const flujo = json(
    'POST',
    '/api/v1/flujos',
    {
      codigo: 'EMPLEADOS',
      nombre: 'Empleados',
      tipoVinculo: 'EMPLEADO',
      etapas: [{ orden: 1, areas: [{ areaId: area.id, aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: true, slaHoras: null }] }],
    },
    admin,
  );
  json('POST', `/api/v1/flujos/${flujo.id}/publicar`, {}, admin);

  const consulta = json('POST', '/api/v1/api-keys', { nombre: 'Marco (carga)', scopes: ['consultas:leer'], empresas: [grupo.empresa.id], expiraEn: null }, admin);

  const total = Number(__ENV.TERCEROS || 150);
  const nits = [];
  for (let desde = 0; desde < total; desde += 10) {
    const grupoDeNits = Array.from({ length: Math.min(10, total - desde) }, (_, k) => nitAleatorio(desde + k));
    const vinculos = lote(
      grupoDeNits.map((nit) => [
        'POST',
        '/api/v1/vinculos',
        {
          tercero: { identificacion: { pais: 'CO', tipo: 'NIT', numero: nit }, tipoPersona: 'Juridica', nombre: `Proveedor ${nit} S.A.S.`, contactos: [{ nombre: 'Ana', correo: `p${nit}@carga.validasoft.co`, rol: 'Comercial' }] },
          tipoVinculo: 'EMPLEADO',
          correoInvitacion: `p${nit}@carga.validasoft.co`,
          enviarInvitacion: false,
        },
      ]),
      admin,
    );
    lote(vinculos.map((v) => ['POST', `/api/v1/vinculos/${v.id}/expedientes`, {}]), admin);
    nits.push(...grupoDeNits);
  }
  return { llave: consulta.valor, empresa: grupo.empresa.id, nits };
}

/** El ETag actual de cada tercero: la «copia» que tendría Marco (compartida, como la de un ERP real, no una por VU). */
function etagsDe(entorno) {
  const etags = {};
  for (const nit of entorno.nits) {
    const r = http.get(`${API_URL}/api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=${nit}&tipoVinculo=EMPLEADO`, {
      headers: { 'X-Api-Key': entorno.llave, 'X-Empresa': entorno.empresa, Accept: 'application/json' },
      tags: { fase: 'siembra' },
    });
    if (r.status !== 200 || !r.headers.Etag) fail(`El tercero ${nit} no responde 200 con ETag (${r.status}): ¿está certificado?`);
    etags[nit] = r.headers.Etag;
  }
  return etags;
}

export function setup() {
  let entorno;
  if (__ENV.API_KEY && __ENV.EMPRESA_ID && __ENV.NITS) {
    entorno = { llave: __ENV.API_KEY, empresa: __ENV.EMPRESA_ID, nits: __ENV.NITS.split(',').map((n) => n.trim()).filter(Boolean) };
  } else {
    if (!__ENV.PLATAFORMA_KEY) fail('Indica API_KEY + EMPRESA_ID + NITS (entorno sembrado) o PLATAFORMA_KEY (para sembrar uno de desarrollo).');
    entorno = sembrar();
    // Solo desarrollo: para repetir la prueba sin volver a sembrar.
    console.log(`ENTORNO_SEMBRADO API_KEY=${entorno.llave} EMPRESA_ID=${entorno.empresa} NITS=${entorno.nits.join(',')}`);
  }
  return { ...entorno, etags: etagsDe(entorno) };
}

export default function (datos) {
  const etiqueta = { escenario: exec.scenario.name };
  const desconocido = Math.random() * 100 < PORCENTAJE_DESCONOCIDOS;
  const nit = desconocido ? '811222333' : datos.nits[Math.floor(Math.random() * datos.nits.length)];
  const cabeceras = { 'X-Api-Key': datos.llave, 'X-Empresa': datos.empresa, Accept: 'application/json' };
  // La mayoría de las consultas llevan la copia (304); el resto llega sin ella (200).
  if (!desconocido && Math.random() * 100 < PORCENTAJE_CON_ETAG) cabeceras['If-None-Match'] = datos.etags[nit];

  const respuesta = http.get(`${API_URL}/api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=${nit}&tipoVinculo=EMPLEADO`, {
    headers: cabeceras,
    tags: { fase: 'consulta' },
  });

  if (respuesta.status === 304) {
    duracion304.add(respuesta.timings.duration, etiqueta);
    respuestas304.add(1, etiqueta);
  } else {
    duracion200.add(respuesta.timings.duration, etiqueta);
    respuestas200.add(1, etiqueta);
  }

  check(
    respuesta,
    {
      'responde 200 o 304': (r) => r.status === 200 || r.status === 304,
      '200 trae ETag y Cache-Control privado': (r) => r.status !== 200 || (!!r.headers.Etag && /private/.test(r.headers['Cache-Control'] || '')),
      'el tercero sembrado está certificado': (r) => desconocido || r.status === 304 || r.json('certificado') === true,
    },
    etiqueta,
  );
}

export function handleSummary(datos) {
  const m = (nombre, estadistica) => datos.metrics[nombre]?.values?.[estadistica];
  const ms = (valor) => (valor === undefined ? '—' : `${valor.toFixed(1)} ms`);
  const linea = (nombre, etiqueta) =>
    `${etiqueta.padEnd(22)} p50 ${ms(m(nombre, 'med'))} · p95 ${ms(m(nombre, 'p(95)'))} · p99 ${ms(m(nombre, 'p(99)'))} · máx ${ms(m(nombre, 'max'))}`;
  const resumen = [
    `Consulta de certificación · ${RPS} rps durante ${DURACION} (tras 10 s de calentamiento)`,
    `peticiones medidas: ${m('http_reqs{scenario:marco}', 'count')} (${m('http_reqs{scenario:marco}', 'rate')?.toFixed(0)}/s) · 200: ${m('consulta_200{escenario:marco}', 'count') ?? 0} · 304: ${m('consulta_304{escenario:marco}', 'count') ?? 0} · errores: ${((m('http_req_failed{scenario:marco}', 'rate') ?? 0) * 100).toFixed(2)} %`,
    linea('http_req_duration{scenario:marco}', 'todas'),
    linea('consulta_200_ms{escenario:marco}', 'respuestas 200'),
    linea('consulta_304_ms{escenario:marco}', 'respuestas 304'),
    `objetivo: p99 < ${P99_MS} ms`,
    ...Object.entries(datos.metrics).flatMap(([nombre, metrica]) =>
      Object.entries(metrica.thresholds || {}).map(([umbral, estado]) => `${estado.ok ? '✓' : '✗'} ${nombre} ${umbral}`),
    ),
    '',
  ].join('\n');
  return { stdout: resumen, 'resultado-carga.json': JSON.stringify(datos, null, 2) };
}
