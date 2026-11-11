import type { SesionTerceroDto } from '@validasoft/api-client';
import { leerReclamosDeJwt, origenPermitido } from '@validasoft/sdk';

/**
 * Lógica pura del modo embebido del tercero (IO-5, docs/06 §3.2): la app corre dentro del iframe de un sistema integrador, que le entrega
 * por `postMessage` un token de pocos minutos emitido para **un** origen, un vínculo y un componente. Aquí se decide si el token se acepta
 * y a dónde lleva; el cableado con el navegador está en `usePuenteEmbebido`.
 */

export type ComponenteEmbebido = 'boveda' | 'expediente' | 'ficha';

const COMPONENTES: readonly string[] = ['boveda', 'expediente', 'ficha'];

export function esComponente(valor: string | undefined): valor is ComponenteEmbebido {
  return valor !== undefined && COMPONENTES.includes(valor);
}

/** Lo que el token dice de sí mismo (la firma la verifica la API en cada petición; esto solo evita usar un token ajeno o vencido). */
export interface ReclamosEmbebidos {
  origen: string;
  componente: string;
  sistema: string;
  expira: number | null;
}

export function reclamosEmbebidos(token: string): ReclamosEmbebidos | null {
  const r = leerReclamosDeJwt(token);
  if (r?.embebido !== 'true' || typeof r.origen !== 'string' || typeof r.componente !== 'string') return null;
  return { origen: r.origen, componente: r.componente, sistema: typeof r.sistema === 'string' ? r.sistema : '', expira: typeof r.exp === 'number' ? r.exp * 1000 : null };
}

export type VeredictoDeSesion = { aceptada: true; reclamos: ReclamosEmbebidos } | { aceptada: false; motivo: 'origen-no-permitido' | 'token-ajeno' | 'token-vencido' };

/**
 * ¿Se acepta el token que llegó de `origenDelMensaje`? Debe venir de un origen permitido que además sea el del contenedor de esta
 * ventana (si se conoce), ser un token embebido y estar dirigido a ese mismo origen, y no haber vencido.
 */
export function evaluarSesion(
  token: string,
  origenDelMensaje: string,
  permitidos: readonly string[],
  contenedor: string | null,
  ahora: number,
): VeredictoDeSesion {
  if (!origenPermitido(origenDelMensaje, permitidos) || (contenedor !== null && contenedor !== origenDelMensaje)) return { aceptada: false, motivo: 'origen-no-permitido' };
  const reclamos = reclamosEmbebidos(token);
  if (reclamos?.origen !== origenDelMensaje || !esComponente(reclamos.componente)) return { aceptada: false, motivo: 'token-ajeno' };
  if (reclamos.expira !== null && reclamos.expira <= ahora) return { aceptada: false, motivo: 'token-vencido' };
  return { aceptada: true, reclamos };
}

/** A qué ruta de la app lleva cada componente; el expediente va a la revisión abierta del vínculo (o a la empresa si no hay). */
export function rutaDelComponente(componente: ComponenteEmbebido, sesion: SesionTerceroDto, expedienteId: string | null): string {
  switch (componente) {
    case 'boveda':
      return '/boveda';
    case 'ficha':
      return '/ficha';
    case 'expediente': {
      const vinculo = sesion.vinculos[0];
      if (expedienteId) return `/expediente/${expedienteId}`;
      return vinculo ? `/empresas/${vinculo.id}` : '/empresas';
    }
  }
}

/** Cuánto antes de vencer se le pide otra sesión al sistema anfitrión. */
export const ANTICIPO_PEDIDO_MS = 60_000;
