import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it } from 'vitest';
import { MarcoMinimo } from './MarcoMinimo';

const montar = (modo: 'light' | 'dark') =>
  render(
    <ThemeProvider theme={createValidasoftTheme(modo)}>
      <MarcoMinimo>
        <p>contenido</p>
      </MarcoMinimo>
    </ThemeProvider>,
  );

describe('MarcoMinimo (la cabecera del ingreso): la marca', () => {
  it('lleva el logotipo con los colores del modo claro', () => {
    montar('light');
    const logo = within(screen.getByRole('banner')).getByRole('img', { name: 'Validasoft' });
    expect(logo).toHaveTextContent('alidaSoft');
    expect(logo.querySelector('svg path')).toHaveAttribute('fill', '#22D38A');
    expect(logo.querySelector('[data-parte="texto"]')).toHaveStyle({ color: '#0A0F1C' });
    expect(logo.querySelector('[data-parte="texto"] span')).toHaveStyle({ color: '#2F55D9' });
  });

  it('cambia de colores con el tema oscuro', () => {
    montar('dark');
    const logo = within(screen.getByRole('banner')).getByRole('img', { name: 'Validasoft' });
    expect(logo.querySelector('svg path')).toHaveAttribute('fill', '#2BE29A');
    expect(logo.querySelector('[data-parte="texto"]')).toHaveStyle({ color: '#F2F5FB' });
    expect(logo.querySelector('[data-parte="texto"] span')).toHaveStyle({ color: '#7C9BFF' });
  });
});
