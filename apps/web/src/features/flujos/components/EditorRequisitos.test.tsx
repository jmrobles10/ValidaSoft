import { ThemeProvider } from '@mui/material';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { ErrorApi, type AreaDto, type TipoDocumentoTenantDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import { areaNueva, requisitoDeSeccionNuevo, requisitoNuevo, type RequisitoEnEditor } from '../editor';
import { errorDeApiEnRequisitos } from '../requisitosDeSeccion';
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

describe('EditorRequisitos: secciones de la información del tercero (F2)', () => {
  const financiera = { ...requisitoDeSeccionNuevo('FICHA_FINANCIERA'), periodos: 2 };

  it('«Agregar sección de la información» suma un requisito de sección sin elegir', () => {
    const onCambiar = vi.fn();
    editor([], onCambiar);
    fireEvent.click(screen.getByRole('button', { name: 'Agregar sección de la información' }));
    expect(onCambiar).toHaveBeenCalledWith([expect.objectContaining({ seccion: '', tipoDocumentoTenantId: '', exigeSoporte: false, periodos: null })]);
  });

  it('la financiera pide periodos y «Exige soporte», con la nota de que no aplica a persona natural', () => {
    const onCambiar = vi.fn();
    editor([financiera], onCambiar);
    const fila = within(screen.getByRole('region', { name: 'Información financiera' }));
    expect(fila.getByRole('combobox', { name: 'Sección de la información del tercero' })).toHaveTextContent('Información financiera');
    expect(fila.queryByRole('combobox', { name: 'Tipo de documento' })).not.toBeInTheDocument();
    expect(fila.getByRole('spinbutton', { name: 'Periodos' })).toHaveValue(2);
    expect(fila.getByText('Legal y Financiera no aplican a persona natural: a ella no se le piden.')).toBeInTheDocument();

    fireEvent.change(fila.getByRole('spinbutton', { name: 'Periodos' }), { target: { value: '3' } });
    expect(onCambiar).toHaveBeenLastCalledWith([expect.objectContaining({ seccion: 'FICHA_FINANCIERA', periodos: 3 })]);
    fireEvent.click(fila.getByRole('checkbox', { name: 'Exige soporte' }));
    expect(onCambiar).toHaveBeenLastCalledWith([expect.objectContaining({ exigeSoporte: true })]);
    expect(fila.getByRole('button', { name: 'Quitar sección' })).toBeInTheDocument();
  });

  it('otra sección no tiene periodos; al elegir una que no va por periodo se limpian', () => {
    const onCambiar = vi.fn();
    editor([financiera], onCambiar);
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Sección de la información del tercero' }));
    fireEvent.click(within(screen.getByRole('listbox')).getByRole('option', { name: 'Seguridad y salud en el trabajo' }));
    expect(onCambiar).toHaveBeenCalledWith([expect.objectContaining({ seccion: 'FICHA_SST', periodos: null })]);
  });

  it('muestra el 422 de la API en su campo: el duplicado en la fila y los periodos inválidos en «Periodos»', () => {
    const requisitos = [financiera, { ...financiera, clave: 'otra' }];
    const duplicado = errorDeApiEnRequisitos(new ErrorApi(422, 'requisito-duplicado', 'El flujo ya pide FICHA_FINANCIERA.', null, null, {}, { requisito: 1 }), requisitos);
    const periodos = errorDeApiEnRequisitos(new ErrorApi(422, 'periodos-invalidos', 'Se piden entre 1 y 5 periodos de Información financiera.', null, null, {}, { requisito: 0 }), [financiera]);
    if (!duplicado || !periodos) throw new Error('los 422 debían ir a un requisito');

    render(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <EditorRequisitos requisitos={requisitos} etapas={[{ clave: 'e1', areas: [areaNueva('compras')] }]} areas={[area('compras', 'Compras')]} tiposDocumento={[]} onCambiar={vi.fn()} errores={[duplicado, periodos]} soloLectura={false} />
      </ThemeProvider>,
    );
    const [primera, segunda] = screen.getAllByRole('region', { name: 'Información financiera' });
    if (!primera || !segunda) throw new Error('faltan las filas');
    expect(within(primera).getByRole('spinbutton', { name: 'Periodos' })).toHaveAccessibleDescription('Entre 1 y 5 periodos.');
    expect(within(primera).queryByText('Esa sección ya está en la lista.')).not.toBeInTheDocument();
    expect(within(segunda).getByText('Esa sección ya está en la lista.')).toBeInTheDocument();
  });
});
