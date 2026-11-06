import type { AreaDto, EtapaDto, FlujoVersionDto, PoliticaDevolucion } from '@validasoft/api-client';

/**
 * Modelo del editor de flujos y su validación. Replica `DefinicionDeFlujo.Validar` y `Flujo.Crear` del dominio
 * (FL-2) para avisar antes de enviar; la API vuelve a validar.
 */

export const MAXIMO_APROBADORES = 10;
const PATRON_CODIGO = /^[A-Z][A-Z0-9_]{1,39}$/;

export interface AreaEnEditor {
  areaId: string;
  aprobadoresRequeridos: number;
  puedeRechazarExpediente: boolean;
  automatica: boolean;
  /** Horas; `null` = sin SLA. */
  slaHoras: number | null;
}

export interface EtapaEnEditor {
  /** Clave estable para React (las etapas se reordenan). */
  clave: string;
  areas: AreaEnEditor[];
}

export interface BorradorFlujo {
  codigo: string;
  nombre: string;
  /** Código del tipo de vínculo (solo al crear). */
  tipoVinculo: string;
  politicaDevolucion: PoliticaDevolucion;
  separacionDeFunciones: boolean;
  etapas: EtapaEnEditor[];
}

export type CodigoErrorFlujo =
  | 'codigo-invalido'
  | 'nombre-requerido'
  | 'tipo-requerido'
  | 'etapas-requeridas'
  | 'etapa-sin-areas'
  | 'area-sin-elegir'
  | 'area-repetida'
  | 'area-invalida'
  | 'aprobadores-invalidos'
  | 'aprobadores-sin-miembros'
  | 'area-automatica-no-rechaza'
  | 'sla-invalido';

export interface ErrorFlujo {
  codigo: CodigoErrorFlujo;
  /** `codigo`, `nombre`, `tipoVinculo`, `etapas`, `etapa.<i>` o `area.<i>.<j>`. */
  campo: string;
  /** Es aviso: no impide guardar (la API lo acepta) pero el flujo no avanzaría. */
  aviso?: boolean;
}

let contador = 0;
export function nuevaClave(): string {
  contador += 1;
  return `etapa-${String(contador)}`;
}

export function areaNueva(areaId = ''): AreaEnEditor {
  return { areaId, aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: false, slaHoras: null };
}

export function borradorVacio(): BorradorFlujo {
  return { codigo: '', nombre: '', tipoVinculo: '', politicaDevolucion: 'Inmediata', separacionDeFunciones: false, etapas: [{ clave: nuevaClave(), areas: [areaNueva()] }] };
}

/** Lleva una versión (publicada o borrador) al editor. */
export function borradorDesdeVersion(base: { codigo: string; nombre: string; tipoVinculo: string }, version: FlujoVersionDto): BorradorFlujo {
  return {
    ...base,
    politicaDevolucion: version.politicaDevolucion,
    separacionDeFunciones: version.separacionDeFunciones,
    etapas: [...version.etapas]
      .sort((a, b) => a.orden - b.orden)
      .map((e) => ({
        clave: nuevaClave(),
        areas: e.areas.map((a) => ({
          areaId: a.areaId,
          aprobadoresRequeridos: a.aprobadoresRequeridos ?? 1,
          puedeRechazarExpediente: a.puedeRechazarExpediente ?? false,
          automatica: a.automatica ?? false,
          slaHoras: a.slaHoras ?? null,
        })),
      })),
  };
}

/** Normaliza el código como lo hace la API (mayúsculas, sin espacios a los lados). */
export function normalizarCodigo(codigo: string): string {
  return codigo.trim().toUpperCase();
}

/**
 * Errores del borrador. `nuevo` valida además código y tipo de vínculo (inmutables después). Las áreas inactivas o de
 * otra empresa son error (`area-invalida`); más aprobadores que miembros es aviso (el área nunca se completaría).
 */
