import { LOCALE } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { fichaMaestra } from '../../../i18n/es-CO-ficha';
import { formatearFechaDeclarada } from '../../documentos/formatoDatos';

// Cómo se lee cada dato de una sección de la ficha (docs/14 §4) en solo lectura: los datos llegan normalizados en
// snake_case (cifras como número, fechas aaaa-mm-dd, sí/no como booleano, países ISO, catálogos por código).

const CIFRAS = new Set(['activos', 'pasivos', 'patrimonio', 'ingresos_brutos', 'ingresos_netos', 'gastos_operacionales', 'gastos_no_operacionales', 'otros_ingresos']);
const PAISES = new Set(['pais', 'nacionalidad', 'residencia', 'paises']);
const SIN_SEPARADOR = new Set(['anio_inicio', 'periodo']);

const pesos = new Intl.NumberFormat(LOCALE, { style: 'currency', currency: 'COP', maximumFractionDigits: 2 });
const numero = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 4 });

function nombresDePaises(): (codigo: string) => string {
  try {
    const nombres = new Intl.DisplayNames(['es'], { type: 'region' });
    return (codigo) => {
      const nombre = nombres.of(codigo);
      return nombre && nombre !== codigo ? `${nombre} (${codigo})` : codigo;
    };
  } catch {
    return (codigo) => codigo;
  }
}

const nombrePais = nombresDePaises();

const ROLES = ['RepresentanteLegal', 'Suplente', 'Apoderado', 'Socio', 'JuntaDirectiva', 'RevisorFiscal', 'Contador'] as const;
const OPERACIONES = ['Importaciones', 'Exportaciones', 'Inversiones', 'Prestamos', 'Giros', 'PagoDeServicios', 'Otras'] as const;
const TIPOS_PERSONA = ['Natural', 'Juridica', 'Consorcio'] as const;

const esDe = <T extends string>(lista: readonly T[], valor: string): valor is T => lista.some((x) => x === valor);

/** Etiqueta legible de un campo; una clave nueva del esquema se muestra tal cual. */
export function etiquetaCampo(clave: string, t: TFunction): string {
  return clave in fichaMaestra.campos ? t(`fichaMaestra.campos.${clave as keyof typeof fichaMaestra.campos}`) : clave;
}

/** Un valor simple (texto, número, sí/no) o una lista de textos, ya listo para leer. */
export function formatearValor(clave: string, valor: unknown, t: TFunction): string {
  if (valor === null || valor === undefined) return '—';
  if (typeof valor === 'boolean') return valor ? t('fichaMaestra.si') : t('fichaMaestra.no');
  if (typeof valor === 'number') {
    if (CIFRAS.has(clave)) return pesos.format(valor);
    if (clave === 'porcentaje') return `${numero.format(valor)} %`;
    if (clave === 'tarifa_por_mil') return `${numero.format(valor)} ‰`;
    return SIN_SEPARADOR.has(clave) ? String(valor) : numero.format(valor);
  }
  if (typeof valor === 'string') {
    if (clave.startsWith('fecha')) return formatearFechaDeclarada(valor);
    if (PAISES.has(clave)) return nombrePais(valor);
    if (clave === 'rol' && esDe(ROLES, valor)) return t(`fichaMaestra.roles.${valor}`);
    if (clave === 'tipo_persona' && esDe(TIPOS_PERSONA, valor)) return t(`terceros.ficha.tiposPersona.${valor}`);
    return valor;
  }
  if (Array.isArray(valor)) {
    return valor
      .map((v) => {
        if (typeof v !== 'string') return formatearValor(clave, v, t);
        if (clave === 'tipos' && esDe(OPERACIONES, v)) return t(`fichaMaestra.operaciones.${v}`);
        return PAISES.has(clave) ? nombrePais(v) : v;
      })
      .join(', ');
  }
  return JSON.stringify(valor);
}

export const esObjeto = (valor: unknown): valor is Record<string, unknown> => typeof valor === 'object' && valor !== null && !Array.isArray(valor);

/** Lista de filas (personas, ICA): un arreglo de objetos. */
export const esListaDeObjetos = (valor: unknown): valor is Record<string, unknown>[] => Array.isArray(valor) && valor.length > 0 && valor.every(esObjeto);

/** Título de una fila: la persona por su nombre y rol; lo demás, por su posición. */
export function tituloDeFila(clave: string, fila: Record<string, unknown>, indice: number, t: TFunction): string {
  if (clave === 'personas') {
    const nombre = [fila.nombres, fila.apellidos].filter((v): v is string => typeof v === 'string' && v.length > 0).join(' ');
    const rol = typeof fila.rol === 'string' ? formatearValor('rol', fila.rol, t) : null;
    return [nombre || null, rol].filter(Boolean).join(' · ') || t('fichaMaestra.fila', { n: String(indice + 1) });
  }
  return t('fichaMaestra.fila', { n: String(indice + 1) });
}
