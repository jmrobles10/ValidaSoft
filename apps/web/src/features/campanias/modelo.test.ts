import type { CampaniaDto, FilaVistaPreviaCampaniaDto, TotalesCampaniaDto } from '@validasoft/api-client';
import i18n from 'i18next';
import { describe, expect, it } from 'vitest';
import {
  accionesDeCampania,
  alternarEstado,
  BORRADOR_INICIAL,
  cuantosAfecta,
  diasValidos,
  enCurso,
  esEstadoDeCampania,
  filasConAccion,
  filtroDeBorrador,
  filtroValido,
  formatearDia,
  hayQueHacer,
  identificacionLegible,
  paginar,
  porcentaje,
  resumenDeFiltro,
  tieneExpediente,
} from './modelo';

const t = i18n.t.bind(i18n);

const totales = (cambios: Partial<TotalesCampaniaDto> = {}): TotalesCampaniaDto => ({ filas: 10, procesadas: 0, pendientes: 10, exitosas: 0, fallidas: 0, omitidas: 0, ...cambios });
const campania = (estado: CampaniaDto['estado'], cambios: Partial<TotalesCampaniaDto> = {}): Pick<CampaniaDto, 'estado' | 'totales'> => ({ estado, totales: totales(cambios) });

describe('filtro de la campaña', () => {
  it('«vencen en N días» es un entero de 0 a 365', () => {
    for (const valido of ['0', '1', '30', '365', ' 90 ']) expect(diasValidos(valido)).toBe(true);
    for (const invalido of ['', '366', '-1', '1.5', 'abc', '1000']) expect(diasValidos(invalido)).toBe(false);
  });

  it('pide la vista previa solo con días válidos y al menos un estado', () => {
    expect(filtroValido(BORRADOR_INICIAL)).toBe(true);
    expect(filtroValido({ ...BORRADOR_INICIAL, dias: '' })).toBe(false);
    expect(filtroValido({ ...BORRADOR_INICIAL, estados: [] })).toBe(false);
  });

  it('marca y desmarca estados dejando siempre el orden canónico', () => {
    expect(alternarEstado(['Activo', 'Vencido'], 'PorVencer')).toEqual(['Activo', 'PorVencer', 'Vencido']);
    expect(alternarEstado(['Activo', 'PorVencer', 'Vencido'], 'Activo')).toEqual(['PorVencer', 'Vencido']);
    expect(alternarEstado(['Vencido'], 'Vencido')).toEqual([]);
  });

  it('envía a la API el filtro tal como está (todos los tipos = sin tipo)', () => {
    expect(filtroDeBorrador(BORRADOR_INICIAL)).toEqual({ diasParaVencer: 30, tipoVinculoId: null, estados: ['Activo', 'PorVencer', 'Vencido'] });
    expect(filtroDeBorrador({ dias: '0', tipoVinculoId: 'tv1', estados: ['Vencido'] })).toEqual({ diasParaVencer: 0, tipoVinculoId: 'tv1', estados: ['Vencido'] });
  });

  it('se lee en una línea', () => {
    expect(resumenDeFiltro({ diasParaVencer: 30, tipoVinculoId: null, estados: ['Activo', 'PorVencer', 'Vencido'] }, null, t)).toBe(
      'Vencen en 30 días · Todos los tipos de vínculo · Todos los estados',
    );
    expect(resumenDeFiltro({ diasParaVencer: 1, tipoVinculoId: 'tv1', estados: ['PorVencer', 'Vencido'] }, 'Proveedor', t)).toBe('Vencen en 1 día · Proveedor · Por vencer, Vencido');
    expect(resumenDeFiltro({ diasParaVencer: 0, tipoVinculoId: 'otro', estados: ['Vencido'] }, null, t)).toBe('Vencen hoy o ya vencieron · Un tipo de vínculo · Vencido');
  });

  it('reconoce los estados que admite una campaña', () => {
    expect(esEstadoDeCampania('PorVencer')).toBe(true);
    expect(esEstadoDeCampania('Bloqueado')).toBe(false);
  });
});

