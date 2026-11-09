import { ThemeProvider } from '@mui/material';
import { fireEvent, render, screen, within } from '@testing-library/react';
import type { AreaDto, TipoDocumentoTenantDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import { areaNueva, requisitoNuevo, type RequisitoEnEditor } from '../editor';
import { EditorRequisitos } from './EditorRequisitos';

const area = (id: string, nombre: string): AreaDto => ({ id, empresaId: 'e', codigo: id.toUpperCase(), nombre, miembros: ['u1'], activa: true });
const tipo = { id: 'rut', codigo: 'RUT', nombre: 'Registro Único Tributario', clase: 'Archivo', activo: true } as TipoDocumentoTenantDto;

function editor(requisitos: RequisitoEnEditor[], onCambiar = vi.fn()) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <EditorRequisitos
        requisitos={requisitos}
        etapas={[{ clave: 'e1', areas: [areaNueva('compras'), areaNueva('cumplimiento'), areaNueva('juridica'), areaNueva('tesoreria')] }]}
        areas={[area('compras', 'Compras'), area('cumplimiento', 'Cumplimiento'), area('juridica', 'Jurídica'), area('tesoreria', 'Tesorería')]}
        tiposDocumento={[tipo]}
        onCambiar={onCambiar}
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
    const obligatorio = screen.getByRole('switch', { name: 'Obligatorio' });
    const quitar = screen.getByRole('button', { name: 'Quitar documento' });
    const celda = obligatorio.closest('label')?.parentElement;
    expect(celda).toBe(quitar.parentElement);
    expect(celda).toHaveStyle({ alignItems: 'center', minHeight: '32px' });
  });

  it('«Se pide a» elige entre todos, solo jurídicas o solo naturales', () => {
    const onCambiar = vi.fn();
    editor([requisitoNuevo('rut')], onCambiar);
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Se pide a' }));
    const opciones = within(screen.getByRole('listbox'));
    expect(opciones.getAllByRole('option').map((o) => o.textContent)).toEqual(['Todos', 'Solo personas jurídicas', 'Solo personas naturales']);
    fireEvent.click(opciones.getByRole('option', { name: 'Solo personas jurídicas' }));
    expect(onCambiar).toHaveBeenCalledWith([
      expect.objectContaining({ condicion: { tipo: 'Predicado', nodos: null, atributo: 'tercero.tipoPersona', operador: 'Igual', valores: ['Juridica'] } }),
    ]);
  });

  it('una condición armada por la API se muestra descrita y como «Condición personalizada»', () => {
    editor([{ ...requisitoNuevo('rut'), condicion: { tipo: 'Predicado', atributo: 'tercero.pais', operador: 'Distinto', valores: ['CO'] } }]);
    expect(screen.getByRole('combobox', { name: 'Se pide a' })).toHaveTextContent('Condición personalizada');
    expect(screen.getByText('Se pide cuando país no es CO.')).toBeInTheDocument();
  });
});
