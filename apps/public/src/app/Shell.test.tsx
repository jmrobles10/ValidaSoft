import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useSesionTercero } from '../sesion/sesionTercero';
import { Shell } from './Shell';

vi.mock('../notificaciones/Campana', () => ({ Campana: () => null }));
// El aviso de «accesos por aprobar» pregunta a la API (se prueba aparte, con su propio cliente).
vi.mock('../accesos/AvisosDeAcceso', async (original) => ({ ...(await original<typeof import('../accesos/AvisosDeAcceso')>()), AvisoDeAccesosPorAprobar: () => null }));
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

describe('Shell del tercero con acceso aislado', () => {
  const aislada = { accessToken: 't', expira: '2099-01-01T00:00:00Z', terceroId: 'ter', otpVerificado: false, vinculos: [], aislada: true, terceroEnmascarado: 'P••••• N•••' };

  it('la barra solo ofrece «Mis empresas» (la bóveda, la información, las cuentas y la privacidad son del tercero entero) y avisa que el acceso espera aprobación', () => {
    useSesionTercero.setState({ estado: 'activa', sesion: aislada });
    montar('/empresas');

    const barra = screen.getByRole('banner');
    expect(within(barra).getByRole('link', { name: 'Mis empresas' })).toBeInTheDocument();
    for (const oculta of ['Mi bóveda', 'Mi información', 'Mis cuentas', 'Privacidad']) expect(within(barra).queryByRole('link', { name: oculta })).not.toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Tu acceso está pendiente de que un contacto de P••••• N••• lo apruebe.');
    const inferior = document.querySelector('.MuiBottomNavigation-root');
    expect([...(inferior?.querySelectorAll('.MuiBottomNavigationAction-label') ?? [])].map((r) => r.textContent)).toEqual(['Mis empresas']);
  });

  it('con acceso completo la barra trae las cinco secciones y ningún aviso', () => {
    useSesionTercero.setState({ estado: 'activa', sesion: { ...aislada, aislada: false, terceroEnmascarado: null } });
    montar('/empresas');

    expect(within(screen.getByRole('banner')).getAllByRole('link').map((l) => l.textContent)).toEqual(['Mi bóveda', 'Mis empresas', 'Mi información', 'Mis cuentas', 'Privacidad']);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});

describe('Shell del tercero: la barra inferior del celular', () => {
  it('cada rótulo va en una sola línea, también el de la sección activa (sin partir «Mi bóveda» ni «Mi información»)', () => {
    useSesionTercero.setState({ estado: 'activa' });
    montar('/boveda');
    const barra = document.querySelector('.MuiBottomNavigation-root');
    expect(barra).not.toBeNull();
    const rotulos = [...(barra?.querySelectorAll('.MuiBottomNavigationAction-label') ?? [])];
    expect(rotulos.map((r) => r.textContent)).toEqual(['Mi bóveda', 'Mis empresas', 'Mi información', 'Mis cuentas', 'Privacidad']);
    expect(barra?.querySelector('.Mui-selected')).not.toBeNull();
    for (const rotulo of rotulos) {
      expect(rotulo).toHaveStyle({ whiteSpace: 'nowrap' });
    }
  });
});
