import type { VigenciaDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { detalleComparticion } from '../boveda/versiones';
import i18n from '../i18n';
import { avisoDeVigencia, ESTADO_DEL_CHIP, estadoDeVigencia, formatearDia, requiereAtencion, textoVigencia } from './vigencia';

const t = i18n.t.bind(i18n);

const vigencia = (estado: string, cambios: Partial<VigenciaDto> = {}): VigenciaDto => ({
  vigenteHasta: '2026-12-31',
  base: 'FechaExpedicion',
  estado,
  diasAviso: [30, 7],
  diasGracia: 5,
  finDeGracia: '2027-01-05',
  vencidaEn: null,
  ...cambios,
});

describe('vigencia de un documento', () => {
  it('muestra las fechas sin hora tal cual, sin correrlas por la zona horaria', () => {
    expect(formatearDia('2026-12-31')).toBe('31/12/2026');
    expect(formatearDia('2026-11-08T15:00:00Z')).toBe('08/11/2026');
  });

  it('dice hasta cuándo sirve o cuándo venció', () => {
    expect(textoVigencia(vigencia('Vigente'), t)).toBe('Vence el 31/12/2026');
    expect(textoVigencia(vigencia('PorVencer'), t)).toBe('Vence el 31/12/2026');
    expect(textoVigencia(vigencia('EnGracia'), t)).toBe('Venció el 31/12/2026');
    expect(textoVigencia(vigencia('Vencida'), t)).toBe('Venció el 31/12/2026');
    expect(textoVigencia(vigencia('SinVencimiento', { vigenteHasta: null }), t)).toBeNull();
    expect(textoVigencia(vigencia('Vigente', { vigenteHasta: null, base: 'PeriodoNoCalculado' }), t)).toBeNull();
  });

  it('avisa qué hacer si está por vencer, en gracia o vencido', () => {
    expect(avisoDeVigencia(vigencia('Vigente'), t)).toBeNull();
    expect(avisoDeVigencia(vigencia('PorVencer'), t)).toEqual({ severidad: 'warning', texto: 'Vence pronto: sube una versión nueva antes del 31/12/2026.' });
    expect(avisoDeVigencia(vigencia('EnGracia'), t)).toEqual({ severidad: 'warning', texto: 'Ya venció. Tienes hasta el 05/01/2027 para subir una versión nueva.' });
    expect(avisoDeVigencia(vigencia('Vencida'), t)?.severidad).toBe('error');
    expect(requiereAtencion(vigencia('EnGracia'))).toBe(true);
    expect(requiereAtencion(vigencia('SinVencimiento'))).toBe(false);
  });

  it('usa estados del design system para el chip e ignora uno desconocido', () => {
    expect(ESTADO_DEL_CHIP.PorVencer).toBe('PorVencer');
    expect(ESTADO_DEL_CHIP.Vencida).toBe('Vencida');
    expect(estadoDeVigencia(vigencia('Renovando'))).toBeNull();
    expect(estadoDeVigencia(null)).toBeNull();
  });

  it('en la bóveda, cada empresa dice hasta cuándo le sirve el documento', () => {
    const empresa = { id: 'e1', nombre: 'Constructora Norte S.A.S.', logoUrl: null };
    const fila = { vinculoId: 'v1', empresa, compartido: true, version: 3, versionActualCompartida: true, compartidaEn: '2026-11-06T12:00:00Z' };
    expect(detalleComparticion({ ...fila, vigencia: vigencia('PorVencer') }, t)).toBe('Tiene esta versión desde el 06/11/2026. Vence el 31/12/2026.');
    expect(detalleComparticion({ ...fila, vigencia: null }, t)).toBe('Tiene esta versión desde el 06/11/2026.');
  });
});
