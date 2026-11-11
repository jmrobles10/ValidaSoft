// Llamadas a la CLI de Docker (Docker Desktop con WSL2 en esta máquina). Sin shell: los argumentos pasan tal cual.
import { spawn } from 'node:child_process';
import { createReadStream, createWriteStream } from 'node:fs';

/** Procesos hijos vivos, para detenerlos si la corrida se interrumpe. */
export const hijos = new Set();

/**
 * Corre `docker <args>` y espera su fin.
 * @param {string[]} args
 * @param {{ log?: string, eco?: (linea: string) => void, entradaDesde?: string, entrada?: string, limiteMs?: number }} [opciones]
 *   `log`: archivo donde queda toda la salida (stdout y stderr mezclados); `eco`: recibe cada línea en vivo; `entradaDesde`: archivo
 *   que se envía por stdin; `limiteMs`: tiempo máximo (mata la CLI; el contenedor lo limpia quien lo creó).
 * @returns {Promise<{ codigo: number, salida: string, agotado: boolean, ms: number }>}
 */
export function docker(args, opciones = {}) {
  return new Promise((resolver) => {
    const inicio = Date.now();
    const proceso = spawn('docker', args, { stdio: [opciones.entradaDesde || opciones.entrada !== undefined ? 'pipe' : 'ignore', 'pipe', 'pipe'], windowsHide: true });
    hijos.add(proceso);
    const archivo = opciones.log ? createWriteStream(opciones.log, { flags: 'a' }) : null;
    const trozos = [];
    let pendiente = '';
    let agotado = false;
    const alRecibir = (datos) => {
      archivo?.write(datos);
      trozos.push(datos);
      if (opciones.eco) {
        pendiente += datos.toString('utf8');
        let salto;
        while ((salto = pendiente.indexOf('\n')) >= 0) {
          opciones.eco(pendiente.slice(0, salto).replace(/\r$/, ''));
          pendiente = pendiente.slice(salto + 1);
        }
      }
    };
    proceso.stdout.on('data', alRecibir);
    proceso.stderr.on('data', alRecibir);
    if (opciones.entradaDesde) {
      const lectura = createReadStream(opciones.entradaDesde);
      lectura.pipe(proceso.stdin);
      proceso.stdin.on('error', () => undefined);
    } else if (opciones.entrada !== undefined) {
      proceso.stdin.on('error', () => undefined);
      proceso.stdin.end(opciones.entrada);
    }
    const temporizador = opciones.limiteMs
      ? setTimeout(() => {
          agotado = true;
          proceso.kill();
        }, opciones.limiteMs)
      : null;
    const terminar = (codigo) => {
      if (temporizador) clearTimeout(temporizador);
      hijos.delete(proceso);
      if (opciones.eco && pendiente) opciones.eco(pendiente);
      const fin = () => resolver({ codigo: codigo ?? 1, salida: Buffer.concat(trozos).toString('utf8'), agotado, ms: Date.now() - inicio });
      if (archivo) archivo.end(fin);
      else fin();
    };
    proceso.on('error', (e) => {
      alRecibir(Buffer.from(`No se pudo ejecutar docker: ${e.message}\n`));
      terminar(127);
    });
    proceso.on('close', terminar);
  });
}

/** `docker <args>` que debe salir bien: devuelve la salida o lanza con ella. */
export async function dockerOk(args, opciones = {}) {
  const r = await docker(args, opciones);
  if (r.codigo !== 0) throw new Error(`docker ${args.slice(0, 3).join(' ')} falló (${String(r.codigo)}):\n${r.salida.trim()}`);
  return r.salida;
}

/** Detiene los procesos hijos que sigan vivos (la CLI de docker; los contenedores los limpia la corrida). */
export function detenerHijos() {
  for (const h of hijos) h.kill();
}
