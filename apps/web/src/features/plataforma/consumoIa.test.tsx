import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type ConsumoDeIaDto } from '@validasoft/api-client';
import { Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { montarPantalla } from '../../pruebas/montar';
import { ProblemaSimulado, simularApi } from '../../pruebas/simularApi';
import { PaginaConsumoIa } from './PaginaConsumoIa';
import { periodoDe } from './modeloConsumo';

const guardados: { nombre: string }[] = [];
vi.mock('../vencimientos/api', () => ({
  guardarArchivo: (archivo: { nombre?: string }, porDefecto: string) => guardados.push({ nombre: archivo.nombre ?? porDefecto }),
}));

afterEach(() => {
  reiniciarCliente();
  guardados.length = 0;
});

const PERIODO = periodoDe(new Date());

const CONSUMO: ConsumoDeIaDto = {
  periodo: PERIODO,
  moneda: 'COP',
  precioPorValidacionAdicional: 1500,
  totales: { grupos: 2, empresas: 3, incluidasConsumidas: 420, adicionalesConsumidas: 30, analisisPagados: 450, tokensEntrada: 2_500_000, tokensSalida: 120_000, costoUsd: 18.4321, valorAdicional: 45000 },
  grupos: [
    {
      grupoId: 'g1',
      grupo: 'Grupo Andino',
      slug: 'grupo-andino',
      incluidasConsumidas: 300,
      adicionalesConsumidas: 30,
      analisisPagados: 330,
      tokensEntrada: 1_800_000,
      tokensSalida: 90_000,
      costoUsd: 13.5,
      valorAdicional: 45000,
      empresas: [
        {
          empresaId: 'e1',
          empresa: 'Andina de Obras S.A.S.',
          validacionesIncluidasMes: 300,
          incluidasConsumidas: 300,
          adicionalesConsumidas: 30,
          adicionalHabilitado: true,
          analisisPagados: 330,
          tokensEntrada: 1_800_000,
          tokensEntradaEnCache: 0,
          tokensSalida: 90_000,
          costoUsd: 13.5,
          precioAdicional: 1500,
          precioPropio: true,
          valorAdicional: 45000,
          modelos: [{ modelo: 'gpt-6-luna', llamadas: 330, tokensEntrada: 1_800_000, tokensEntradaEnCache: 0, tokensSalida: 90_000, costoUsd: 13.5 }],
        },
      ],
    },
    {
      grupoId: 'g2',
      grupo: 'Grupo Sur',
      slug: 'grupo-sur',
      incluidasConsumidas: 120,
      adicionalesConsumidas: 0,
      analisisPagados: 120,
      tokensEntrada: 700_000,
      tokensSalida: 30_000,
      costoUsd: 4.9321,
      valorAdicional: 0,
      empresas: [],
    },
  ],
  siguienteCursor: null,
};

const TARIFA = { precioPorValidacionAdicional: 1500, moneda: 'COP', actualizadaEn: '2026-11-05T15:00:00Z', actualizadaPor: 'camila@validasoft.co' };

function montar(rutas: Parameters<typeof simularApi>[0] = {}, ruta = '/plataforma/uso-ia') {
  const peticiones = simularApi({
    'GET /api/v1/plataforma/ia/consumo': CONSUMO,
    'GET /api/v1/plataforma/ia/tarifa': TARIFA,
    ...rutas,
  });
  montarPantalla(
    <Routes>
      <Route path="/plataforma/uso-ia" element={<PaginaConsumoIa />} />
    </Routes>,
    ruta,
  );
  return { peticiones, usuario: userEvent.setup() };
}

describe('Uso de IA por cliente (PI-12)', () => {
  it('muestra los totales del mes con la tarifa y cada grupo con su consumo', async () => {
    montar();

    expect(await screen.findByRole('heading', { name: 'Uso de IA' })).toBeInTheDocument();
    expect(await screen.findByText('2 grupos · 3 empresas')).toBeInTheDocument();
    expect(screen.getByText('420')).toBeInTheDocument();
    expect(screen.getByText('2,5 M · 120 mil')).toBeInTheDocument();
    expect(await screen.findByText(/por validación adicional/)).toBeInTheDocument();
    const tabla = screen.getByRole('table', { name: 'Uso de IA' });
    expect(within(tabla).getByText('Grupo Andino')).toBeInTheDocument();
    expect(within(tabla).getByText('Grupo Sur')).toBeInTheDocument();
    expect(screen.getByText('Eso es todo: 2 grupos.')).toBeInTheDocument();
  });

  it('pide el mes actual por omisión y deja elegir otro', async () => {
    const { peticiones, usuario } = montar();
    await screen.findByText('Grupo Andino');
    expect(peticiones.find((p) => p.ruta === '/api/v1/plataforma/ia/consumo')?.url).toContain(`periodo=${PERIODO}`);

    await usuario.click(screen.getByRole('combobox', { name: 'Mes' }));
    const opciones = await screen.findAllByRole('option');
    expect(opciones).toHaveLength(12);
    await usuario.click(opciones[1]!);

    await waitFor(() => expect(peticiones.filter((p) => p.ruta === '/api/v1/plataforma/ia/consumo').length).toBeGreaterThan(1));
    const ultima = peticiones.filter((p) => p.ruta === '/api/v1/plataforma/ia/consumo').at(-1);
    expect(ultima?.url).not.toContain(`periodo=${PERIODO}`);
  });

  it('despliega las empresas de un grupo con su precio propio y los modelos que usó', async () => {
    const { usuario } = montar();
    await screen.findByText('Grupo Andino');
    expect(screen.queryByText('Andina de Obras S.A.S.')).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Ver la empresa de Grupo Andino' }));

    expect(await screen.findByText('Andina de Obras S.A.S.')).toBeInTheDocument();
    expect(screen.getByText('Precio propio')).toBeInTheDocument();
    expect(screen.getByText(/gpt-6-luna: 330 llamadas/)).toBeInTheDocument();
    expect(screen.getByText('300 / 300')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Ocultar las empresas de Grupo Andino' }));
    expect(screen.queryByText('Andina de Obras S.A.S.')).not.toBeInTheDocument();
  });

  it('con la búsqueda sin resultados lo dice y ofrece nada más que cambiarla', async () => {
    montar({ 'GET /api/v1/plataforma/ia/consumo': { ...CONSUMO, grupos: [], totales: { ...CONSUMO.totales, grupos: 0, empresas: 0 } } }, '/plataforma/uso-ia?q=zzz');
    expect(await screen.findByText('Ningún cliente coincide con «zzz»')).toBeInTheDocument();
  });

  it('un mes sin consumo dice que nadie consumió', async () => {
    montar({ 'GET /api/v1/plataforma/ia/consumo': { ...CONSUMO, grupos: [], totales: { ...CONSUMO.totales, grupos: 0, empresas: 0 } } });
    expect(await screen.findByText(/Nadie consumió IA en/)).toBeInTheDocument();
  });

  it('exporta a Excel o CSV con los mismos filtros', async () => {
    // Bytes, no un `Blob` de jsdom: con Node 22 (el del CI) `new Response(blob)` falla con «object.stream is not a function» —Node 26 sí lo acepta— y la
    // descarga simulada reventaba antes de guardar nada.
    const archivo = () => new Response(new Uint8Array([120]), { status: 200, headers: { 'Content-Type': 'text/csv' } });
    const { peticiones, usuario } = montar({ 'GET /api/v1/plataforma/ia/consumo/exportar': archivo }, '/plataforma/uso-ia?q=andino');
    await screen.findByText('Grupo Andino');

    await usuario.click(screen.getByRole('button', { name: 'CSV' }));

    await waitFor(() => expect(guardados).toHaveLength(1));
    const exportacion = peticiones.find((p) => p.ruta === '/api/v1/plataforma/ia/consumo/exportar');
    expect(exportacion?.url).toContain('formato=csv');
    expect(exportacion?.url).toContain('q=andino');
    expect(exportacion?.url).toContain(`periodo=${PERIODO}`);
    expect(guardados[0]?.nombre).toBe(`uso-de-ia-${PERIODO}.csv`);
  });

  it('si no se puede cargar ofrece reintentar', async () => {
    montar({ 'GET /api/v1/plataforma/ia/consumo': new ProblemaSimulado(500, 'error-interno', 'falló') });
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});

describe('tarifa del adicional (PI-12)', () => {
  it('se cambia con precio y moneda y se guarda para todos los clientes sin precio propio', async () => {
    const { peticiones, usuario } = montar({ 'PUT /api/v1/plataforma/ia/tarifa': { ...TARIFA, precioPorValidacionAdicional: 1800, moneda: 'COP' } });
    await usuario.click(await screen.findByRole('button', { name: 'Cambiar' }));

    const dialogo = await screen.findByRole('dialog', { name: 'Cambiar la tarifa general' });
    const precio = within(dialogo).getByLabelText('Precio');
    await usuario.clear(precio);
    await usuario.type(precio, '1800');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones.find((p) => p.metodo === 'PUT')).toBeDefined());
    expect(peticiones.find((p) => p.metodo === 'PUT')?.cuerpo).toEqual({ precioPorValidacionAdicional: 1800, moneda: 'COP' });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('un precio inválido no se envía y dice qué corregir', async () => {
    const { peticiones, usuario } = montar();
    await usuario.click(await screen.findByRole('button', { name: 'Cambiar' }));
    const dialogo = await screen.findByRole('dialog');
    const precio = within(dialogo).getByLabelText('Precio');
    await usuario.clear(precio);
    await usuario.type(precio, 'mil');

    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar' }));

    expect(await within(dialogo).findByText('Escribe un precio entre 0 y 100 000 000.')).toBeInTheDocument();
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);
  });

  it('sin tarifa fijada avisa que lo adicional no se valora', async () => {
    montar({ 'GET /api/v1/plataforma/ia/tarifa': { ...TARIFA, precioPorValidacionAdicional: 0, actualizadaEn: null, actualizadaPor: null } });
    expect(await screen.findByText(/Aún no hay tarifa/)).toBeInTheDocument();
  });
});
