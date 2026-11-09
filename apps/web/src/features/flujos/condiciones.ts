import type { CondicionDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';

/**
 * «Se pide a» de un requisito (RQ-12). El editor ofrece los casos de uso diario como atajos (siempre, solo a personas
 * jurídicas, solo a naturales); una condición armada por la API (país, atributos del vínculo, combinaciones) se muestra
 * descrita y se conserva tal cual al guardar el resto del flujo.
 */
export type AtajoCondicion = 'siempre' | 'juridica' | 'natural' | 'personalizada';

const TIPO_PERSONA = 'tercero.tipoPersona';

function soloTipoPersona(valor: 'Juridica' | 'Natural'): CondicionDto {
  return { tipo: 'Predicado', nodos: null, atributo: TIPO_PERSONA, operador: 'Igual', valores: [valor] };
}

/** Atajo que corresponde a la condición (sin distinguir mayúsculas, como la API al normalizar). */
export function atajoDe(condicion: CondicionDto | null | undefined): AtajoCondicion {
  if (!condicion) return 'siempre';
  const { tipo, atributo, operador, valores } = condicion;
  if (tipo === 'Y' && (condicion.nodos?.length ?? 0) === 0) return 'siempre';
  if (tipo !== 'Predicado' || atributo !== TIPO_PERSONA || operador !== 'Igual' || valores?.length !== 1) return 'personalizada';
  const valor = (valores[0] ?? '').toLowerCase();
  return valor === 'juridica' ? 'juridica' : valor === 'natural' ? 'natural' : 'personalizada';
}

/** Condición de un atajo; `personalizada` conserva la que había. */
export function condicionDe(atajo: AtajoCondicion, actual: CondicionDto | null): CondicionDto | null {
  switch (atajo) {
    case 'siempre':
      return null;
    case 'juridica':
      return soloTipoPersona('Juridica');
    case 'natural':
      return soloTipoPersona('Natural');
    case 'personalizada':
      return actual;
  }
}

/** Texto legible de una condición, p. ej. «tipo de persona es Jurídica y país en CO, PE». */
export function describirCondicion(condicion: CondicionDto, t: TFunction): string {
  const nodos = condicion.nodos ?? [];
  const grupo = (separador: string) => nodos.map((n) => (n.nodos?.length ? `(${describirCondicion(n, t)})` : describirCondicion(n, t))).join(separador);
  switch (condicion.tipo) {
    case 'Y':
      return grupo(` ${t('flujos.condicion.y')} `);
    case 'O':
      return grupo(` ${t('flujos.condicion.o')} `);
    case 'No':
      return `${t('flujos.condicion.no')} (${grupo('')})`;
    default: {
      const atributo = condicion.atributo ?? '';
      const nombre = atributo.startsWith('vinculo.atributos.')
        ? atributo.slice('vinculo.atributos.'.length)
        : t(`flujos.condicion.atributos.${atributo}`, { defaultValue: atributo });
      const valores = (condicion.valores ?? []).map((v) => (atributo === TIPO_PERSONA ? t(`flujos.condicion.personas.${v}`, { defaultValue: v }) : v));
      const operador = t(`flujos.condicion.operadores.${condicion.operador ?? 'Igual'}`, { defaultValue: condicion.operador ?? '' });
      return [nombre, operador, valores.join(', ')].filter(Boolean).join(' ');
    }
  }
}
