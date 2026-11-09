import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { SCOPES } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppShell } from './AppShell';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../features/empresas/useEmpresas', () => ({
  useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }),
  aOpcion: (e: { id: string; nombre: string }) => ({ id: e.id, nombre: e.nombre, identificacion: '' }),
}));
vi.mock('../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));
vi.mock('../features/notificaciones/Campana', () => ({ Campana: () => null }));
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
});
