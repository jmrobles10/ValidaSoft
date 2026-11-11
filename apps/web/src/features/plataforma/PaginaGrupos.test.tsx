import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type EmpresaDeGrupoDto, type GrupoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { desplazarVentana, simularDisenoDeLista } from '../../pruebas/listaVirtual';
import { PaginaGrupos } from './PaginaGrupos';

const ALTO_GRUPO = 200;
const SEPARACION = 16;
const nombreDe = (i: number) => `Grupo ${String(i).padStart(2, '0')}`;

function empresa(grupo: number, i: number): EmpresaDeGrupoDto {
  return {
    id: `e${String(grupo)}-${String(i)}`,
    nombre: `Empresa ${String(grupo)}.${String(i)} S.A.S.`,
    identificacion: { pais: 'CO', tipo: 'NIT', numero: String(800_000_000 + grupo * 10 + i), digitoVerificacion: '1' },
    prefijoFolio: `P${String(grupo)}${String(i)}`,
    activa: true,
    planIaConfigurado: false,
    planIaActivo: false,
    llaveConfigurada: false,
    ultimosCuatro: null,
  };
}

function grupo(i: number, empresas = 1, nombre = nombreDe(i)): GrupoDto {
  return {
    tenant: { id: `t${String(i)}`, slug: `grupo-${String(i)}`, nombre, region: 'co', plan: 'Inicial', zonaHoraria: 'America/Bogota', idioma: 'es-CO', activo: true, creadoEn: '2026-11-09T15:00:00Z' },
    empresas: Array.from({ length: empresas }, (_, k) => empresa(i, k + 1)),
  };
}

/** 45 grupos: 20 + 20 + 5; el 8.º se llama «Andina Obras». */
const TODOS: GrupoDto[] = Array.from({ length: 45 }, (_, k) => grupo(k + 1, 1, k + 1 === 8 ? 'Andina Obras' : nombreDe(k + 1)));

interface Opciones {
  grupos?: GrupoDto[];
  /** La petición número n (0 = la primera) responde 500: para simular una página que falla. */
  fallaEn?: (n: number) => boolean;
}

/** API simulada: filtra por `q` y pagina con un cursor que es el desplazamiento. */
function montar({ grupos = TODOS, fallaEn = () => false }: Opciones = {}, ruta = '/plataforma/grupos') {
  const peticiones: URL[] = [];
  const json = (cuerpo: unknown, status = 200) =>
    Promise.resolve(new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } }));
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = new URL(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url, 'http://localhost');
      const n = peticiones.push(url) - 1;
      if (fallaEn(n)) return json({ codigo: 'error' }, 500);
      const q = (url.searchParams.get('q') ?? '').toLowerCase();
      const coinciden = grupos.filter(
        (g) => !q || g.tenant.nombre.toLowerCase().includes(q) || g.tenant.slug.includes(q) || g.empresas.some((e) => e.nombre.toLowerCase().includes(q)),
      );
      const limite = Number(url.searchParams.get('limite') ?? 20);
      const desde = Number(url.searchParams.get('cursor') ?? 0);
      const siguienteCursor = desde + limite < coinciden.length ? String(desde + limite) : null;
      return json({ datos: coinciden.slice(desde, desde + limite), siguienteCursor });
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/plataforma/grupos" element={<PaginaGrupos />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

const buscador = () => screen.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' });
const conCursor = (peticiones: URL[], cursor: string) => peticiones.filter((p) => p.searchParams.get('cursor') === cursor);

