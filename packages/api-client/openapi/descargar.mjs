// Actualiza el snapshot del contrato desde una API levantada (por defecto http://localhost:5080).
//   pnpm --filter @validasoft/api-client openapi:actualizar
// Después: `pnpm generar:api` y revisar el diff del cliente generado.
import { writeFile } from 'node:fs/promises';

const base = process.env.VALIDASOFT_API_URL ?? 'http://localhost:5080';
const respuesta = await fetch(`${base}/openapi/v1.json`);
if (!respuesta.ok) {
  console.error(`No se pudo leer ${base}/openapi/v1.json (${respuesta.status}).`);
  process.exit(1);
}
const documento = await respuesta.json();
await writeFile(new URL('./v1.json', import.meta.url), JSON.stringify(documento, null, 2) + '\n', 'utf8');
console.log(`Snapshot actualizado desde ${base}.`);
