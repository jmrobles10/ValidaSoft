// Pasos de empaquetado de @validasoft/sdk que tsc no hace (los llama el script `build` del paquete):
//   - dist/cjs/package.json con "type": "commonjs", para que Node lea la salida CJS como tal aunque el paquete sea ESM;
//   - LICENSE.md copiado de LICENCIA-SDK.md (la fuente única), que npm exige dentro de la carpeta del paquete.
import { copyFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const paquete = join(raiz, 'packages', 'sdk');

mkdirSync(join(paquete, 'dist', 'cjs'), { recursive: true });
writeFileSync(join(paquete, 'dist', 'cjs', 'package.json'), `${JSON.stringify({ type: 'commonjs' }, null, 2)}\n`);
copyFileSync(join(raiz, 'LICENCIA-SDK.md'), join(paquete, 'LICENSE.md'));
