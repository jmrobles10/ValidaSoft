import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import { SCOPES } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppShell } from './AppShell';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
const excepciones = vi.hoisted(() => ({ conteo: null as number | null }));
vi.mock('../features/empresas/useEmpresas', () => ({
  useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }),
  aOpcion: (e: { id: string; nombre: string }) => ({ id: e.id, nombre: e.nombre, identificacion: '' }),
}));
vi.mock('../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));
vi.mock('../features/notificaciones/Campana', () => ({ Campana: () => null }));
vi.mock('../features/revisionAutomatica/api', () => ({ useConteoExcepciones: () => excepciones.conteo }));
vi.mock('../sesion/useYo', () => ({ useYo: () => ({ data: undefined }) }));

function montar(ruta: string) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route path="/e/:empresaId/*" element={<AppShell />} />
        </Routes>
      </MemoryRouter>
    </ThemeProvider>,
  );
}

afterEach(() => {
  permisos.scopes = new Set();
  excepciones.conteo = null;
});

describe('AppShell', () => {
  it('sin auditoria:leer no muestra la pestaña Auditoría', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    montar('/e/emp/vinculos');
    expect(screen.getByRole('link', { name: 'Vínculos' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Auditoría' })).not.toBeInTheDocument();
  });

  it('con auditoria:leer muestra la pestaña y la marca activa en /auditoria', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.auditoriaLeer]);
    montar('/e/emp/auditoria');
    const pestana = screen.getByRole('link', { name: 'Auditoría' });
    expect(pestana).toHaveAttribute('href', '/e/emp/auditoria');
    expect(pestana).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Vínculos' })).not.toHaveAttribute('aria-current');
  });

  it('Configuración no es una pestaña de texto: es un engranaje con su tooltip, activo en cualquiera de sus secciones', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.configuracionLeer]);
    montar('/e/emp/plantillas');
    const barra = screen.getByRole('navigation', { name: 'Secciones' });
    expect(within(barra).queryByRole('link', { name: 'Configuración' })).not.toBeInTheDocument();
    const engranaje = screen.getByRole('link', { name: 'Configuración' });
    expect(engranaje).toHaveAttribute('href', '/e/emp/tipos-vinculo');
    expect(engranaje).toHaveAttribute('aria-current', 'page');
    // Estando en Configuración ninguna pestaña queda activa.
    expect(within(barra).getByRole('link', { name: 'Vínculos' })).not.toHaveAttribute('aria-current');
  });

  it('fuera de Configuración el engranaje no está activo, y sin permisos de configuración no aparece', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.configuracionLeer]);
    montar('/e/emp/vinculos');
    expect(screen.getByRole('link', { name: 'Configuración' })).not.toHaveAttribute('aria-current');
  });

  it('sin ningún permiso de configuración no hay engranaje', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    montar('/e/emp/vinculos');
    expect(screen.queryByRole('link', { name: 'Configuración' })).not.toBeInTheDocument();
  });

  it('con todos los permisos hay diez pestañas, en su orden, y sin medidas (jsdom) todas a la vista', () => {
    permisos.scopes = new Set(Object.values(SCOPES));
    montar('/e/emp/inicio');
    const barra = screen.getByRole('navigation', { name: 'Secciones' });
    expect(within(barra).getAllByRole('link').map((e) => e.textContent)).toEqual([
      'Inicio',
      'Bandeja',
      'Excepciones',
      'Revalidaciones',
      'Vencimientos',
      'Cumplimiento',
      'Certificados',
      'Vínculos',
      'Informes',
      'Auditoría',
    ]);
    expect(within(barra).queryByRole('button', { name: 'Más' })).not.toBeInTheDocument();
  });

  it('Excepciones lleva el contador de las que esperan a una persona', () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    excepciones.conteo = 3;
    montar('/e/emp/excepciones');
    const pestana = screen.getByRole('link', { name: 'Excepciones (3)' });
    expect(pestana).toHaveAttribute('href', '/e/emp/excepciones');
    expect(pestana).toHaveAttribute('aria-current', 'page');
  });

  it('sin excepciones pendientes la pestaña no muestra contador', () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    excepciones.conteo = 0;
    montar('/e/emp/bandeja');
    expect(screen.getByRole('link', { name: 'Excepciones' })).toBeInTheDocument();
  });

  it('sin expedientes:leer no aparece la pestaña Excepciones', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    montar('/e/emp/vinculos');
    expect(screen.queryByRole('link', { name: /^Excepciones/ })).not.toBeInTheDocument();
  });
});
