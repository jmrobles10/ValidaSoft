import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import type { AreaDto, TipoDocumentoTenantDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import { areaNueva, requisitoNuevo, type RequisitoEnEditor } from '../editor';
import { EditorRequisitos } from './EditorRequisitos';

const area = (id: string, nombre: string): AreaDto => ({ id, empresaId: 'e', codigo: id.toUpperCase(), nombre, miembros: ['u1'], activa: true });
const tipo = { id: 'rut', codigo: 'RUT', nombre: 'Registro Único Tributario', clase: 'Archivo', activo: true } as TipoDocumentoTenantDto;

function editor(requisitos: RequisitoEnEditor[]) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <EditorRequisitos
        requisitos={requisitos}
        etapas={[{ clave: 'e1', areas: [areaNueva('compras'), areaNueva('cumplimiento')] }]}
        areas={[area('compras', 'Compras'), area('cumplimiento', 'Cumplimiento')]}
        tiposDocumento={[tipo]}
        onCambiar={vi.fn()}
        errores={[]}
        soloLectura={false}
      />
    </ThemeProvider>,
  );
}

describe('EditorRequisitos', () => {
  it('«Lo revisan» resume las áreas en una línea («2 áreas» o el nombre) en vez de etiquetas que crecen', () => {
    editor([{ ...requisitoNuevo('rut'), areasQueRevisan: ['compras', 'cumplimiento'] }, { ...requisitoNuevo('rut'), areasQueRevisan: ['cumplimiento'] }]);
    expect(screen.getByText('2 áreas')).toHaveAttribute('title', 'Compras, Cumplimiento');
    expect(screen.getByText('Cumplimiento')).toBeInTheDocument();
    expect(document.querySelectorAll('.MuiChip-root')).toHaveLength(0);
  });

  it('«Obligatorio» y la papelera van juntos en la celda alineada con los campos', () => {
    editor([requisitoNuevo('rut')]);
    const obligatorio = screen.getByRole('checkbox', { name: 'Obligatorio' });
    const quitar = screen.getByRole('button', { name: 'Quitar documento' });
    const celda = obligatorio.closest('label')?.parentElement;
    expect(celda).toBe(quitar.parentElement);
    expect(celda).toHaveStyle({ alignItems: 'center', minHeight: '32px' });
  });
});
