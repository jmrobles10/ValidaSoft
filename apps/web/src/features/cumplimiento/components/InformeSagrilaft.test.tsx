import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { filaSagrilaft, informeSagrilaft } from '../datosPrueba';
import { renderizar, simularApi, type LlamadaApi } from '../pruebasApi';
import { InformeCumplimiento } from './InformeCumplimiento';

const INFORME = '/api/v1/informes/sagrilaft';

function montar(manejadores: Record<string, (llamada: LlamadaApi) => unknown> = {}): LlamadaApi[] {
  const llamadas = simularApi({ [`GET ${INFORME}`]: () => informeSagrilaft(), ...manejadores });
  renderizar(<InformeCumplimiento empresaId="emp" />);
  return llamadas;
}

afterEach(() => {
  reiniciarCliente();
  vi.restoreAllMocks();
});

describe('Informe SAGRILAFT completo (CU-19)', () => {
  it('mientras carga muestra el esqueleto con su forma, no un texto «Cargando…»', async () => {
    montar();
    expect(screen.getByRole('status', { name: 'Cargando el informe' })).toBeInTheDocument();
    expect(screen.queryByText(/Cargando…/)).not.toBeInTheDocument();
    await screen.findByText('Terceros');
    expect(screen.queryByRole('status', { name: 'Cargando el informe' })).not.toBeInTheDocument();
  });

  it('muestra las tarjetas, la distribución del riesgo, los tiempos y la tendencia de 12 meses', async () => {
    const llamadas = montar();
    await screen.findByText('Estudios vencidos');
    const tarjeta = (clave: string) => document.querySelector(`[data-tarjeta="${clave}"]`);
    expect(tarjeta('terceros')).toHaveTextContent('Terceros3');
    expect(tarjeta('conEstudioVigente')).toHaveTextContent('Con estudio vigente1');
    expect(tarjeta('estudiosVencidos')).toHaveTextContent('Estudios vencidos1');
    expect(tarjeta('sinEstudio')).toHaveTextContent('Sin estudio1');
    expect(tarjeta('listasPorVencer')).toHaveTextContent('Listas por vencer1');
    expect(tarjeta('listasVencidas')).toHaveTextContent('Listas vencidas0');
    expect(llamadas[0]?.url.searchParams.get('hasta')).toMatch(/^\d{4}-\d{2}-\d{2}$/);

    const riesgo = within(screen.getByRole('region', { name: 'Distribución del riesgo' }));
    expect(riesgo.getByRole('listitem', { name: '1 · Bajo: 0' })).toBeInTheDocument();
    expect(riesgo.getByRole('listitem', { name: '2 · Medio: 1' })).toBeInTheDocument();
    expect(riesgo.getByRole('listitem', { name: '3 · Alto: 1' })).toBeInTheDocument();

    const tiempos = within(screen.getByRole('region', { name: 'Tiempos de cumplimiento' }));
    expect(tiempos.getByText('3,5 días')).toBeInTheDocument();
    expect(tiempos.getByText('Con más de 7 días')).toBeInTheDocument();

    // La tendencia es decorativa: el dato va en una tabla para lectores de pantalla.
    const tendencia = within(screen.getByRole('table', { name: 'Últimos 12 meses' }));
    expect(tendencia.getAllByRole('row')).toHaveLength(4);
    expect(tendencia.getByRole('row', { name: /oct.* 2026 3 4/ })).toBeInTheDocument();
  });

  it('la tabla por tercero dice el estado de las listas y del estudio, el riesgo y cuándo toca la revisión', async () => {
    montar();
    const tabla = within(await screen.findByRole('table', { name: 'Informe SAGRILAFT' }));
    const vigente = within(tabla.getByRole('row', { name: /Aceros del Norte/ }));
    expect(vigente.getAllByText('Vigente')).toHaveLength(2);
    expect(vigente.getByText('2 · Medio')).toBeInTheDocument();
    expect(vigente.getByText('Ordinaria')).toBeInTheDocument();
    expect(vigente.getByText('Vence en 325 días')).toBeInTheDocument();

    const vencida = within(tabla.getByRole('row', { name: /Ferretería Roja/ }));
    expect(vencida.getByText('Vencido')).toBeInTheDocument();
    expect(vencida.getByText('Intensificada')).toBeInTheDocument();
    expect(vencida.getByText('Venció hace 10 días')).toBeInTheDocument();
    expect(vencida.getByText('1 coincidencia por analizar')).toBeInTheDocument();
    expect(vencida.getByText('1 PEP')).toBeInTheDocument();

    const sin = within(tabla.getByRole('row', { name: /Sin Estudio Ltda/ }));
    expect(sin.getByText('Sin estudio')).toBeInTheDocument();
    expect(sin.getByText('Sin consulta')).toBeInTheDocument();
    expect(sin.getByText('Sin estudio vigente')).toBeInTheDocument();
  });

  it('la revisión de un día va en singular: «Vence en 1 día» y «Venció hace 1 día», nunca «1 días»', async () => {
    montar({
      [`GET ${INFORME}`]: () =>
        informeSagrilaft({
          filas: [
            filaSagrilaft({ vinculoId: 'v1', tercero: 'Mañana Ltda', diasParaRevision: 1 }),
            filaSagrilaft({ vinculoId: 'v2', terceroId: 't2', tercero: 'Ayer Ltda', diasParaRevision: -1 }),
          ],
        }),
    });
    const tabla = within(await screen.findByRole('table', { name: 'Informe SAGRILAFT' }));
    expect(within(tabla.getByRole('row', { name: /Mañana Ltda/ })).getByText('Vence en 1 día')).toBeInTheDocument();
    expect(within(tabla.getByRole('row', { name: /Ayer Ltda/ })).getByText('Venció hace 1 día')).toBeInTheDocument();
    expect(screen.queryByText(/1 días/)).not.toBeInTheDocument();
  });

  it('filtra por estado del estudio y por nivel de riesgo y deja quitar los filtros', async () => {
    montar();
    const tabla = await screen.findByRole('table', { name: 'Informe SAGRILAFT' });
    expect(within(tabla).getAllByRole('row')).toHaveLength(4);

    await userEvent.click(screen.getByRole('combobox', { name: 'Estudio' }));
    await userEvent.click(screen.getByRole('option', { name: 'Vencido' }));
    expect(within(tabla).getAllByRole('row')).toHaveLength(2);
    expect(within(tabla).getByText('Ferretería Roja S.A.S.')).toBeInTheDocument();
    expect(screen.getByText('1 de 1 terceros')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('combobox', { name: 'Riesgo' }));
    await userEvent.click(screen.getByRole('option', { name: '1 · Bajo' }));
    expect(screen.getByText('Ningún tercero coincide con esos filtros.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Quitar filtros' }));
    expect(within(screen.getByRole('table', { name: 'Informe SAGRILAFT' })).getAllByRole('row')).toHaveLength(4);
  });

  it('el detalle de personas muestra la participación, el PEP y el beneficiario final', async () => {
    const llamadas = montar({
      [`GET ${INFORME}/terceros/t2/personas`]: () => ({
        terceroId: 't2',
        tercero: 'Ferretería Roja S.A.S.',
        personas: [
          { rol: 'RepresentanteLegal', tipoIdentificacion: 'CC', identificacion: '1020304050', nombre: 'Ana Coincidencia', participacion: null, esPep: true, vinculoPep: false, beneficiarioFinal: false },
          { rol: 'Socio', tipoIdentificacion: 'CC', identificacion: '79000111', nombre: 'Luis Socio', participacion: 60, esPep: false, vinculoPep: false, beneficiarioFinal: true },
        ],
      }),
    });
    await userEvent.click(await screen.findByRole('button', { name: 'Ver personas de Ferretería Roja S.A.S.' }));
    const dialogo = within(await screen.findByRole('dialog', { name: /Personas de Ferretería Roja/ }));
    const tabla = within(await dialogo.findByRole('table'));
    expect(tabla.getByRole('row', { name: /Ana Coincidencia/ })).toHaveTextContent('PEP');
    expect(tabla.getByRole('row', { name: /Luis Socio/ })).toHaveTextContent('60 %');
    expect(tabla.getByRole('row', { name: /Luis Socio/ })).toHaveTextContent('Beneficiario final');
    expect(llamadas.some((l) => l.ruta === `${INFORME}/terceros/t2/personas`)).toBe(true);
    await userEvent.click(dialogo.getByRole('button', { name: 'Cerrar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('un tercero sin personas no ofrece el detalle', async () => {
    montar();
    await screen.findByRole('table', { name: 'Informe SAGRILAFT' });
    expect(screen.queryByRole('button', { name: 'Ver personas de Sin Estudio Ltda.' })).not.toBeInTheDocument();
  });

  it('descarga el Excel de tres hojas con la misma fecha', async () => {
    URL.createObjectURL = vi.fn(() => 'blob:informe');
    URL.revokeObjectURL = vi.fn();
    const descargas: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      descargas.push(this.download);
    });
    const llamadas = montar({
      [`GET ${INFORME}/excel`]: () => new Response('xlsx', { status: 200, headers: { 'Content-Disposition': 'attachment; filename="informe-sagrilaft-completo-20261111.xlsx"' } }),
    });
    const boton = await screen.findByRole('button', { name: 'Descargar Excel' });
    await waitFor(() => expect(boton).toBeEnabled());
    await userEvent.click(boton);
    await waitFor(() => expect(descargas).toEqual(['informe-sagrilaft-completo-20261111.xlsx']));
    const excel = llamadas.find((l) => l.ruta === `${INFORME}/excel`);
    expect(excel?.url.searchParams.get('hasta')).toBe(llamadas[0]?.url.searchParams.get('hasta'));
  });

  it('sin terceros dice qué aparecerá aquí', async () => {
    montar({ [`GET ${INFORME}`]: () => informeSagrilaft({ filas: [], terceros: 0, riesgo: [], tendencia: [] }) });
    expect(await screen.findByText('Aún no hay terceros con vínculo')).toBeInTheDocument();
    expect(screen.getByText('Ningún tercero tiene estudio todavía.')).toBeInTheDocument();
    expect(screen.getByText('Aún no hay estudios ni consultas en estos meses.')).toBeInTheDocument();
  });

  it('el informe de consultas y estudios sigue disponible como segunda vista', async () => {
    const llamadas = montar({
      'GET /api/v1/informes/cumplimiento': () => ({ desde: '2026-08-13', hasta: '2026-11-11', verificaciones: [], riesgo: [], estudiosVencidos: 4, pendientesReconsulta: 2, filas: [] }),
    });
    await screen.findByRole('table', { name: 'Informe SAGRILAFT' });
    await userEvent.click(screen.getByRole('button', { name: 'Consultas y estudios' }));
    expect(await screen.findByText('Listas por reconsultar')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Exportar a Excel' })).toBeInTheDocument();
    expect(llamadas.some((l) => l.ruta === '/api/v1/informes/cumplimiento')).toBe(true);
  });

  it('con una fila de cada estado los chips usan el mismo texto que el filtro', async () => {
    montar({ [`GET ${INFORME}`]: () => informeSagrilaft({ filas: [filaSagrilaft({ estadoDebidaDiligencia: 'PorVencer', diasParaRevision: 0 })] }) });
    const tabla = within(await screen.findByRole('table', { name: 'Informe SAGRILAFT' }));
    expect(tabla.getByText('Por vencer')).toBeInTheDocument();
    expect(tabla.getByText('Vence hoy')).toBeInTheDocument();
  });
});