describe('avance y acciones de la campaña', () => {
  it('el avance se mide sobre todas las filas', () => {
    expect(porcentaje(totales())).toBe(0);
    expect(porcentaje(totales({ procesadas: 3, pendientes: 7 }))).toBe(30);
    expect(porcentaje(totales({ procesadas: 10, pendientes: 0 }))).toBe(100);
    expect(porcentaje(totales({ filas: 0, pendientes: 0 }))).toBe(100);
  });

  it('las acciones dependen del estado', () => {
    expect(accionesDeCampania(campania('Procesando'))).toEqual(['pausar', 'cancelar']);
    expect(accionesDeCampania(campania('Pausada'))).toEqual(['reanudar', 'cancelar']);
    expect(accionesDeCampania(campania('CompletadaConErrores', { fallidas: 2 }))).toEqual(['reintentar']);
    expect(accionesDeCampania(campania('CompletadaConErrores', { fallidas: 0 }))).toEqual([]);
    expect(accionesDeCampania(campania('Completada'))).toEqual([]);
    expect(accionesDeCampania(campania('Cancelada', { fallidas: 2 }))).toEqual([]);
    expect(enCurso({ estado: 'Procesando' })).toBe(true);
    expect(enCurso({ estado: 'Pausada' })).toBe(false);
  });

  it('la confirmación dice cuántos vínculos toca: lo pendiente, o las fallidas al reintentar', () => {
    const conPendientes = totales({ pendientes: 6, fallidas: 4 });
    expect(cuantosAfecta('pausar', conPendientes)).toBe(6);
    expect(cuantosAfecta('reanudar', conPendientes)).toBe(6);
    expect(cuantosAfecta('cancelar', conPendientes)).toBe(6);
    expect(cuantosAfecta('reintentar', conPendientes)).toBe(4);
  });
});

describe('vista previa', () => {
  const fila = (n: number, accion: string): FilaVistaPreviaCampaniaDto => ({
    n,
    vinculoId: `v${String(n)}`,
    tercero: { id: `t${String(n)}`, nombre: `Tercero ${String(n)}`, identificacion: 'CO|NIT|900' },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    estadoVinculo: 'PorVencer',
    venceEn: '2026-11-30',
    accion,
    motivo: accion === 'Omitir' ? 'Tiene un expediente abierto.' : null,
    conCorreo: true,
  });

  it('solo hay qué hacer si se abre o se recuerda alguna renovación', () => {
    expect(hayQueHacer({ abririan: 0, recordarian: 0 })).toBe(false);
    expect(hayQueHacer({ abririan: 1, recordarian: 0 })).toBe(true);
    expect(hayQueHacer({ abririan: 0, recordarian: 2 })).toBe(true);
  });

  it('filtra por lo que pasará con cada vínculo y pagina de a diez', () => {
    const filas = [fila(1, 'Abrir'), fila(2, 'Recordar'), fila(3, 'Omitir'), fila(4, 'Abrir')];
    expect(filasConAccion(filas, '').map((f) => f.n)).toEqual([1, 2, 3, 4]);
    expect(filasConAccion(filas, 'Abrir').map((f) => f.n)).toEqual([1, 4]);
    expect(filasConAccion(filas, 'Omitir').map((f) => f.n)).toEqual([3]);

    const veinticinco = Array.from({ length: 25 }, (_, i) => i);
    expect(paginar(veinticinco, 0)).toEqual({ filas: veinticinco.slice(0, 10), haySiguiente: true });
    expect(paginar(veinticinco, 2)).toEqual({ filas: veinticinco.slice(20), haySiguiente: false });
    expect(paginar(veinticinco, 1, 15).haySiguiente).toBe(false);
  });
});

describe('formatos', () => {
  it('muestra el día sin pasar por la zona horaria (la API manda solo la fecha)', () => {
    expect(formatearDia('2026-11-30')).toBe('30/11/2026');
    expect(formatearDia('2026-01-01')).toBe('01/01/2026');
    expect(formatearDia(null)).toBe('—');
    expect(formatearDia('')).toBe('—');
  });

  it('muestra la identificación sin el país', () => {
    expect(identificacionLegible('CO|NIT|900123456')).toBe('NIT 900123456');
    expect(identificacionLegible('NIT 1')).toBe('NIT 1');
  });

  it('la fila tiene expediente cuando trae su id y su folio', () => {
    expect(tieneExpediente({ expedienteId: 'e1', folio: 'NOR-1' })).toBe(true);
    expect(tieneExpediente({ expedienteId: null, folio: 'NOR-1' })).toBe(false);
    expect(tieneExpediente({ expedienteId: 'e1', folio: null })).toBe(false);
  });
});
