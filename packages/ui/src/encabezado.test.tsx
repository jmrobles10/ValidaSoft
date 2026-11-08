import { Button, ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EncabezadoFijo, VARIABLE_TOPE } from './encabezado';
import { createValidasoftTheme } from './tema';

describe('EncabezadoFijo', () => {
  it('pone título, subtítulo y acciones en una franja pegada arriba del contenedor de scroll', () => {
    render(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <EncabezadoFijo
          antes={<a href="#/flujos">Flujos</a>}
          titulo="Nuevo flujo"
          tituloCompleto="Nuevo flujo"
          subtitulo="Definen qué áreas revisan"
          etiquetaAcciones="Acciones de la página"
          acciones={<Button>Crear borrador</Button>}
        />
      </ThemeProvider>,
    );
    const titulo = screen.getByRole('heading', { level: 1, name: 'Nuevo flujo' });
    const franja = titulo.closest('[data-encabezado-fijo]');
    expect(franja).toHaveStyle({ position: 'sticky', top: `var(${VARIABLE_TOPE}, 0px)` });
    expect(franja).toContainElement(screen.getByRole('link', { name: 'Flujos' }));
    expect(screen.getByRole('group', { name: 'Acciones de la página' })).toContainElement(screen.getByRole('button', { name: 'Crear borrador' }));
    expect(titulo).toHaveAttribute('title', 'Nuevo flujo');
  });

  it('sin acciones no deja un grupo vacío', () => {
    render(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <EncabezadoFijo titulo="Áreas" etiquetaAcciones="Acciones de la página" />
      </ThemeProvider>,
    );
    expect(screen.queryByRole('group')).toBeNull();
  });
});
