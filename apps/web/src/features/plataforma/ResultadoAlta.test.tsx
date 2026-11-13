import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import type { TenantCreadoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { ResultadoAlta } from './ResultadoAlta';

function alta(nombre: string): TenantCreadoDto {
  return {
    tenant: { id: 't1', slug: 'cumbre', nombre, region: 'co', plan: 'Inicial', zonaHoraria: 'America/Bogota', idioma: 'es-CO', activo: true, creadoEn: '2026-11-09T15:00:00Z' },
    apiKeyAdministrador: { id: 'k1', prefijo: 'vs_admin', nombre: 'Administración', scopes: [], empresas: [], expiraEn: null, creadaEn: '2026-11-09T15:00:00Z', sandbox: false, valor: 'vs_admin_0123456789' },
    empresa: null,
    administrador: null,
    sistema: null,
    apiKeySistema: null,
  };
}

function montar(nombre: string) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <MemoryRouter>
        <ResultadoAlta alta={alta(nombre)} />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

describe('ResultadoAlta', () => {
  it('antepone «Grupo» al nombre que no lo trae', () => {
    montar('Cumbre');
    expect(screen.getByRole('heading', { level: 1, name: 'Grupo Cumbre creado' })).toBeInTheDocument();
  });

  it('no repite «Grupo» cuando el nombre ya empieza por esa palabra', () => {
    montar('Grupo Cumbre');
    expect(screen.getByRole('heading', { level: 1, name: 'Grupo Cumbre creado' })).toBeInTheDocument();
    expect(screen.queryByText(/Grupo Grupo/)).not.toBeInTheDocument();
  });
});
