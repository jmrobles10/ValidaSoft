// Backend mínimo del sistema anfitrión ficticio (IO-5, docs/08 §2.2). Sirve index.html y expone POST /sesion: canjea con la API key del
// sistema (que solo vive aquí, en el servidor) una sesión del tercero en Validasoft y devuelve a la página únicamente el resultado.
//
//   VALIDASOFT_URL=http://localhost:5080 VALIDASOFT_API_KEY=vsk_… VALIDASOFT_EMPRESA=<id> VALIDASOFT_VINCULO=<id> \
//   ORIGEN_ANFITRION=http://localhost:5199 PUERTO=5199 node servidor.mjs
//
// Requisitos en Validasoft: un sistema integrador cuyo `origenesPermitidos` incluya ORIGEN_ANFITRION, una API key suya con el permiso
// `sesiones-tercero:crear` y un vínculo (de la empresa) con la invitación aceptada que lleve la referencia de ese sistema.
import { readFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const { VALIDASOFT_URL, VALIDASOFT_API_KEY, VALIDASOFT_EMPRESA, VALIDASOFT_VINCULO, ORIGEN_ANFITRION = 'http://localhost:5199', PUERTO = '5199' } = process.env;
if (!VALIDASOFT_URL || !VALIDASOFT_API_KEY || !VALIDASOFT_EMPRESA || !VALIDASOFT_VINCULO) {
  console.error('Faltan VALIDASOFT_URL, VALIDASOFT_API_KEY, VALIDASOFT_EMPRESA o VALIDASOFT_VINCULO.');
  process.exit(1);
}

const pagina = await readFile(join(dirname(fileURLToPath(import.meta.url)), 'index.html'), 'utf8');

/** POST /api/v1/sesiones-tercero con la API key del sistema; el vínculo lo decide el sistema, no la página. */
async function abrirSesion(componente) {
  const respuesta = await fetch(`${VALIDASOFT_URL}/api/v1/sesiones-tercero`, {
    method: 'POST',
    headers: { 'X-Api-Key': VALIDASOFT_API_KEY, 'X-Empresa': VALIDASOFT_EMPRESA, 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify({ vinculoId: VALIDASOFT_VINCULO, componente, origen: ORIGEN_ANFITRION, minutos: 10 }),
  });
  if (!respuesta.ok) throw new Error(`Validasoft respondió ${respuesta.status}: ${await respuesta.text()}`);
  return respuesta.json();
}

createServer(async (req, res) => {
  try {
    if (req.method === 'POST' && req.url === '/sesion') {
      let cuerpo = '';
      for await (const trozo of req) cuerpo += trozo;
      const { componente } = JSON.parse(cuerpo || '{}');
      res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(await abrirSesion(componente)));
    } else {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }).end(pagina);
    }
  } catch (error) {
    res.writeHead(502, { 'Content-Type': 'text/plain; charset=utf-8' }).end(String(error));
  }
}).listen(Number(PUERTO), () => console.log(`Anfitrión ficticio en ${ORIGEN_ANFITRION}`));
