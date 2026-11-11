import { Accordion, AccordionDetails, AccordionSummary, Autocomplete, Button, MenuItem, TextField, ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve, sep } from 'node:path';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { DosColumnas } from './disposicion';
import { EsqueletoDetalle, EsqueletoFormulario, EsqueletoHistorial, EsqueletoTabla, EsqueletoTarjeta, EsqueletoTarjetas, OpcionesEnCarga } from './esqueletos';
import { createValidasoftTheme, type App } from './tema';

/** Reglas visuales de toda la aplicación (UI-1…UI-6): lo que el tema y los esqueletos garantizan, y lo que ningún código debe volver a hacer. */
const conTema = (nodo: ReactNode, app: App = 'web') => render(<ThemeProvider theme={createValidasoftTheme('light', { app })}>{nodo}</ThemeProvider>);

describe('UI-1 · cargando = esqueleto', () => {
  it.each([
    ['EsqueletoTarjetas', <EsqueletoTarjetas key="t" cantidad={3} />],
    ['EsqueletoFormulario', <EsqueletoFormulario key="f" campos={4} columnas={2} conTarjeta />],
    ['EsqueletoTabla', <EsqueletoTabla key="b" columnas={4} filas={3} />],
    ['EsqueletoDetalle', <EsqueletoDetalle key="d" />],
    ['EsqueletoHistorial', <EsqueletoHistorial key="h" />],
    ['OpcionesEnCarga', <OpcionesEnCarga key="o" />],
  ])('%s es una región ocupada con nombre accesible y sin texto ni spinner', (_nombre, nodo) => {
    const { container } = conTema(nodo);
    const region = screen.getByRole('status');
    expect(region).toHaveAttribute('aria-busy', 'true');
    expect(region).toHaveAccessibleName();
    expect(container).toHaveTextContent('');
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(container.querySelectorAll('.MuiSkeleton-root').length).toBeGreaterThan(0);
  });

  it('el nombre accesible se puede cambiar y por omisión es «Cargando»', () => {
    conTema(<EsqueletoTarjetas etiqueta="Cargando los documentos" />);
    expect(screen.getByRole('status', { name: 'Cargando los documentos' })).toBeInTheDocument();
    conTema(<EsqueletoDetalle />);
    expect(screen.getByRole('status', { name: 'Cargando' })).toBeInTheDocument();
  });

  it('la tabla de esqueleto tiene la forma de una tabla (encabezado y filas)', () => {
    conTema(<EsqueletoTabla columnas={3} filas={4} />);
    const tabla = screen.getByRole('table');
    expect(within(tabla).getAllByRole('row')).toHaveLength(5);
    expect(within(tabla).getAllByRole('columnheader')).toHaveLength(3);
  });

  it('una tarjeta de esqueleto respeta el alto mínimo pedido', () => {
    const { container } = conTema(<EsqueletoTarjeta alto={112} />);
    expect(getComputedStyle(container.firstElementChild!).minHeight).toBe('112px');
  });

  it('mientras el autocompletar busca muestra un esqueleto de opciones, no el texto «Cargando…»', async () => {
    const usuario = userEvent.setup();
    conTema(<Autocomplete open options={[]} loading renderInput={(p) => <TextField {...p} label="Ciudad" />} />);
    await usuario.click(screen.getByRole('combobox', { name: 'Ciudad' }));
    expect(screen.getByRole('status', { name: 'Buscando opciones' })).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByText(/Cargando/)).not.toBeInTheDocument();
  });
});

