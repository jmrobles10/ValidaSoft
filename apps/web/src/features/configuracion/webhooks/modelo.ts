import type { CrearWebhookDto, EditarWebhookDto, TipoEventoPublicoDto, WebhookDto } from '@validasoft/api-client';

/**
 * Suscripciones a webhooks (docs/05 §6, NW-1…NW-8): a qué URL se envía cada evento público, de qué tipos y de qué
 * empresas. Misma validación que la API antes de guardar; la API decide (422 `url-invalida`, `url-no-permitida`,
 * `tipos-requeridos`, `empresas-fuera-de-alcance`…).
 */
export const TODOS = '*';

export interface BorradorWebhook {
  url: string;
  descripcion: string;
  /** `['*']` = todos los tipos, presentes y futuros. */
  tipos: string[];
  /** `['*']` = todas las empresas del alcance. */
  empresas: string[];
}

export type ErrorWebhook = 'url-requerida' | 'url-invalida' | 'url-no-permitida' | 'tipos-requeridos' | 'empresas-requeridas';

const HOSTS_LOCALES = new Set(['localhost', '127.0.0.1', '[::1]']);

export function borradorWebhookNuevo(): BorradorWebhook {
  return { url: '', descripcion: '', tipos: [TODOS], empresas: [TODOS] };
}

export function borradorDeWebhook(w: WebhookDto): BorradorWebhook {
  return { url: w.url, descripcion: w.descripcion ?? '', tipos: [...w.tipos], empresas: [...w.empresas] };
}

/** `https://…` (o `http` hacia localhost, que la API solo acepta en desarrollo); sin usuario ni fragmento. */
export function validarUrl(texto: string): ErrorWebhook | null {
  const limpio = texto.trim();
  if (!limpio) return 'url-requerida';
  let url: URL;
  try {
    url = new URL(limpio);
  } catch {
    return 'url-invalida';
  }
  if (url.username || url.password || url.hash) return 'url-invalida';
  if (url.protocol === 'https:') return null;
  if (url.protocol === 'http:' && HOSTS_LOCALES.has(url.hostname)) return null;
  return 'url-no-permitida';
}

export function validarWebhook(b: BorradorWebhook): ErrorWebhook[] {
  const errores: ErrorWebhook[] = [];
  const url = validarUrl(b.url);
  if (url) errores.push(url);
  if (b.tipos.length === 0) errores.push('tipos-requeridos');
  if (b.empresas.length === 0) errores.push('empresas-requeridas');
  return errores;
}

export function cuerpoCrearWebhook(b: BorradorWebhook): CrearWebhookDto {
  return { url: b.url.trim(), tipos: b.tipos, empresas: b.empresas, descripcion: b.descripcion.trim() || null };
}

export function cuerpoEditarWebhook(b: BorradorWebhook): EditarWebhookDto {
  return { url: b.url.trim(), tipos: b.tipos, empresas: b.empresas, descripcion: b.descripcion.trim() };
}

/** Tipos del catálogo agrupados por su prefijo («vinculo», «expediente», «documento», «certificado»…), en orden. */
export function agruparTipos(tipos: readonly TipoEventoPublicoDto[]): { grupo: string; tipos: TipoEventoPublicoDto[] }[] {
  const grupos = new Map<string, TipoEventoPublicoDto[]>();
  for (const t of tipos) {
    const grupo = t.tipo.split('.')[0] ?? t.tipo;
    grupos.set(grupo, [...(grupos.get(grupo) ?? []), t]);
  }
  return [...grupos].map(([grupo, lista]) => ({ grupo, tipos: lista }));
}

const GRUPOS_CONOCIDOS = ['vinculo', 'expediente', 'documento', 'certificado'] as const;
export type GrupoConocido = (typeof GRUPOS_CONOCIDOS)[number];
export const esGrupoConocido = (grupo: string): grupo is GrupoConocido => GRUPOS_CONOCIDOS.some((g) => g === grupo);

/** Marca o desmarca un tipo; con `*` marcado, desmarcar uno deja todos los demás explícitos. */
export function alternarTipo(actuales: readonly string[], tipo: string, catalogo: readonly string[]): string[] {
  const explicitos = actuales.includes(TODOS) ? [...catalogo] : [...actuales];
  return explicitos.includes(tipo) ? explicitos.filter((t) => t !== tipo) : [...explicitos, tipo];
}

/** Estado visible de una suscripción, para el chip. */
export function estadoWebhook(w: Pick<WebhookDto, 'activa' | 'fallosSeguidos' | 'causaDeshabilitacion'>): 'activa' | 'conFallos' | 'deshabilitadaPorFallos' | 'desactivada' {
  if (w.activa) return w.fallosSeguidos > 0 ? 'conFallos' : 'activa';
  return w.causaDeshabilitacion === 'fallos-consecutivos' ? 'deshabilitadaPorFallos' : 'desactivada';
}
