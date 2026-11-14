import { render, screen } from '@testing-library/react';
import type { SesionTerceroDto } from '@validasoft/api-client';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';
import { RequiereAccesoCompleto } from './PaginasSesion';
import { useSesionTercero } from './sesionTercero';

function sesion(aislada: boolean): SesionTerceroDto {
  return { accessToken: 't', expira: '2099-01-01T00:00:00Z', terceroId: 'ter', otpVerificado: false, vinculos: [], aislada, terceroEnmascarado: null };
}

function montar(ruta: string) {
  render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/empresas" element={<p>mis empresas</p>} />
        <Route element={<RequiereAccesoCompleto />}>
          <Route path="/boveda" element={<p>la bóveda</p>} />
          <Route path="/accesos" element={<p>los accesos</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

afterEach(() => useSesionTercero.setState({ estado: 'sin-sesion', sesion: null }));

describe('rutas del tercero entero', () => {
  it.each(['/boveda', '/accesos'])('una sesión aislada que entra a %s vuelve a «Mis empresas»', (ruta) => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion(true) });
    montar(ruta);

    expect(screen.getByText('mis empresas')).toBeInTheDocument();
  });

  it('con acceso completo se ve la pantalla', () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion(false) });
    montar('/boveda');

    expect(screen.getByText('la bóveda')).toBeInTheDocument();
  });
});
