import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import type { EmpresaDto, UsuarioDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import { DrawerRoles } from './DrawerRoles';

const EMPRESAS: EmpresaDto[] = [
  { id: 'e1', tenantId: 't1', identificacion: { pais: 'CO', tipo: 'NIT', numero: '800197268' }, nombre: 'Norte S.A.S.', logoUrl: null, replyTo: null, prefijoFolio: 'NORTE', activa: true },
  { id: 'e2', tenantId: 't1', identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456' }, nombre: 'Sur S.A.S.', logoUrl: null, replyTo: null, prefijoFolio: 'SUR', activa: true },
];

const USUARIO: UsuarioDto = {
  id: 'u1',
  nombre: 'Camila Admin',
  correo: 'camila@norte.co',
  identificacion: null,
  activo: true,
  identidades: [],
  roles: [
    { rol: 'Administrador', empresaId: 'e1' },
    { rol: 'Revisor', empresaId: 'e2' },
    { rol: 'Auditor', empresaId: null },
  ],
  creadoEn: '2026-11-01T10:00:00Z',
  ultimoAcceso: null,
};

describe('DrawerRoles', () => {
  it('cada fila de roles tiene el nombre accesible de sus dos selectores, no solo la primera', () => {
    render(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <DrawerRoles usuario={USUARIO} empresas={EMPRESAS} guardando={false} error={null} onGuardar={vi.fn()} onCerrar={vi.fn()} />
      </ThemeProvider>,
    );

    const selectores = screen.getAllByRole('combobox');
    expect(selectores.map((s) => s.getAttribute('aria-label') ?? s.getAttribute('aria-labelledby'))).not.toContain(null);
    expect(selectores).toHaveLength(6);
    expect(screen.getByRole('combobox', { name: 'Empresa de la fila 2' })).toHaveTextContent('Sur S.A.S.');
    expect(screen.getByRole('combobox', { name: 'Rol de la fila 2' })).toHaveTextContent('Revisor');
    expect(screen.getByRole('combobox', { name: 'Empresa de la fila 3' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Rol de la fila 3' })).toBeInTheDocument();
    for (const selector of selectores) expect(selector).toHaveAccessibleName();
  });
});