export function validarFlujo(borrador: BorradorFlujo, areas: readonly AreaDto[], nuevo: boolean): ErrorFlujo[] {
  const errores: ErrorFlujo[] = [];
  if (nuevo) {
    if (!PATRON_CODIGO.test(normalizarCodigo(borrador.codigo))) errores.push({ codigo: 'codigo-invalido', campo: 'codigo' });
    if (!borrador.tipoVinculo) errores.push({ codigo: 'tipo-requerido', campo: 'tipoVinculo' });
  }
  if (!borrador.nombre.trim()) errores.push({ codigo: 'nombre-requerido', campo: 'nombre' });
  if (borrador.etapas.length === 0) errores.push({ codigo: 'etapas-requeridas', campo: 'etapas' });

  const vistas = new Set<string>();
  borrador.etapas.forEach((etapa, i) => {
    if (etapa.areas.length === 0) errores.push({ codigo: 'etapa-sin-areas', campo: `etapa.${String(i)}` });
    etapa.areas.forEach((area, j) => {
      const campo = `area.${String(i)}.${String(j)}`;
      if (!area.areaId) {
        errores.push({ codigo: 'area-sin-elegir', campo });
        return;
      }
      if (vistas.has(area.areaId)) errores.push({ codigo: 'area-repetida', campo });
      vistas.add(area.areaId);

      const existente = areas.find((a) => a.id === area.areaId);
      if (!existente?.activa) errores.push({ codigo: 'area-invalida', campo });
      if (area.slaHoras !== null && (!Number.isInteger(area.slaHoras) || area.slaHoras <= 0)) errores.push({ codigo: 'sla-invalido', campo });

      if (area.automatica) {
        if (area.puedeRechazarExpediente) errores.push({ codigo: 'area-automatica-no-rechaza', campo });
        return;
      }
      if (!Number.isInteger(area.aprobadoresRequeridos) || area.aprobadoresRequeridos < 1 || area.aprobadoresRequeridos > MAXIMO_APROBADORES) {
        errores.push({ codigo: 'aprobadores-invalidos', campo });
      } else if (existente && area.aprobadoresRequeridos > existente.miembros.length) {
        errores.push({ codigo: 'aprobadores-sin-miembros', campo, aviso: true });
      }
    });
  });
  return errores;
}

/** Solo los errores que impiden guardar. */
export function erroresBloqueantes(errores: readonly ErrorFlujo[]): ErrorFlujo[] {
  return errores.filter((e) => !e.aviso);
}

/** Etapas para la API: orden 1..n en el orden del editor; un área automática cuenta como un aprobador y no rechaza. */
export function etapasADto(etapas: readonly EtapaEnEditor[]): EtapaDto[] {
  return etapas.map((etapa, i) => ({
    orden: i + 1,
    areas: etapa.areas.map((a) => ({
      areaId: a.areaId,
      aprobadoresRequeridos: a.automatica ? 1 : a.aprobadoresRequeridos,
      puedeRechazarExpediente: a.automatica ? false : a.puedeRechazarExpediente,
      automatica: a.automatica,
      slaHoras: a.slaHoras,
    })),
  }));
}

/** Mueve la etapa `desde` una posición arriba (-1) o abajo (+1). */
export function moverEtapa(etapas: readonly EtapaEnEditor[], desde: number, direccion: -1 | 1): EtapaEnEditor[] {
  const hasta = desde + direccion;
  if (hasta < 0 || hasta >= etapas.length) return [...etapas];
  const copia = [...etapas];
  const [movida] = copia.splice(desde, 1);
  if (movida) copia.splice(hasta, 0, movida);
  return copia;
}

/** Áreas activas que aún no están en el flujo (para el selector), más la que ya tiene la fila. */
export function areasElegibles(areas: readonly AreaDto[], etapas: readonly EtapaEnEditor[], actual: string): AreaDto[] {
  const usadas = new Set(etapas.flatMap((e) => e.areas.map((a) => a.areaId)));
  return areas.filter((a) => a.id === actual || (a.activa && !usadas.has(a.id)));
}

/** Firma comparable del contenido editable (para saber si hay cambios sin guardar). */
export function firmaBorrador(borrador: BorradorFlujo): string {
  return JSON.stringify({
    nombre: borrador.nombre.trim(),
    politica: borrador.politicaDevolucion,
    separacion: borrador.separacionDeFunciones,
    etapas: etapasADto(borrador.etapas),
  });
}
