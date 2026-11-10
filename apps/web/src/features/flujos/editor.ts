import type {
  AreaDto,
  CondicionDto,
  EtapaDto,
  FlujoVersionDto,
  PoliticaDevolucion,
  RequisitoDelFlujoDto,
  TipoDocumentoTenantDto,
  TipoVerificacion,
  VerificacionRequeridaDto,
} from '@validasoft/api-client';
import { requisitoDeSeccionADto, validarRequisitoDeSeccion } from './requisitosDeSeccion';

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

/** Documento (RQ-1) o sección de la información del tercero (F2, FM-25) que pide el flujo. */
export interface RequisitoEnEditor {
  /** Clave estable para React; el `id` de la API se conserva para que los expedientes reconozcan el requisito. */
  clave: string;
  id: string | null;
  tipoDocumentoTenantId: string;
  obligatorio: boolean;
  /** Áreas que lo revisan; vacío = todas las áreas del flujo. */
  areasQueRevisan: string[];
  /** `null` = los aprobadores de cada área. */
  aprobadoresRequeridos: number | null;
  /** «Se pide a» (RQ-12); `null` = siempre. Se conserva tal cual aunque el editor no la sepa armar. */
  condicion: CondicionDto | null;
  /** Sección de la ficha (`FICHA_GENERAL`…) en vez de documento; `null` = pide un documento. */
  seccion: string | null;
  /** Cierres financieros que se piden (solo la financiera; `null` = el último). */
  periodos: number | null;
  /** La versión de la sección debe traer soporte para estar completa para la empresa. */
  exigeSoporte: boolean;
}

/**
 * Verificación externa que pide el flujo (VE-6): listas restrictivas o estudio de cumplimiento. `null` en vigencia o
 * reconsulta = la de la configuración (estudio 30 días; listas cada 12 meses).
 */
export interface VerificacionEnEditor {
  tipo: TipoVerificacion;
  bloqueante: boolean;
  vigenciaDias: number | null;
  frecuenciaReconsultaMeses: number | null;
}

export const TIPOS_VERIFICACION: readonly TipoVerificacion[] = ['ListasRestrictivas', 'EstudioDeCumplimiento'];
export const MAXIMO_VIGENCIA_DIAS = 3650;
export const MAXIMO_MESES_RECONSULTA = 60;

export function verificacionNueva(tipo: TipoVerificacion): VerificacionEnEditor {
  return { tipo, bloqueante: true, vigenciaDias: null, frecuenciaReconsultaMeses: null };
}

