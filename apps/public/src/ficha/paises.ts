import { sinTildes } from './api';

/** Códigos ISO 3166-1 alfa-2 (los que acepta la ficha para país, nacionalidad, residencia y moneda extranjera). */
const CODIGOS =
  'AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ CA CC CD CF CG CH CI CK CL CM CN CO CR ' +
  'CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT ' +
  'HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO ' +
  'MP MQ MR MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW SA SB SC SD SE ' +
  'SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ' +
  'ZA ZM ZW';

export interface Opcion {
  codigo: string;
  nombre: string;
}

function nombresDePaises(): (codigo: string) => string {
  try {
    const nombres = new Intl.DisplayNames(['es'], { type: 'region' });
    return (codigo) => nombres.of(codigo) ?? codigo;
  } catch {
    return (codigo) => codigo;
  }
}

export const nombrePais = nombresDePaises();

/** Colombia primero (es el caso común) y el resto por nombre. */
export const PAISES: readonly Opcion[] = CODIGOS.split(' ')
  .map((codigo) => ({ codigo, nombre: nombrePais(codigo) }))
  .sort((a, b) => (a.codigo === 'CO' ? -1 : b.codigo === 'CO' ? 1 : a.nombre.localeCompare(b.nombre, 'es')));

/** Filtro de opciones sin tildes, por nombre o código. */
export function filtrarOpciones<T extends Opcion>(opciones: readonly T[], texto: string, maximo = 50): T[] {
  const buscado = sinTildes(texto.trim());
  if (!buscado) return opciones.slice(0, maximo);
  return opciones.filter((o) => sinTildes(o.nombre).includes(buscado) || o.codigo.toLowerCase().startsWith(buscado)).slice(0, maximo);
}
