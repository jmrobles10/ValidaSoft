import type { CrearSolicitudHabeasDto, ExportacionBovedaDto, SolicitudHabeasDto } from '@validasoft/api-client';

/** Derechos que el tercero puede ejercer (Ley 1581 de 2012): conocer sus datos, corregirlos o pedir que se supriman. */
export const TIPOS_DE_SOLICITUD = ['Consulta', 'Correccion', 'Supresion'] as const;
export type TipoDeSolicitud = (typeof TIPOS_DE_SOLICITUD)[number];

/** Qué se suprime (solo en la supresión). */
export const ALCANCES_DE_SUPRESION = ['Todo', 'Documentos', 'Datos'] as const;
export type AlcanceDeSupresion = (typeof ALCANCES_DE_SUPRESION)[number];

/** La plataforma responde por la bóveda; una empresa, por lo que tiene de sus vínculos. */
export const DESTINO_PLATAFORMA = 'plataforma';

export const MAXIMO_DETALLE = 2000;
export const MINIMO_DETALLE_CORRECCION = 10;

/** Días hábiles de la ley: 10 las consultas, 15 los reclamos (corrección y supresión). */
export function diasHabilesDe(tipo: TipoDeSolicitud): number {
  return tipo === 'Consulta' ? 10 : 15;
}

export function esTipoDeSolicitud(valor: string): valor is TipoDeSolicitud {
  return (TIPOS_DE_SOLICITUD as readonly string[]).includes(valor);
}

export function esAlcanceDeSupresion(valor: string): valor is AlcanceDeSupresion {
  return (ALCANCES_DE_SUPRESION as readonly string[]).includes(valor);
}

export interface BorradorDeSolicitud {
  tipo: TipoDeSolicitud | null;
  /** `DESTINO_PLATAFORMA` o el id de una empresa. */
  destino: string;
  alcance: AlcanceDeSupresion;
  detalle: string;
}

export const BORRADOR_VACIO: BorradorDeSolicitud = { tipo: null, destino: DESTINO_PLATAFORMA, alcance: 'Todo', detalle: '' };

export type ErrorDeSolicitud = 'tipo-requerido' | 'detalle-requerido' | 'detalle-largo';

/** Lo que falta o sobra antes de enviar (el primero va en el aviso del botón). */
export function validarSolicitud(b: BorradorDeSolicitud): ErrorDeSolicitud[] {
  const errores: ErrorDeSolicitud[] = [];
  if (b.tipo === null) errores.push('tipo-requerido');
  const detalle = b.detalle.trim();
  if (b.tipo === 'Correccion' && detalle.length < MINIMO_DETALLE_CORRECCION) errores.push('detalle-requerido');
  if (detalle.length > MAXIMO_DETALLE) errores.push('detalle-largo');
  return errores;
}

export function cuerpoDeSolicitud(b: BorradorDeSolicitud): CrearSolicitudHabeasDto {
  return {
    tipo: b.tipo ?? 'Consulta',
    alcance: b.tipo === 'Supresion' ? b.alcance : null,
    empresaId: b.destino === DESTINO_PLATAFORMA ? null : b.destino,
    detalle: b.detalle.trim() || null,
  };
}

/** Cómo se muestra el plazo de una solicitud (`Plazo` del servidor). */
export type EstadoDePlazo = 'EnPlazo' | 'PorVencer' | 'Vencida' | 'Resuelta';

export function estadoDePlazo(s: Pick<SolicitudHabeasDto, 'plazo'>): EstadoDePlazo {
  return s.plazo === 'PorVencer' || s.plazo === 'Vencida' || s.plazo === 'Resuelta' ? s.plazo : 'EnPlazo';
}

/** Las abiertas primero (la que vence antes arriba) y luego las resueltas, la más reciente primero. */
export function ordenarSolicitudes(lista: readonly SolicitudHabeasDto[]): SolicitudHabeasDto[] {
  return [...lista].sort((a, b) => {
    const abiertaA = a.estado === 'Recibida';
    const abiertaB = b.estado === 'Recibida';
    if (abiertaA !== abiertaB) return abiertaA ? -1 : 1;
    if (abiertaA) return a.venceEl.localeCompare(b.venceEl);
    return b.recibidaEn.localeCompare(a.recibidaEn);
  });
}

/** ¿Hay una igual sin resolver para ese derecho y destino? El servidor responde 409; aquí se avisa antes de enviar. */
export function hayIgualEnTramite(lista: readonly SolicitudHabeasDto[], tipo: TipoDeSolicitud | null, destino: string): SolicitudHabeasDto | null {
  if (tipo === null) return null;
  return (
    lista.find((s) => s.estado === 'Recibida' && s.tipo === tipo && (destino === DESTINO_PLATAFORMA ? s.destino.empresaId === null : s.destino.empresaId === destino)) ??
    null
  );
}

export type EstadoDeExportacion = 'Pendiente' | 'Generando' | 'Lista' | 'Fallida' | 'Vencida';

export function esEstadoDeExportacion(valor: string): valor is EstadoDeExportacion {
  return valor === 'Pendiente' || valor === 'Generando' || valor === 'Lista' || valor === 'Fallida' || valor === 'Vencida';
}

export type ResultadoDeSolicitud = 'Atendida' | 'Parcial' | 'Rechazada';

export function esResultadoDeSolicitud(valor: string): valor is ResultadoDeSolicitud {
  return valor === 'Atendida' || valor === 'Parcial' || valor === 'Rechazada';
}

export function enCurso(e: Pick<ExportacionBovedaDto, 'estado'>): boolean {
  return e.estado === 'Pendiente' || e.estado === 'Generando';
}

/** Mientras haya una exportación preparándose, la lista se refresca cada 3 segundos. */
export function intervaloDeRefresco(lista: readonly ExportacionBovedaDto[] | undefined): number | false {
  return lista?.some(enCurso) ? 3000 : false;
}

/** Tamaño legible en es-CO: `4,2 MB`. */
export function tamanoLegible(bytes: number): string {
  if (bytes < 1024) return `${String(bytes)} B`;
  const unidades = ['KB', 'MB', 'GB'];
  let valor = bytes / 1024;
  let i = 0;
  while (valor >= 1024 && i < unidades.length - 1) {
    valor /= 1024;
    i += 1;
  }
  return `${valor.toLocaleString('es-CO', { maximumFractionDigits: 1 })} ${unidades[i] ?? 'GB'}`;
}
