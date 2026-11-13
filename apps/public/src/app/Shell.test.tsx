import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useSesionTercero } from '../sesion/sesionTercero';
import { Shell } from './Shell';

vi.mock('../notificaciones/Campana', () => ({ Campana: () => null }));
vi.mock('../embebido/usePuenteEmbebido', () => ({ usePuenteEmbebido: () => undefined }));

function montar(ruta: string) {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route element={<Shell />}>
            <Route path="*" element={<p>contenido</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </ThemeProvider>,
  );
}

afterEach(() => useSesionTercero.setState({ estado: 'sin-sesion', sesion: null }));

describe('Shell del tercero: la marca', () => {
  it('la barra lleva el logotipo de Validasoft (la «V» y «alidaSoft») y, en las pantallas de entrada, el rótulo «Mis documentos»', () => {
    useSesionTercero.setState({ estado: 'sin-sesion' });
    montar('/entrar');
    const barra = screen.getByRole('banner');
    const logo = within(barra).getByRole('img', { name: 'Validasoft' });
    expect(logo).toHaveTextContent('alidaSoft');
    expect(logo.querySelector('svg path')).toHaveAttribute('fill', '#22D38A');
    expect(within(barra).getByText('Mis documentos')).toBeInTheDocument();
  });

  it('en la verificación pública el rótulo es «Verificación de certificados»', () => {
    montar('/verificar/NORTE-C-2026-000001-1');
    expect(within(screen.getByRole('banner')).getByText('Verificación de certificados')).toBeInTheDocument();
    expect(within(screen.getByRole('banner')).getByRole('img', { name: 'Validasoft' })).toBeInTheDocument();
  });

  it('con sesión las secciones de la barra ya dicen dónde está la persona: sin el rótulo, para que no se corte', () => {
    useSesionTercero.setState({ estado: 'activa' });
    montar('/boveda');
    const barra = screen.getByRole('banner');
    expect(within(barra).getByRole('img', { name: 'Validasoft' })).toBeInTheDocument();
    expect(within(barra).queryByText('Mis documentos')).not.toBeInTheDocument();
    expect(within(barra).getByRole('link', { name: 'Mi bóveda' })).toBeInTheDocument();
  });

  it('el pie dice «con Validasoft» con el ícono de la marca (decorativo)', () => {
    montar('/entrar');
    const pie = screen.getByRole('contentinfo');
    expect(pie).toHaveTextContent('con Validasoft');
    expect(pie.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    // El nombre «Validasoft» del logotipo es solo el de la barra: el ícono del pie no lo repite a los lectores de pantalla.
    expect(within(pie).queryByRole('img', { name: 'Validasoft' })).not.toBeInTheDocument();
  });
});
