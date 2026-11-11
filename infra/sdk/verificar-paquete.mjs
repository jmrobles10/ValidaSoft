// Humo del paquete construido: las dos salidas (ESM y CJS) cargan, exponen lo mismo y validan un webhook firmado de punta a punta.
// Corre después de `pnpm --filter @validasoft/sdk build` (script `verificar` del paquete).
import { createRequire } from 'node:module';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const paquete = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', 'packages', 'sdk');
const esm = await import(pathToFileURL(join(paquete, 'dist', 'esm', 'index.js')).href);
const cjs = createRequire(import.meta.url)(join(paquete, 'dist', 'cjs', 'index.js'));

const funciones = ['crearFirmanteDeAserciones', 'verificarWebhook', 'validarWebhook', 'firmarWebhook', 'leerCabecera'];
const grupos = ['vinculos', 'invitaciones', 'expedientes', 'certificados', 'consultas', 'eventos', 'sesiones', 'sandbox'];

for (const [nombre, modulo] of [['ESM', esm], ['CJS', cjs]]) {
  for (const exportado of ['ClienteValidasoft', 'ErrorValidasoft', 'WebhookInvalidoError', ...funciones]) {
    if (typeof modulo[exportado] !== 'function') throw new Error(`${nombre}: falta ${exportado}.`);
  }

  const cliente = new modulo.ClienteValidasoft({ urlBase: 'https://valida.test', apiKey: 'vsk_test_x_0' });
  for (const grupo of grupos) {
    if (typeof cliente[grupo] !== 'object') throw new Error(`${nombre}: el cliente no tiene «${grupo}».`);
  }

  // Un webhook firmado como lo hace el servidor se valida con esta salida, y uno alterado se rechaza.
  const secreto = 'whsec_prueba';
  const cuerpo = JSON.stringify({ id: 'e1', tipo: 'certificado.emitido', version: 1, ocurridoEn: new Date().toISOString(), tenantId: 't', empresaId: null, cursor: '1', datos: {} });
  const firma = await modulo.firmarWebhook(secreto, Math.floor(Date.now() / 1000), cuerpo);
  const evento = await modulo.validarWebhook({ secretos: secreto, firma, cuerpo });
  if (evento.id !== 'e1') throw new Error(`${nombre}: el webhook no se validó.`);
  if (await modulo.verificarWebhook({ secretos: secreto, firma, cuerpo: cuerpo.replace('e1', 'e2') })) throw new Error(`${nombre}: aceptó un cuerpo alterado.`);
}

console.log('✓ @validasoft/sdk: ESM y CJS cargan, exponen lo mismo y validan un webhook firmado.');
