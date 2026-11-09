import { createHmac, timingSafeEqual } from 'node:crypto';
import { createServer, type Server } from 'node:http';
import type { AddressInfo } from 'node:net';

/** Una entrega recibida por el sistema integrado de prueba. */
export interface Entrega {
  tipo: string;
  id: string;
  cuerpo: string;
  firma: string;
  datos: Record<string, unknown>;
}

/**
 * Receptor de webhooks del «sistema integrado»: un servidor HTTP local (Validasoft en desarrollo admite http hacia
 * localhost) que guarda cada entrega y responde 200, o 500 si `fallar` está encendido.
 */
export class Receptor {
  readonly entregas: Entrega[] = [];
  fallar = false;
  private constructor(
    private readonly servidor: Server,
    readonly url: string,
  ) {}

  static async iniciar(): Promise<Receptor> {
    const ref: { receptor?: Receptor } = {};
    const servidor = createServer((peticion, respuesta) => {
      let cuerpo = '';
      peticion.setEncoding('utf8');
      peticion.on('data', (parte: string) => (cuerpo += parte));
      peticion.on('end', () => {
        const json = JSON.parse(cuerpo || '{}') as { tipo?: string; id?: string; datos?: Record<string, unknown> };
        ref.receptor?.entregas.push({
          tipo: json.tipo ?? '',
          id: json.id ?? '',
          cuerpo,
          firma: String(peticion.headers['validasoft-signature'] ?? ''),
          datos: json.datos ?? {},
        });
        respuesta.statusCode = ref.receptor?.fallar ? 500 : 200;
        respuesta.end();
      });
    });
    await new Promise<void>((listo) => servidor.listen(0, '127.0.0.1', listo));
    const { port } = servidor.address() as AddressInfo;
    ref.receptor = new Receptor(servidor, `http://localhost:${String(port)}/validasoft`);
    return ref.receptor;
  }

  /** Espera hasta que haya una entrega que cumpla `condicion`. */
  async esperar(condicion: (e: Entrega) => boolean, segundos = 45): Promise<Entrega> {
    const limite = Date.now() + segundos * 1000;
    while (Date.now() < limite) {
      const encontrada = this.entregas.find(condicion);
      if (encontrada) return encontrada;
      await new Promise((r) => setTimeout(r, 300));
    }
    throw new Error(`No llegó la entrega esperada. Llegaron: ${this.entregas.map((e) => e.tipo).join(', ') || '(ninguna)'}`);
  }

  async cerrar(): Promise<void> {
    await new Promise<void>((listo) => this.servidor.close(() => listo()));
  }
}

/** `Validasoft-Signature: t={hora},v1={hex}` con `v1 = HMAC-SHA256(secreto, "{hora}.{cuerpo}")`, a menos de 5 minutos. */
export function firmaValida(secreto: string, firma: string, cuerpo: string): boolean {
  const partes = Object.fromEntries(firma.split(',').map((p) => p.split('=') as [string, string]));
  const t = partes.t;
  const v1 = partes.v1;
  if (!t || !v1 || Math.abs(Date.now() / 1000 - Number(t)) > 300) return false;
  const esperado = createHmac('sha256', secreto).update(`${t}.${cuerpo}`).digest('hex');
  return esperado.length === v1.length && timingSafeEqual(Buffer.from(esperado), Buffer.from(v1));
}
