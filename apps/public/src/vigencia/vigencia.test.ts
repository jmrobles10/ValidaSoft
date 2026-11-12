import type { VigenciaDto } from '@validasoft/api-client';
import { formatearDiaCivil } from '@validasoft/ui';
import { describe, expect, it } from 'vitest';
import { detalleComparticion } from '../boveda/versiones';
import i18n from '../i18n';
import { avisoDeVigencia, ESTADO_DEL_CHIP, estadoDeVigencia, requiereAtencion, textoVigencia } from './vigencia';

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
    expect(formatearDiaCivil('2026-12-31')).toBe('31/12/2026');
    expect(formatearDiaCivil('2026-11-08T15:00:00Z')).toBe('08/11/2026');
    // El texto que ve el tercero usa ese mismo día, no el anterior.
    const vigencia: VigenciaDto = { vigenteHasta: '2026-11-26', base: 'FechaCarga', estado: 'Vigente', diasAviso: [], diasGracia: 0, finDeGracia: null, vencidaEn: null };
    expect(textoVigencia(vigencia, t)).toContain('26/11/2026');
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

  it('en la bóveda, cada empresa dice hasta cuándo le sirve el documento solo si difiere de la cabecera, y por qué', () => {
    const empresa = { id: 'e1', nombre: 'Constructora Norte S.A.S.', logoUrl: null };
    const fila = { vinculoId: 'v1', empresa, compartido: true, version: 3, versionActualCompartida: true, compartidaEn: '2026-11-06T12:00:00Z', situacion: 'TieneVigente', otraVersion: null };
    expect(detalleComparticion({ ...fila, vigencia: vigencia('PorVencer') }, t)).toBe(
      'Tiene la versión 3, la vigente. Vence el 31/12/2026: su regla de vigencia cuenta desde la fecha de expedición.',
    );
    // Si es la misma fecha de la cabecera, no se repite.
    expect(detalleComparticion({ ...fila, vigencia: vigencia('PorVencer') }, t, '2026-12-31')).toBe('Tiene la versión 3, la vigente.');
    expect(detalleComparticion({ ...fila, vigencia: null }, t)).toBe('Tiene la versión 3, la vigente.');
  });
});
