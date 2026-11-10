import type { TFunction } from 'i18next';
import { camposFicha } from '../i18n/es-CO-ficha-empresas';

const esCampoConocido = (clave: string): clave is keyof typeof camposFicha => clave in camposFicha;

/**
 * Etiqueta legible de un campo que señaló una empresa (ruta snake_case): `razon_social` ⇒ «Razón social»;
 * `personas[1].numero` ⇒ «Personas relacionadas 2 › numero» (lo que no tiene etiqueta se muestra tal cual).
 */
export function etiquetaDeCampo(ruta: string, t: TFunction): string {
  return ruta
    .split('.')
    .map((parte) => {
      const [, clave = parte, indice] = /^([^[]+)(?:\[(\d+)\])?$/.exec(parte) ?? [];
      const etiqueta = esCampoConocido(clave) ? t(`ficha.campos.${clave}`) : clave;
      return indice === undefined ? etiqueta : `${etiqueta} ${String(Number(indice) + 1)}`;
    })
    .join(' › ');
}

/** ¿La ruta de un campo del formulario cae dentro de alguno de los campos señalados? (`personas[0].pep` en `personas`). */
export function estaSenalado(ruta: string, senalados: readonly string[]): boolean {
  return senalados.some((c) => ruta === c || ruta.startsWith(`${c}.`) || ruta.startsWith(`${c}[`));
}
