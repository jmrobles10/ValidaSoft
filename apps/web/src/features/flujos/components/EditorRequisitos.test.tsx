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
        etapas={[{ clave: 'e1', areas: [areaNueva('compras'), areaNueva('cumplimiento'), areaNueva('juridica'), areaNueva('tesoreria')] }]}
        areas={[area('compras', 'Compras'), area('cumplimiento', 'Cumplimiento'), area('juridica', 'Jurídica'), area('tesoreria', 'Tesorería')]}
        tiposDocumento={[tipo]}
        onCambiar={vi.fn()}
        errores={[]}
        soloLectura={false}
      />
    </ThemeProvider>,
  );
}

describe('EditorRequisitos', () => {
  it('«Lo revisan» muestra las áreas como chips en una sola línea: dos a la vista y el resto en «+N»', () => {
    editor([{ ...requisitoNuevo('rut'), areasQueRevisan: ['compras', 'cumplimiento', 'juridica', 'tesoreria'] }]);
    const chips = [...document.querySelectorAll('.MuiChip-root')].map((c) => c.textContent);
    expect(chips).toEqual(['Compras', 'Cumplimiento', '+2']);
    expect(screen.getByText('+2').closest('.MuiChip-root')).toHaveAttribute('title', 'Jurídica, Tesorería');
    // Los chips se pueden quitar uno a uno, como antes.
    expect(document.querySelectorAll('.MuiChip-deleteIcon')).toHaveLength(2);
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
