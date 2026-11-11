import { describe, expect, it } from 'vitest';
import { cuantasPestanasCaben, EXTRA_BOTON_MAS, HOLGURA_PESTANAS, SEPARACION_PESTANAS } from './pestanasAdaptativas';

const ANCHOS = [80, 90, 100, 110, 120];
const MAS = 60;
const SEP = SEPARACION_PESTANAS;
/** Ancho de las `n` primeras pestañas con sus separaciones. */
const ancho = (n: number) => ANCHOS.slice(0, n).reduce((suma, a) => suma + a, 0) + SEP * Math.max(0, n - 1);

function caben(disponible: number, indiceActiva: number | null = null, anchos: readonly number[] = ANCHOS, anchoMas = MAS) {
  return cuantasPestanasCaben({ anchos, anchoMas, disponible, indiceActiva });
}

describe('cuantasPestanasCaben', () => {
  it('sin pestañas no hay nada que mostrar', () => {
    expect(caben(500, null, [])).toBe(0);
  });

  it('si caben todas se muestran todas, sin reservar el botón «Más»', () => {
    expect(caben(ancho(5) + HOLGURA_PESTANAS)).toBe(5);
    expect(caben(5000)).toBe(5);
  });

  it('un píxel menos de lo que necesitan todas y la última pasa a «Más»', () => {
    expect(caben(ancho(5) + HOLGURA_PESTANAS - 1)).toBe(4);
  });

  it('reserva el botón «Más» con su separación y las que no caben pasan al menú, en orden', () => {
    // 3 pestañas + separación + «Más» justo.
    expect(caben(ancho(3) + SEP + MAS + HOLGURA_PESTANAS)).toBe(3);
    expect(caben(ancho(3) + SEP + MAS + HOLGURA_PESTANAS - 1)).toBe(2);
    expect(caben(ancho(2) + SEP + MAS + HOLGURA_PESTANAS)).toBe(2);
  });

  it('si ni una pestaña cabe junto al botón, solo se ve «Más»', () => {
    expect(caben(ANCHOS[0]! + SEP + MAS)).toBe(0);
    expect(caben(0)).toBe(0);
  });

  it('con la activa visible no se reserva más ancho que el del botón normal', () => {
    const disponible = ancho(3) + SEP + MAS + HOLGURA_PESTANAS;
    expect(caben(disponible, 0)).toBe(3);
    expect(caben(disponible, 2)).toBe(3);
  });

  it('con la activa en «Más» el botón lleva su nombre y puede dejar fuera una pestaña más', () => {
    const disponible = ancho(3) + SEP + MAS + HOLGURA_PESTANAS;
    // Activa = la quinta (120 px): el botón pasa a medir 120 + ícono, más ancho que «Más».
    expect(ANCHOS[4]! + EXTRA_BOTON_MAS).toBeGreaterThan(MAS);
    expect(caben(disponible, 4)).toBe(2);
  });

  it('con la activa en «Más» pero un nombre corto, el botón no es más angosto que «Más»', () => {
    const anchos = [200, 200, 20];
    expect(caben(200 + SEP + MAS + HOLGURA_PESTANAS, 2, anchos)).toBe(1);
  });

  it('cuando caben todas, la activa nunca estorba', () => {
    expect(caben(5000, 4)).toBe(5);
  });

  it('crece sin saltos: más ancho nunca muestra menos pestañas (con o sin activa en «Más»)', () => {
    for (const indiceActiva of [null, 0, 2, 4]) {
      let anterior = 0;
      for (let disponible = 0; disponible <= 700; disponible++) {
        const n = caben(disponible, indiceActiva);
        expect(n).toBeGreaterThanOrEqual(anterior);
        anterior = n;
      }
    }
  });

  it('lo que se muestra más el botón (con el nombre de la activa) siempre cabe, salvo cuando ni una cabe', () => {
    for (const indiceActiva of [null, 1, 4]) {
      for (let disponible = 200; disponible <= 700; disponible++) {
        const n = caben(disponible, indiceActiva);
        if (n === ANCHOS.length) continue;
        const botonConNombre = indiceActiva !== null && indiceActiva >= n ? Math.max(MAS, ANCHOS[indiceActiva]! + EXTRA_BOTON_MAS) : MAS;
        expect(ancho(n) + (n > 0 ? SEP : 0) + botonConNombre).toBeLessThanOrEqual(disponible - HOLGURA_PESTANAS);
      }
    }
  });
});
