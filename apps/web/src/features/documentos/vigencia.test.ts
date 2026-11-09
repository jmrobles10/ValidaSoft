import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import { vigencia } from '../expedientes/datosPrueba';
import { CHIP_VIGENCIA, estadoVigencia, etiquetaVigencia, explicacionVigencia, requiereAviso, severidadAviso, textoAvisoVigencia, textoVencimiento } from './vigencia';

const t = i18n.t.bind(i18n);

describe('texto de la vigencia', () => {
  it('vigente o por vencer: «Vence el …» con el día calendario (sin correrlo por zona)', () => {
    expect(textoVencimiento(vigencia(), t)).toBe('Vence el 30/11/2026');
    expect(textoVencimiento(vigencia({ estado: 'PorVencer' }), t)).toBe('Vence el 30/11/2026');
  });

  it('en gracia o vencida: «Venció el …»', () => {
    expect(textoVencimiento(vigencia({ estado: 'EnGracia', diasGracia: 5, finDeGracia: '2026-12-05' }), t)).toBe('Venció el 30/11/2026');
    expect(textoVencimiento(vigencia({ estado: 'Vencida' }), t)).toBe('Venció el 30/11/2026');
  });

  it('sin fecha: lo dice', () => {
    expect(textoVencimiento(vigencia({ vigenteHasta: null, estado: 'SinVencimiento', base: 'SinVencimiento' }), t)).toBe('Sin fecha de vencimiento');
  });

  it('el chip dice el estado y la fecha que importa (en gracia, hasta cuándo se acepta)', () => {
    expect(etiquetaVigencia(vigencia(), t)).toBe('Vigente · 30/11/2026');
    expect(etiquetaVigencia(vigencia({ estado: 'PorVencer' }), t)).toBe('Por vencer · 30/11/2026');
    expect(etiquetaVigencia(vigencia({ estado: 'EnGracia', diasGracia: 5, finDeGracia: '2026-12-05' }), t)).toBe('En gracia · hasta el 05/12/2026');
    expect(etiquetaVigencia(vigencia({ estado: 'Vencida' }), t)).toBe('Vencida · 30/11/2026');
    expect(etiquetaVigencia(vigencia({ vigenteHasta: null, estado: 'SinVencimiento', base: 'SinVencimiento' }), t)).toBe('Sin vencimiento');
  });

  it('la base explica de dónde sale la fecha, con los días de gracia', () => {
    expect(explicacionVigencia(vigencia(), t)).toBe('Calculada desde la fecha de expedición que declaró el tercero.');
    expect(explicacionVigencia(vigencia({ base: 'FechaCarga', diasGracia: 1 }), t)).toBe(
      'Calculada desde la fecha en que el tercero cargó el documento. Tiene 1 día de gracia después del vencimiento.',
    );
    expect(explicacionVigencia(vigencia({ base: 'VencimientoPropio', diasGracia: 10 }), t)).toBe(
      'Es la fecha de vencimiento que declaró el tercero. Tiene 10 días de gracia después del vencimiento.',
    );
    expect(explicacionVigencia(vigencia({ base: 'Desconocida' }), t)).toBe('');
  });
});

describe('estado y aviso de la vigencia', () => {
  it('avisa por vencer, en gracia y vencida; no en vigente ni sin vencimiento', () => {
    expect(requiereAviso(vigencia({ estado: 'PorVencer' }))).toBe(true);
    expect(requiereAviso(vigencia({ estado: 'EnGracia' }))).toBe(true);
    expect(requiereAviso(vigencia({ estado: 'Vencida' }))).toBe(true);
    expect(requiereAviso(vigencia())).toBe(false);
    expect(requiereAviso(vigencia({ estado: 'SinVencimiento' }))).toBe(false);
    expect(textoAvisoVigencia(vigencia(), t)).toBeNull();
  });

  it('el aviso en gracia dice hasta cuándo; vencida es error', () => {
    expect(textoAvisoVigencia(vigencia({ estado: 'PorVencer' }), t)).toBe('Vence pronto: el 30/11/2026.');
    expect(textoAvisoVigencia(vigencia({ estado: 'EnGracia', diasGracia: 5, finDeGracia: '2026-12-05' }), t)).toBe(
      'Venció el 30/11/2026. Está en periodo de gracia hasta el 05/12/2026.',
    );
    expect(textoAvisoVigencia(vigencia({ estado: 'Vencida' }), t)).toBe('Venció el 30/11/2026: ya no está vigente para este vínculo.');
    expect(severidadAviso(vigencia({ estado: 'Vencida' }))).toBe('error');
    expect(severidadAviso(vigencia({ estado: 'EnGracia' }))).toBe('warning');
  });

  it('cada estado tiene su chip canónico; un estado desconocido se ignora', () => {
    expect(CHIP_VIGENCIA.PorVencer).toBe('PorVencer');
    expect(CHIP_VIGENCIA.Vencida).toBe('Vencida');
    expect(CHIP_VIGENCIA.EnGracia).not.toBe(CHIP_VIGENCIA.PorVencer);
    expect(estadoVigencia({ estado: 'Caducada' })).toBeNull();
    expect(requiereAviso({ estado: 'Caducada' })).toBe(false);
  });
});
