import type { ActualizarDominioDeCorreoDto, ComprobacionDeRegistroDnsDto, DominioDeCorreoDto } from '@validasoft/api-client';
import type { Estado } from '@validasoft/ui';

export const LONGITUD_MAXIMA_DOMINIO = 253;
export const LONGITUD_MAXIMA_NOMBRE = 80;
export const PARTE_LOCAL_POR_DEFECTO = 'no-responder';

/** Un nombre de dominio público: etiquetas de letras, números y guiones separadas por puntos, con al menos un punto (lo mismo que valida el servidor). */
const DOMINIO = /^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$/;
const PARTE_LOCAL = /^[a-z0-9]([a-z0-9._+-]{0,62}[a-z0-9])?$/;

export interface BorradorDominio {
  dominio: string;
  parteLocal: string;
  nombre: string;
}

export type ErrorDominio = 'dominio-invalido' | 'parte-local-invalida' | 'nombre-invalido';

/** El dominio como lo guarda el servidor: sin espacios, sin `@` inicial ni punto final y en minúsculas. */
export function normalizarDominio(texto: string): string {
  return texto.trim().replace(/^@+/, '').replace(/\.+$/, '').toLowerCase();
}

export function borradorDesdeDominio(d: DominioDeCorreoDto): BorradorDominio {
  return { dominio: d.dominio ?? '', parteLocal: d.configurado ? d.parteLocal : '', nombre: d.nombreRemitente ?? '' };
}

export function validarDominio(b: BorradorDominio): ErrorDominio[] {
  const errores: ErrorDominio[] = [];
  if (!DOMINIO.test(normalizarDominio(b.dominio))) errores.push('dominio-invalido');
  const parte = b.parteLocal.trim().toLowerCase();
  if (parte !== '' && !PARTE_LOCAL.test(parte)) errores.push('parte-local-invalida');
  const nombre = b.nombre.trim();
  if (nombre.length > LONGITUD_MAXIMA_NOMBRE || /[<>]/.test(nombre)) errores.push('nombre-invalido');
  return errores;
}

export function cuerpoDominio(b: BorradorDominio): ActualizarDominioDeCorreoDto {
  return { dominio: normalizarDominio(b.dominio), parteLocal: b.parteLocal.trim().toLowerCase() || null, nombreRemitente: b.nombre.trim() || null };
}

/** ¿El borrador difiere de lo guardado? El botón de guardar solo se habilita con cambios. */
export function hayCambiosDeDominio(b: BorradorDominio, d: DominioDeCorreoDto): boolean {
  const base = borradorDesdeDominio(d);
  return normalizarDominio(b.dominio) !== base.dominio || (b.parteLocal.trim().toLowerCase() || PARTE_LOCAL_POR_DEFECTO) !== (base.parteLocal || PARTE_LOCAL_POR_DEFECTO) || b.nombre.trim() !== base.nombre;
}

/** La dirección que se verá, con lo que va escrito (aún sin guardar). */
export function direccionDeVistaPrevia(b: BorradorDominio): string | null {
  const dominio = normalizarDominio(b.dominio);
  return DOMINIO.test(dominio) ? `${b.parteLocal.trim().toLowerCase() || PARTE_LOCAL_POR_DEFECTO}@${dominio}` : null;
}

export type EstadoDelDominio = 'sin-dominio' | 'Pendiente' | 'Verificado' | 'Fallido';

export function estadoDelDominio(d: Pick<DominioDeCorreoDto, 'configurado' | 'estado'>): EstadoDelDominio {
  if (!d.configurado) return 'sin-dominio';
  return d.estado === 'Verificado' || d.estado === 'Fallido' ? d.estado : 'Pendiente';
}

export function chipDeDominio(estado: Exclude<EstadoDelDominio, 'sin-dominio'>): Estado {
  return estado === 'Verificado' ? 'Aprobado' : estado === 'Fallido' ? 'Rechazado' : 'Pendiente';
}

/** Lo que halló la verificación para un registro, por su finalidad (`Spf`, `Dkim`, `Dmarc`). */
export function comprobacionDe(d: Pick<DominioDeCorreoDto, 'verificacion'>, finalidad: string): ComprobacionDeRegistroDnsDto | undefined {
  return d.verificacion.find((c) => c.finalidad === finalidad);
}

/** Cuántos registros faltan por quedar bien (todos si aún no se verificó). */
export function registrosPendientes(d: Pick<DominioDeCorreoDto, 'registros' | 'verificacion'>): number {
  return d.registros.filter((r) => comprobacionDe(d, r.finalidad)?.correcto !== true).length;
}

export const FINALIDADES_DNS = ['Spf', 'Dkim', 'Dmarc'] as const;
export type FinalidadDns = (typeof FINALIDADES_DNS)[number];

export function comoFinalidad(valor: string): FinalidadDns | null {
  return (FINALIDADES_DNS as readonly string[]).includes(valor) ? (valor as FinalidadDns) : null;
}
