import { esErrorApi, type ConsentimientoDto, type InvitacionPublicaDto, type TextoLegalPublicoDto, type TipoTextoLegal } from '@validasoft/api-client';

/** Orden de lectura: primero el aviso de la plataforma (solo si falta), luego la autorización y los términos de la empresa. */
const ORDEN: Record<TipoTextoLegal, number> = { AvisoPlataforma: 0, AutorizacionEmpresa: 1, TerminosEmpresa: 2 };

/** Identifica un texto por tipo y versión: si se publica otra versión mientras la persona lee, es otro texto. */
export function claveTexto(texto: Pick<TextoLegalPublicoDto, 'tipo' | 'textoVersion'>): string {
  return `${texto.tipo}:${String(texto.textoVersion)}`;
}

export function textosOrdenados(invitacion: InvitacionPublicaDto): TextoLegalPublicoDto[] {
  return [...invitacion.textosPorAceptar].sort((a, b) => ORDEN[a.tipo] - ORDEN[b.tipo]);
}

/** Qué ve la persona: ya aceptó, la empresa aún no publica sus textos, o tiene textos por aceptar. */
export type EstadoInvitacion = 'aceptada' | 'faltan-textos' | 'por-aceptar';

export function estadoInvitacion(invitacion: InvitacionPublicaDto): EstadoInvitacion {
  if (invitacion.vinculo.invitacionAceptada) return 'aceptada';
  if (!invitacion.puedeAceptar || invitacion.textosFaltantes.length > 0) return 'faltan-textos';
  return 'por-aceptar';
}

/** Aceptar exige marcar cada texto vigente (no hay "aceptar todo" implícito). */
export function puedeAceptar(invitacion: InvitacionPublicaDto, marcados: ReadonlySet<string>): boolean {
  return estadoInvitacion(invitacion) === 'por-aceptar' && invitacion.textosPorAceptar.every((t) => marcados.has(claveTexto(t)));
}

/** Cuerpo de `POST /invitaciones/{id}/aceptar`: cada texto con la versión exacta que se mostró. */
export function consentimientosDe(invitacion: InvitacionPublicaDto, marcados: ReadonlySet<string>): ConsentimientoDto[] {
  return textosOrdenados(invitacion).map((t) => ({ tipo: t.tipo, textoVersion: t.textoVersion, aceptado: marcados.has(claveTexto(t)) }));
}

/**
 * Deja marcados solo los textos que siguen vigentes con la misma versión: tras un `texto-desactualizado` la invitación
 * se recarga y el texto que cambió vuelve a quedar sin marcar (hay que leerlo de nuevo).
 */
export function marcadosVigentes(invitacion: InvitacionPublicaDto, marcados: ReadonlySet<string>): Set<string> {
  const vigentes = new Set(invitacion.textosPorAceptar.map(claveTexto));
  return new Set([...marcados].filter((clave) => vigentes.has(clave)));
}

/** Errores de aceptar con tratamiento propio en la pantalla. */
export type FalloAceptacion = 'texto-desactualizado' | 'textos-legales-faltantes' | 'otp-requerido' | 'ya-aceptada' | 'otro';

export function falloDeAceptacion(error: unknown): FalloAceptacion {
  if (!esErrorApi(error)) return 'otro';
  switch (error.codigo) {
    case 'texto-desactualizado':
      return 'texto-desactualizado';
    case 'textos-legales-faltantes':
      return 'textos-legales-faltantes';
    case 'otp-requerido':
      return 'otp-requerido';
    case 'invitacion-ya-aceptada':
      return 'ya-aceptada';
    default:
      return 'otro';
  }
}
