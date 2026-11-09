import { EstadoVinculo, type EmpresaDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';

const TIPOS_PERSONA = ['Natural', 'Juridica', 'Consorcio'] as const;
type TipoPersona = (typeof TIPOS_PERSONA)[number];
const esTipoPersona = (valor: string): valor is TipoPersona => TIPOS_PERSONA.some((tipo) => tipo === valor);

/** «Persona jurídica», «Persona natural»…; un tipo nuevo de la API se muestra tal cual. */
export const textoTipoPersona = (tipo: string, t: TFunction) => (esTipoPersona(tipo) ? t(`terceros.ficha.tiposPersona.${tipo}`) : tipo);

const ESTADOS_VINCULO: readonly string[] = Object.values(EstadoVinculo);
/** El estado de un vínculo en otra empresa llega como texto: se estrecha para pintarlo con el chip canónico. */
export const esEstadoVinculo = (valor: string): valor is EstadoVinculo => ESTADOS_VINCULO.includes(valor);

/**
 * Nombre de otra empresa del grupo: solo si la persona tiene acceso a ella (está entre las empresas de su sesión);
 * de lo contrario la ficha no lo revela y se muestra un texto genérico (`null`).
 */
export function nombreEmpresaVisible(empresaId: string, empresas: readonly Pick<EmpresaDto, 'id' | 'nombre'>[]): string | null {
  return empresas.find((e) => e.id.toLowerCase() === empresaId.toLowerCase())?.nombre ?? null;
}