describe('UI-2 · botones compactos', () => {
  it.each(['web', 'public'] as const)('el tamaño por omisión es medium en %s y large queda para la acción principal', (app) => {
    const tema = createValidasoftTheme('light', { app });
    expect(tema.components?.MuiButton?.defaultProps?.size).toBe('medium');
  });

  it('en web el botón mide lo que el campo (32 px), el pequeño 28 y el grande 36', () => {
    const estilos = createValidasoftTheme('light', { app: 'web' }).components?.MuiButton?.styleOverrides as Record<string, Record<string, unknown>>;
    expect([estilos.sizeSmall?.minHeight, estilos.sizeMedium?.minHeight, estilos.sizeLarge?.minHeight]).toEqual([28, 32, 36]);
  });

  it('en el tercero el botón mide 36 px en escritorio y 40 en el celular, y el área táctil de 44 se completa sin inflarlo', () => {
    const estilos = createValidasoftTheme('light', { app: 'public' }).components?.MuiButton?.styleOverrides as Record<string, Record<string, unknown>>;
    const medio = estilos.sizeMedium as { minHeight: number; '@media (min-width:600px)': { minHeight: number } };
    expect(medio.minHeight).toBe(40);
    expect(medio['@media (min-width:600px)'].minHeight).toBe(36);
    const grande = estilos.sizeLarge as { minHeight: number; '@media (min-width:600px)': { minHeight: number } };
    expect([grande.minHeight, grande['@media (min-width:600px)'].minHeight]).toEqual([44, 40]);
    const raiz = estilos.root as Record<string, { '&::after': { height: number } }>;
    expect(raiz['@media (max-width:599.95px)']?.['&::after'].height).toBe(44);
    // Ninguno de los tamaños fija 44 px de alto en escritorio.
    const pequeno = estilos.sizeSmall as { '@media (min-width:600px)': { minHeight: number } };
    expect(pequeno['@media (min-width:600px)'].minHeight).toBeLessThanOrEqual(36);
  });

  it('un botón sin tamaño se dibuja con el tamaño medium', () => {
    conTema(<Button>Guardar</Button>, 'public');
    expect(screen.getByRole('button', { name: 'Guardar' })).toHaveClass('MuiButton-sizeMedium');
  });
});

