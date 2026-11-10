import type { ActorAuditoriaDto, ListarAuditoriaParams } from '@validasoft/api-client';
import type { TFunction } from 'i18next';

/** Tipos que la API responde hoy; `Dominio` e `Identidad` dan 422 `tipo-no-disponible` y no se ofrecen. */
export const TIPOS_AUDITORIA = ['Configuracion', 'Acceso'] as const;
export type TipoAuditoria = (typeof TIPOS_AUDITORIA)[number];
export const TIPO_POR_DEFECTO: TipoAuditoria = 'Configuracion';

/** Entidades de configuración que se auditan (`RegistroDeCambioDeConfiguracion.Entidad`, filtro exacto). */
export const ENTIDADES_CONFIGURACION = [
  'Empresa',
  'Usuario',
  'Area',
  'TipoVinculo',
  'TipoDocumentoTenant',
  'PlantillaTipoDocumento',
  'Motivo',
  'Flujo',
  'FlujoVersion',
  'FlujoPorDefecto',
  'Webhook',
  'SistemaIntegrado',
  'ApiKey',
  'TextoLegal',
  'AccesoSoporte',
] as const;
export const ENTIDADES_ACCESO = ['Documento', 'Certificado', 'Expediente', 'Exportacion'] as const;
export type EntidadAuditada = (typeof ENTIDADES_CONFIGURACION)[number] | (typeof ENTIDADES_ACCESO)[number];

export const ACCIONES = ['Creado', 'Modificado', 'Publicado', 'Archivado', 'Desactivado', 'Rotado', 'Revocado', 'Descargado', 'Exportado'] as const;
export const ACTORES = ['Usuario', 'Tercero', 'Integracion', 'Sistema', 'Plataforma', 'Migracion'] as const;
export const TIPOS_CAMBIO = ['Agregado', 'Modificado', 'Eliminado'] as const;
export type TipoCambio = (typeof TIPOS_CAMBIO)[number];

/** Espera de los campos de texto (ids) antes de escribirlos en la URL. */
export const ESPERA_TEXTO_MS = 400;
/** Largo a partir del cual un valor del diff se recorta con «Ver más». */
export const LARGO_RECORTE = 200;

/** La API tipa estos campos como texto: se estrechan aquí y lo desconocido se muestra tal cual. */
export function esUnoDe<T extends string>(valores: readonly T[], valor: unknown): valor is T {
  return (valores as readonly unknown[]).includes(valor);
}

export const entidadesDe = (tipo: TipoAuditoria): readonly EntidadAuditada[] => (tipo === 'Acceso' ? ENTIDADES_ACCESO : ENTIDADES_CONFIGURACION);

export interface FiltrosAuditoria {
  tipo: TipoAuditoria;
  entidad: EntidadAuditada | '';
  entidadId: string;
  actor: string;
  /** Días calendario `AAAA-MM-DD` (los del campo de fecha), en la zona del navegador. */
  desde: string;
  hasta: string;
}

const PATRON_DIA = /^\d{4}-\d{2}-\d{2}$/;
const dia = (valor: string | null) => (valor && PATRON_DIA.test(valor) ? valor : '');

/** Filtros de la URL (`?tipo=Acceso&entidad=&entidadId=&actor=&desde=&hasta=`); lo inválido se ignora. */
export function leerFiltros(params: URLSearchParams): FiltrosAuditoria {
  const tipoUrl = params.get('tipo');
  const tipo = esUnoDe(TIPOS_AUDITORIA, tipoUrl) ? tipoUrl : TIPO_POR_DEFECTO;
  const entidad = params.get('entidad');
  return {
    tipo,
    entidad: esUnoDe(entidadesDe(tipo), entidad) ? entidad : '',
    entidadId: (params.get('entidadId') ?? '').trim(),
    actor: (params.get('actor') ?? '').trim(),
    desde: dia(params.get('desde')),
    hasta: dia(params.get('hasta')),
  };
}

export const hayFiltros = (f: FiltrosAuditoria) => Boolean(f.entidad || f.entidadId || f.actor || f.desde || f.hasta);

/** Con las dos fechas, «desde» no puede ser posterior a «hasta» (la API respondería 422 `rango-invalido`). */
export const rangoInvalido = (f: Pick<FiltrosAuditoria, 'desde' | 'hasta'>) => Boolean(f.desde && f.hasta && f.desde > f.hasta);

/** Inicio (00:00:00.000) o fin (23:59:59.999) del día en la zona del navegador, en ISO-8601 UTC. */
export function limiteDelDia(valor: string, extremo: 'inicio' | 'fin'): string {
  const [anio = 0, mes = 1, d = 1] = valor.split('-').map(Number);
  const fecha = extremo === 'inicio' ? new Date(anio, mes - 1, d) : new Date(anio, mes - 1, d, 23, 59, 59, 999);
  return fecha.toISOString();
}

/** Parámetros de la consulta: lo vacío no viaja; el tipo por defecto, tampoco. */
export function parametrosConsulta(f: FiltrosAuditoria, limite: number, cursor: string | null): ListarAuditoriaParams {
  return {
    tipo: f.tipo === TIPO_POR_DEFECTO ? undefined : f.tipo,
    entidad: f.entidad || undefined,
    entidadId: f.entidadId || undefined,
    actor: f.actor || undefined,
    desde: f.desde ? limiteDelDia(f.desde, 'inicio') : undefined,
    hasta: f.hasta ? limiteDelDia(f.hasta, 'fin') : undefined,
    limite,
    cursor: cursor ?? undefined,
  };
}

/** 09/11/2026, 3:05:12 p. m. en la zona del navegador (la auditoría se lee con segundos). */
export function formatearMomento(valor: string): string {
  return new Intl.DateTimeFormat('es-CO', { day: '2-digit', month: '2-digit', year: 'numeric', hour: 'numeric', minute: '2-digit', second: '2-digit' }).format(
    new Date(valor),
  );
}

/** Primeros 8 caracteres de un id (los GUID se reconocen por su prefijo). */
export const idCorto = (id: string) => (id.length > 8 ? id.slice(0, 8) : id);

export function textoEntidad(tipo: string, t: TFunction): string {
  return esUnoDe([...ENTIDADES_CONFIGURACION, ...ENTIDADES_ACCESO], tipo) ? t(`auditoria.entidades.${tipo}`) : tipo;
}

export function textoAccion(accion: string, t: TFunction): string {
  return esUnoDe(ACCIONES, accion) ? t(`auditoria.acciones.${accion}`) : accion;
}

/** Quién: el nombre si la API lo resolvió; si no, el tipo de actor con el id corto. */
export function textoActor(actor: ActorAuditoriaDto, t: TFunction): string {
  if (actor.nombre) return actor.nombre;
  const tipo = esUnoDe(ACTORES, actor.tipo) ? t(`auditoria.actores.${actor.tipo}`) : actor.tipo;
  return actor.id ? `${tipo} ${idCorto(actor.id)}` : tipo;
}

/** Valor del diff como texto: los textos tal cual, lo demás en JSON compacto; `null` es «vacío». */
export function textoValor(valor: unknown): string | null {
  if (valor === null || valor === undefined) return null;
  if (typeof valor === 'string') return valor;
  return JSON.stringify(valor);
}

export const COLOR_CAMBIO: Readonly<Record<TipoCambio, string>> = { Agregado: 'success.main', Modificado: 'warning.main', Eliminado: 'error.main' };