describe('PaginaGrupos', () => {
  beforeEach(() => simularDisenoDeLista({ altoFila: ALTO_GRUPO }));
  afterEach(() => {
    vi.restoreAllMocks();
    reiniciarCliente();
  });

  it('pide solo la primera página de 20 y dibuja unas pocas tarjetas, sin botón de cargar más', async () => {
    const peticiones = montar();

    expect(await screen.findByText('Grupo 01')).toBeInTheDocument();
    expect(peticiones).toHaveLength(1);
    expect(peticiones[0]?.searchParams.get('limite')).toBe('20');
    expect(peticiones[0]?.searchParams.has('cursor')).toBe(false);
    expect(peticiones[0]?.searchParams.has('q')).toBe(false);
    // Virtualizada: solo las visibles y unas de margen, no las 20 que ya llegaron.
    const tarjetas = screen.getAllByRole('listitem');
    expect(tarjetas.length).toBeGreaterThan(3);
    expect(tarjetas.length).toBeLessThan(14);
    expect(screen.queryByText('Grupo 20')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cargar más/i })).not.toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Grupos' })).toBeInTheDocument();
  });

  it('al acercarse al final pide la siguiente página con el cursor, hasta no tener más', async () => {
    const peticiones = montar();
    await screen.findByText('Grupo 01');

    desplazarVentana(20 * (ALTO_GRUPO + SEPARACION));
    await waitFor(() => expect(conCursor(peticiones, '20')).toHaveLength(1));
    expect(peticiones[1]?.searchParams.get('limite')).toBe('20');
    await screen.findByText('Grupo 21');

    desplazarVentana(40 * (ALTO_GRUPO + SEPARACION));
    await waitFor(() => expect(conCursor(peticiones, '40')).toHaveLength(1));
    expect(await screen.findByText('Eso es todo: 45 grupos.')).toBeInTheDocument();
    expect(peticiones).toHaveLength(3);

    // Ya no hay más: seguir desplazándose no pide nada.
    desplazarVentana(45 * (ALTO_GRUPO + SEPARACION));
    expect(peticiones).toHaveLength(3);
  });

  it('busca con espera corta: una sola petición con q, desde el principio', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar();
    await screen.findByText('Grupo 01');

    await usuario.type(buscador(), 'andina');
    expect(peticiones).toHaveLength(1); // aún no: espera a que se deje de escribir
    await waitFor(() => expect(peticiones.filter((p) => p.searchParams.has('q'))).toHaveLength(1));
    const consulta = peticiones.find((p) => p.searchParams.has('q'));
    expect(consulta?.searchParams.get('q')).toBe('andina');
    expect(consulta?.searchParams.has('cursor')).toBe(false);

    expect(await screen.findByText('Andina Obras')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Grupo 01')).not.toBeInTheDocument());
    expect(await screen.findByText('Eso es todo: 1 grupo.')).toBeInTheDocument();
  });

  it('la búsqueda viene de la URL y, sin resultados, ofrece limpiarla', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar({}, '/plataforma/grupos?q=zzz');

    expect(await screen.findByText('Ningún grupo coincide con «zzz»')).toBeInTheDocument();
    expect(peticiones[0]?.searchParams.get('q')).toBe('zzz');
    expect(buscador()).toHaveValue('zzz');

    await usuario.click(screen.getByRole('button', { name: 'Limpiar la búsqueda' }));
    expect(await screen.findByText('Grupo 01')).toBeInTheDocument();
    expect(buscador()).toHaveValue('');
  });

  it('sin grupos dice que aún no hay y cómo dar de alta el primero', async () => {
    montar({ grupos: [] });
    expect(await screen.findByText('Aún no hay grupos')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Nuevo grupo' })).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Grupos' })).not.toBeInTheDocument();
  });

  it('un grupo con muchas empresas muestra la tabla plegada y se despliega a pedido', async () => {
    const usuario = userEvent.setup();
    montar({ grupos: [grupo(1, 7, 'Grupo Grande'), grupo(2, 2, 'Grupo Chico')] });

    expect(await screen.findByText('Grupo Grande')).toBeInTheDocument();
    // El chico muestra su tabla de una; el grande, solo cuántas empresas tiene.
    expect(screen.getByRole('table', { name: 'Empresas de Grupo Chico' })).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Empresas de Grupo Grande' })).not.toBeInTheDocument();
    expect(screen.getByText(/^7 empresas · /)).toBeInTheDocument();

    const mostrar = screen.getByRole('button', { name: 'Mostrar las 7 empresas' });
    expect(mostrar).toHaveAttribute('aria-expanded', 'false');
    await usuario.click(mostrar);
    const tabla = screen.getByRole('table', { name: 'Empresas de Grupo Grande' });
    expect(within(tabla).getAllByRole('row')).toHaveLength(8); // encabezado + 7
    expect(screen.getByRole('button', { name: 'Ocultar las empresas' })).toHaveAttribute('aria-expanded', 'true');
    expect(within(tabla).getByRole('link', { name: 'Plan de IA de Empresa 1.1 S.A.S.' })).toHaveAttribute('href', '/plataforma/grupos/t1/empresas/e1-1/plan-ia');

    await usuario.click(screen.getByRole('button', { name: 'Ocultar las empresas' }));
    expect(screen.queryByRole('table', { name: 'Empresas de Grupo Grande' })).not.toBeInTheDocument();
  });

  it('si una página falla no insiste sola: avisa y reintenta a pedido', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar({ fallaEn: (n) => n === 1 });
    await screen.findByText('Grupo 01');

    desplazarVentana(20 * (ALTO_GRUPO + SEPARACION));
    expect(await screen.findByText('No pudimos cargar más grupos.')).toBeInTheDocument();
    await new Promise((resolver) => setTimeout(resolver, 300));
    expect(peticiones).toHaveLength(2); // la que falló; no vuelve a pedir sola

    await usuario.click(screen.getByRole('button', { name: 'Reintentar' }));
    await waitFor(() => expect(peticiones).toHaveLength(3));
    expect(peticiones[2]?.searchParams.get('cursor')).toBe('20');
    await waitFor(() => expect(screen.queryByText('No pudimos cargar más grupos.')).not.toBeInTheDocument());
  });
});