describe('UI-3 · autocompletar y selects', () => {
  it.each(['web', 'public'] as const)('el ícono de abrir queda en su sitio (centrado por MUI) y no se corre hacia arriba en %s', (app) => {
    const sobrescritos = createValidasoftTheme('light', { app }).components?.MuiAutocomplete?.styleOverrides as Record<string, Record<string, unknown>>;
    expect(sobrescritos.endAdornment).not.toHaveProperty('top');
    expect(sobrescritos.endAdornment).not.toHaveProperty('transform');
  });

  it('los textos del autocompletar están en español', async () => {
    const usuario = userEvent.setup();
    conTema(<Autocomplete options={[]} value={null} renderInput={(p) => <TextField {...p} label="País" />} />);
    expect(screen.getByRole('button', { name: 'Abrir' })).toHaveAttribute('title', 'Abrir');
    await usuario.click(screen.getByRole('button', { name: 'Abrir' }));
    expect(screen.getByText('Sin resultados')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cerrar' })).toHaveAttribute('title', 'Cerrar');
  });

  it('los demás componentes de MUI con texto propio también salen en español', () => {
    const c = createValidasoftTheme('light').components;
    expect(c?.MuiAutocomplete?.defaultProps).toMatchObject({ openText: 'Abrir', closeText: 'Cerrar', clearText: 'Limpiar', noOptionsText: 'Sin resultados' });
    expect(c?.MuiAlert?.defaultProps).toMatchObject({ closeText: 'Cerrar' });
    expect(c?.MuiTablePagination?.defaultProps).toMatchObject({ labelRowsPerPage: 'Filas por página:' });
  });

  it('el select abre con su lista y el ícono usa la posición centrada de MUI', async () => {
    const usuario = userEvent.setup();
    conTema(
      <TextField select label="Tipo de vía" value="" slotProps={{ select: { displayEmpty: true } }} onChange={() => undefined}>
        <MenuItem value="Calle">Calle</MenuItem>
      </TextField>,
    );
    await usuario.click(screen.getByRole('combobox', { name: 'Tipo de vía' }));
    expect(screen.getByRole('option', { name: 'Calle' })).toBeInTheDocument();
    const icono = createValidasoftTheme('light').components?.MuiSelect?.styleOverrides as { icon: Record<string, unknown> };
    expect(icono.icon).not.toHaveProperty('top');
  });
});

describe('UI-4 · nada cuadrado suelto', () => {
  it.each(['web', 'public'] as const)('el acordeón tiene el radio de las tarjetas (12 px) en cualquier posición y sin la línea superior en %s', (app) => {
    const raiz = createValidasoftTheme('light', { app }).components?.MuiAccordion?.styleOverrides?.root as Record<string, unknown>;
    expect(raiz['&, &:first-of-type, &:last-of-type']).toEqual({ borderRadius: 12 });
    expect(raiz['&::before']).toEqual({ display: 'none' });
    expect(createValidasoftTheme('light', { app }).components?.MuiPaper?.styleOverrides?.rounded).toEqual({ borderRadius: 12 });
  });

  it('un acordeón con hermanos antes y después se pinta redondeado', () => {
    conTema(
      <div>
        <div>antes</div>
        <Accordion variant="outlined">
          <AccordionSummary>Dirección por partes</AccordionSummary>
          <AccordionDetails>contenido</AccordionDetails>
        </Accordion>
        <div>después</div>
      </div>,
    );
    expect(screen.getByRole('button', { name: 'Dirección por partes' }).closest('.MuiAccordion-root')).toHaveClass('MuiAccordion-rounded');
  });
});

describe('UI-5 · nada volcado a la izquierda', () => {
  it('DosColumnas reparte en dos columnas iguales desde lg y en una por debajo', () => {
    const { container } = conTema(<DosColumnas izquierda={<p>A</p>} derecha={<p>B</p>} />);
    const rejilla = container.firstElementChild as HTMLElement;
    expect(rejilla.children).toHaveLength(2);
    expect(within(rejilla.children[0] as HTMLElement).getByText('A')).toBeInTheDocument();
    expect(within(rejilla.children[1] as HTMLElement).getByText('B')).toBeInTheDocument();
  });
});

/** Recorre los fuentes (sin pruebas) de las dos apps y del design system. */
function fuentes(): { ruta: string; texto: string }[] {
  const raiz = resolve(import.meta.dirname, '../../..');
  const salida: { ruta: string; texto: string }[] = [];
  const recorrer = (carpeta: string) => {
    for (const nombre of readdirSync(carpeta)) {
      if (nombre === 'node_modules' || nombre === 'dist' || nombre === 'generado') continue;
      const ruta = join(carpeta, nombre);
      if (statSync(ruta).isDirectory()) recorrer(ruta);
      else if (/\.(ts|tsx)$/.test(nombre) && !/\.(test|spec)\./.test(nombre)) salida.push({ ruta: relative(raiz, ruta).split(sep).join('/'), texto: readFileSync(ruta, 'utf8') });
    }
  };
  for (const carpeta of ['apps/web/src', 'apps/public/src', 'packages/ui/src']) recorrer(join(raiz, carpeta));
  return salida;
}

describe('normas de código (UI-1, UI-2)', () => {
  const todos = fuentes().filter((f) => f.ruta !== 'packages/ui/src/tema.ts');

  it('el único spinner es el del botón que guarda (`IconoCarga`): ningún otro `CircularProgress`', () => {
    const conSpinner = todos.filter((f) => f.texto.includes('CircularProgress')).map((f) => f.ruta);
    expect(conSpinner).toEqual(['apps/web/src/shared/componentes.tsx']);
  });

  it('ningún botón fuerza un alto de 44 o 48 px (el tamaño lo da el tema)', () => {
    const infractores = todos
      .filter((f) => f.texto.split('\n').some((linea) => /<Button\b/.test(linea) && /minHeight:\s*4[48]\b/.test(linea)))
      .map((f) => f.ruta);
    expect(infractores).toEqual([]);
  });

  it('nadie pinta «Cargando…» como texto: lo que carga es un esqueleto', () => {
    const infractores = todos
      .filter((f) => f.texto.split('\n').some((linea) => /<Typography[^>]*>\s*\{t\('comun\.cargando'\)\}/.test(linea) || linea.includes('loadingText={t(')))
      .map((f) => f.ruta);
    expect(infractores).toEqual([]);
  });
});
