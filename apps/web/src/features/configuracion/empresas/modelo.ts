import type { CrearEmpresaDto, IdentificacionDto, TextoLegalDto, TextosLegalesEmpresaDto, TipoIdentificacionDto } from '@validasoft/api-client';

/**
 * Empresas del grupo (ADR-0008) y sus textos legales. Misma validación que el dominio (`Empresa.Crear`,
 * `Identificacion.Crear`, `TextoLegal.Publicar`) para avisar antes de enviar; la API decide (422
 * `prefijo-folio-invalido`, `numero-identificacion-invalido`, `digito-verificacion-invalido`; 409 `empresa-duplicada`,
 * `prefijo-folio-duplicado`).
 */

export const PAIS_POR_DEFECTO = 'CO';
export const TIPO_POR_DEFECTO = 'NIT';
/** `TextoLegal.LongitudMaxima`. */
export const LONGITUD_MAXIMA_TEXTO = 100_000;

export interface BorradorEmpresa {
  pais: string;
  tipo: string;
  numero: string;
  nombre: string;
  prefijo: string;
}

export type ErrorEmpresa = 'tipo-requerido' | 'numero-requerido' | 'numero-invalido' | 'nombre-requerido' | 'prefijo-invalido';

export function borradorEmpresaNuevo(): BorradorEmpresa {
  return { pais: PAIS_POR_DEFECTO, tipo: TIPO_POR_DEFECTO, numero: '', nombre: '', prefijo: '' };
}

/** Como el dominio: mayúsculas, sin espacios; 2 a 10 letras o números. */
export function normalizarPrefijo(texto: string): string {
  return texto.trim().toUpperCase();
}

export function prefijoValido(prefijo: string): boolean {
  return /^[A-Z0-9]{2,10}$/.test(normalizarPrefijo(prefijo));
}

/**
 * Número sin puntos, espacios ni guiones (el dígito de verificación tras el último guion se descarta si el tipo lo
 * tiene: la API lo recalcula y lo compara).
 */
export function numeroNormalizado(numero: string, tipo: Pick<TipoIdentificacionDto, 'tieneDv'> | undefined): string {
  let crudo = numero.trim().toUpperCase();
  if (tipo?.tieneDv && crudo.includes('-')) crudo = crudo.slice(0, crudo.lastIndexOf('-'));
  return crudo.replace(/[\s.-]/g, '');
}

/** ¿El número cumple el patrón del tipo? Si el patrón no se puede leer en el navegador, decide la API. */
export function numeroValido(numero: string, tipo: TipoIdentificacionDto | undefined): boolean {
  if (!tipo) return true;
  try {
    return new RegExp(tipo.patron).test(numeroNormalizado(numero, tipo));
  } catch {
    return true;
  }
}

export function validarEmpresa(b: BorradorEmpresa, tipo: TipoIdentificacionDto | undefined): ErrorEmpresa[] {
  const errores: ErrorEmpresa[] = [];
  if (!b.tipo) errores.push('tipo-requerido');
  if (!b.numero.trim()) errores.push('numero-requerido');
  else if (!numeroValido(b.numero, tipo)) errores.push('numero-invalido');
  if (!b.nombre.trim()) errores.push('nombre-requerido');
  if (!prefijoValido(b.prefijo)) errores.push('prefijo-invalido');
  return errores;
}

export function cuerpoCrearEmpresa(b: BorradorEmpresa): CrearEmpresaDto {
  return {
    identificacion: { pais: b.pais, tipo: b.tipo, numero: b.numero.trim(), digitoVerificacion: null },
    nombre: b.nombre.trim(),
    prefijoFolio: normalizarPrefijo(b.prefijo),
    logoUrl: null,
    replyTo: null,
  };
}

/** «NIT 900123456-7». */
export function identificacionVisible(i: IdentificacionDto): string {
  return `${i.tipo} ${i.digitoVerificacion ? `${i.numero}-${i.digitoVerificacion}` : i.numero}`;
}

/** Ejemplo de folio con el prefijo («NORTE-E-2026-000123»). */
export function ejemploFolio(prefijo: string, anio: number): string {
  return `${normalizarPrefijo(prefijo) || 'PREFIJO'}-E-${String(anio)}-000123`;
}

/** Textos legales de una empresa: alias de la ruta (`PUT …/textos-legales/{tipo}`) → nombre del enum en la API. */
export const TIPOS_TEXTO = [
  { ruta: 'autorizacion', tipo: 'AutorizacionEmpresa' },
  { ruta: 'terminos', tipo: 'TerminosEmpresa' },
] as const;
export type RutaTexto = (typeof TIPOS_TEXTO)[number]['ruta'];

export function vigenteDe(textos: TextosLegalesEmpresaDto | undefined, ruta: RutaTexto): TextoLegalDto | null {
  const tipo = TIPOS_TEXTO.find((x) => x.ruta === ruta)?.tipo;
  return textos?.vigentes.find((v) => v.tipo === tipo) ?? null;
}

export type ErrorTexto = 'contenido-requerido' | 'demasiado-largo' | 'sin-cambios';

/** Lo que el dominio rechaza (vacío, más de 100 000 caracteres) y publicar el mismo texto que ya está vigente. */
export function validarTexto(contenido: string, vigente: TextoLegalDto | null): ErrorTexto | null {
  const texto = contenido.trim();
  if (!texto) return 'contenido-requerido';
  if (texto.length > LONGITUD_MAXIMA_TEXTO) return 'demasiado-largo';
  if (vigente?.contenido.trim() === texto) return 'sin-cambios';
  return null;
}