export interface BorradorFlujo {
  codigo: string;
  nombre: string;
  /** Código del tipo de vínculo (solo al crear). */
  tipoVinculo: string;
  politicaDevolucion: PoliticaDevolucion;
  separacionDeFunciones: boolean;
  /** Plazo de cargue del expediente inicial (LT-1), como lo escribe la persona: días (0 = sin plazo) y recordatorios «15, 7, 1». */
  plazoDias: string;
  plazoRecordatorios: string;
  etapas: EtapaEnEditor[];
  requisitos: RequisitoEnEditor[];
  verificaciones: VerificacionEnEditor[];
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
  | 'sla-invalido'
  | 'requisito-sin-tipo'
  | 'requisito-duplicado'
  | 'requisito-invalido'
  | 'requisito-area-invalida'
  | 'requisito-aprobadores-invalidos'
  | 'requisito-siempre-requerido'
  | 'area-sin-requisitos'
  | 'seccion-invalida'
  | 'periodos-no-aplica'
  | 'periodos-invalidos'
  | 'vigencia-invalida'
  | 'reconsulta-invalida';

export interface ErrorFlujo {
  codigo: CodigoErrorFlujo;
  /** `codigo`, `nombre`, `tipoVinculo`, `etapas`, `etapa.<i>`, `area.<i>.<j>`, `requisito.<k>` o `requisito.<k>.periodos`. */
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

export function requisitoNuevo(tipoDocumentoTenantId = ''): RequisitoEnEditor {
  return {
    clave: nuevaClave(),
    id: null,
    tipoDocumentoTenantId,
    obligatorio: true,
    areasQueRevisan: [],
    aprobadoresRequeridos: null,
    condicion: null,
    seccion: null,
    periodos: null,
    exigeSoporte: false,
  };
}

/** Requisito de sección recién agregado: aún sin elegir la sección (`''`). */
export function requisitoDeSeccionNuevo(seccion = ''): RequisitoEnEditor {
  return { ...requisitoNuevo(), seccion };
}

export function borradorVacio(): BorradorFlujo {
  return {
    codigo: '',
    nombre: '',
    tipoVinculo: '',
    politicaDevolucion: 'Inmediata',
    separacionDeFunciones: false,
    plazoDias: String(PLAZO_POR_DEFECTO.dias),
    plazoRecordatorios: PLAZO_POR_DEFECTO.recordatorios.join(', '),
    etapas: [{ clave: nuevaClave(), areas: [areaNueva()] }],
    requisitos: [],
    verificaciones: [],
  };
}

/** Lleva una versión (publicada o borrador) al editor. */
export function borradorDesdeVersion(base: { codigo: string; nombre: string; tipoVinculo: string }, version: FlujoVersionDto): BorradorFlujo {
  return {
    ...base,
    politicaDevolucion: version.politicaDevolucion,
    separacionDeFunciones: version.separacionDeFunciones,
    plazoDias: String((version.plazoCargue ?? PLAZO_POR_DEFECTO).dias),
    plazoRecordatorios: (version.plazoCargue?.recordatorios ?? PLAZO_POR_DEFECTO.recordatorios).join(', '),
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
    requisitos: (version.requisitos ?? []).map((r) => ({
      clave: nuevaClave(),
      id: r.id ?? null,
      tipoDocumentoTenantId: r.tipoDocumentoTenantId ?? '',
      obligatorio: r.obligatorio ?? true,
      areasQueRevisan: [...(r.areasQueRevisan ?? [])],
      aprobadoresRequeridos: r.aprobadoresRequeridos ?? null,
      condicion: r.condicion ?? null,
      seccion: r.seccion ?? null,
      periodos: r.periodos ?? null,
      exigeSoporte: r.exigeSoporte ?? false,
    })),
    verificaciones: (version.verificaciones ?? []).map((v) => ({
      tipo: v.tipo,
      bloqueante: v.bloqueante ?? true,
      vigenciaDias: v.vigenciaDias ?? null,
      frecuenciaReconsultaMeses: v.frecuenciaReconsultaMeses ?? null,
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
export function validarFlujo(
  borrador: BorradorFlujo,
  areas: readonly AreaDto[],
  nuevo: boolean,
  tiposDocumento: readonly TipoDocumentoTenantDto[] = [],
): ErrorFlujo[] {
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
  errores.push(...validarRequisitos(borrador, tiposDocumento));
  errores.push(...validarVerificaciones(borrador.verificaciones));
  return errores;
}

/** Vigencia de 1 a 3650 días y reconsulta de listas de 1 a 60 meses (vacías = las de la configuración), como `DefinicionDeFlujo`. */
export function validarVerificaciones(verificaciones: readonly VerificacionEnEditor[]): ErrorFlujo[] {
  const errores: ErrorFlujo[] = [];
  for (const v of verificaciones) {
    const campo = `verificacion.${v.tipo}`;
    if (v.vigenciaDias !== null && (!Number.isInteger(v.vigenciaDias) || v.vigenciaDias < 1 || v.vigenciaDias > MAXIMO_VIGENCIA_DIAS)) {
      errores.push({ codigo: 'vigencia-invalida', campo });
    }
    if (
      v.tipo === 'ListasRestrictivas' &&
      v.frecuenciaReconsultaMeses !== null &&
      (!Number.isInteger(v.frecuenciaReconsultaMeses) || v.frecuenciaReconsultaMeses < 1 || v.frecuenciaReconsultaMeses > MAXIMO_MESES_RECONSULTA)
    ) {
      errores.push({ codigo: 'reconsulta-invalida', campo });
    }
  }
  return errores;
}

/** Verificaciones para la API, en el orden fijo de los tipos (la reconsulta solo va en listas). */
export function verificacionesADto(verificaciones: readonly VerificacionEnEditor[]): VerificacionRequeridaDto[] {
  return TIPOS_VERIFICACION.flatMap((tipo) => verificaciones.filter((v) => v.tipo === tipo)).map((v) => ({
    tipo: v.tipo,
    bloqueante: v.bloqueante,
    vigenciaDias: v.vigenciaDias,
    frecuenciaReconsultaMeses: v.tipo === 'ListasRestrictivas' ? v.frecuenciaReconsultaMeses : null,
  }));
}

/**
 * Un tipo se puede pedir en un flujo si está activo y el proveedor lo puede entregar (`cargaSoportada`: archivo, o
 * formulario en un tipo propio del vínculo, TD-4). Sin el dato (API anterior), solo los de clase Archivo.
 */
export function sePuedePedir(tipo: Pick<TipoDocumentoTenantDto, 'activo' | 'cargaSoportada' | 'clase'>): boolean {
  return tipo.activo && (tipo.cargaSoportada ?? tipo.clase === 'Archivo');
}

/**
 * Requisitos (RQ-1): tipo elegido, activo, que el proveedor pueda entregar y sin repetir; áreas del flujo; 1–10 aprobadores; y, si hay
 * requisitos, toda área del flujo revisa al menos uno (`area-sin-requisitos`: su aprobación se deriva de ellos) y al menos uno se pide
 * siempre (`requisito-siempre-requerido`, RQ-12: si todos tuvieran condición, a algún tercero no se le pediría nada).
 */
export function validarRequisitos(borrador: Pick<BorradorFlujo, 'etapas' | 'requisitos'>, tiposDocumento: readonly TipoDocumentoTenantDto[]): ErrorFlujo[] {
  const errores: ErrorFlujo[] = [];
  const areasDelFlujo = new Set(borrador.etapas.flatMap((e) => e.areas.map((a) => a.areaId)).filter(Boolean));
  const tipos = new Set<string>();
  const secciones = new Set<string>();
  borrador.requisitos.forEach((r, k) => {
    const campo = `requisito.${String(k)}`;
    if (r.seccion !== null) {
      errores.push(...validarRequisitoDeSeccion(r, campo, secciones));
    } else if (!r.tipoDocumentoTenantId) {
      errores.push({ codigo: 'requisito-sin-tipo', campo });
    } else {
      if (tipos.has(r.tipoDocumentoTenantId)) errores.push({ codigo: 'requisito-duplicado', campo });
      tipos.add(r.tipoDocumentoTenantId);
      const tipo = tiposDocumento.find((t) => t.id === r.tipoDocumentoTenantId);
      if (tiposDocumento.length > 0 && !(tipo && sePuedePedir(tipo))) errores.push({ codigo: 'requisito-invalido', campo });
    }
    if (r.areasQueRevisan.some((a) => !areasDelFlujo.has(a))) errores.push({ codigo: 'requisito-area-invalida', campo });
    if (r.aprobadoresRequeridos !== null && (!Number.isInteger(r.aprobadoresRequeridos) || r.aprobadoresRequeridos < 1 || r.aprobadoresRequeridos > MAXIMO_APROBADORES)) {
      errores.push({ codigo: 'requisito-aprobadores-invalidos', campo });
    }
  });
  if (borrador.requisitos.length > 0 && borrador.requisitos.every((r) => r.condicion !== null)) {
    borrador.requisitos.forEach((_, k) => errores.push({ codigo: 'requisito-siempre-requerido', campo: `requisito.${String(k)}` }));
  }
  if (borrador.requisitos.length > 0) {
    const cubiertas = new Set(borrador.requisitos.flatMap((r) => (r.areasQueRevisan.length === 0 ? [...areasDelFlujo] : r.areasQueRevisan)));
    borrador.etapas.forEach((etapa, i) =>
      etapa.areas.forEach((area, j) => {
        if (area.areaId && !cubiertas.has(area.areaId)) errores.push({ codigo: 'area-sin-requisitos', campo: `area.${String(i)}.${String(j)}` });
      }),
    );
  }
  return errores;
}

/** Requisitos para la API (`null` en aprobadores = los del área); una sección va sin tipo de documento. */
export function requisitosADto(requisitos: readonly RequisitoEnEditor[]): RequisitoDelFlujoDto[] {
  return requisitos.map((r) => (r.seccion !== null ? requisitoDeSeccionADto(r) : {
    id: r.id,
    tipoDocumentoTenantId: r.tipoDocumentoTenantId,
    tipoDocumento: null,
    obligatorio: r.obligatorio,
    areasQueRevisan: r.areasQueRevisan,
    aprobadoresRequeridos: r.aprobadoresRequeridos,
    condicion: r.condicion,
  }));
}

/** Quita de los requisitos las áreas que ya no están en el flujo (al quitar un área de una etapa). */
export function limpiarAreasDeRequisitos(borrador: BorradorFlujo): BorradorFlujo {
  const areasDelFlujo = new Set(borrador.etapas.flatMap((e) => e.areas.map((a) => a.areaId)));
  return { ...borrador, requisitos: borrador.requisitos.map((r) => ({ ...r, areasQueRevisan: r.areasQueRevisan.filter((a) => areasDelFlujo.has(a)) })) };
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
    plazo: plazoADto(borrador),
    etapas: etapasADto(borrador.etapas),
    requisitos: requisitosADto(borrador.requisitos),
    verificaciones: verificacionesADto(borrador.verificaciones),
  });
}

/** Plazo de cargue por omisión (P-4-2): 30 días con recordatorios a 15, 7 y 1 día de vencer. */
export const PLAZO_POR_DEFECTO = { dias: 30, recordatorios: [15, 7, 1] } as const;

/**
 * El plazo del editor como lo recibe la API: días enteros y recordatorios (números separados por coma o espacio, sin
 * repetir); lo que no sea número se descarta y la API valida los rangos (`plazo-cargue-invalido`, `recordatorio-invalido`).
 */
export function plazoADto(borrador: Pick<BorradorFlujo, 'plazoDias' | 'plazoRecordatorios'>): { dias: number; recordatorios: number[] } {
  const dias = Number.parseInt(borrador.plazoDias.trim(), 10);
  const recordatorios = borrador.plazoRecordatorios
    .split(/[\s,;]+/)
    .map((r) => Number.parseInt(r, 10))
    .filter((r) => Number.isFinite(r));
  return { dias: Number.isFinite(dias) ? dias : PLAZO_POR_DEFECTO.dias, recordatorios: [...new Set(recordatorios)].sort((a, b) => b - a) };
}
