import { ThemeProvider } from '@mui/material';
import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CondicionDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import { borradorVacio, type ErrorFlujo } from '../editor';
import { EditorSeleccionFlujo } from './EditorSeleccionFlujo';

const juridica: CondicionDto = { tipo: 'Predicado', nodos: null, atributo: 'tercero.tipoPersona', operador: 'Igual', valores: ['Juridica'] };
const riesgoAlto: CondicionDto = { tipo: 'Predicado', nodos: null, atributo: 'vinculo.atributos.nivelRiesgo', operador: 'En', valores: ['Alto'] };

function montar(cambios: Partial<ReturnType<typeof borradorVacio>> = {}, errores: ErrorFlujo[] = [], soloLectura = false) {
  const onCambiar = vi.fn();
  const borrador = { ...borradorVacio(), ...cambios };
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <EditorSeleccionFlujo borrador={borrador} onCambiar={onCambiar} errores={errores} soloLectura={soloLectura} />
    </ThemeProvider>,
  );
  return { onCambiar, borrador };
}

describe('EditorSeleccionFlujo', () => {
  it('por omisión el flujo no se escoge solo: sin condición ni prioridad, y lo explica', () => {
    montar();
    expect(screen.getByRole('combobox', { name: 'Se escoge cuando' })).toHaveTextContent('Sin condición');
    expect(screen.queryByLabelText('Prioridad')).not.toBeInTheDocument();
    expect(screen.getByText(/solo se usa si lo eligen al abrir el expediente o si es el flujo por defecto/)).toBeInTheDocument();
  });

  it('ofrece los mismos atajos del editor de condiciones de los requisitos', () => {
    montar();
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Se escoge cuando' }));
    expect(within(screen.getByRole('listbox')).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Sin condición',
      'El tercero es persona jurídica',
      'El tercero es persona natural',
    ]);
  });

  it('elegir «persona jurídica» arma la condición y pide la prioridad', async () => {
    const usuario = userEvent.setup();
    const { onCambiar, borrador } = montar();

    await usuario.click(screen.getByRole('combobox', { name: 'Se escoge cuando' }));
    await usuario.click(await screen.findByRole('option', { name: 'El tercero es persona jurídica' }));
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, seleccionCondicion: juridica, seleccionPrioridad: '' });
  });

  it('con condición muestra la prioridad (solo dígitos) y su ayuda', () => {
    const { onCambiar, borrador } = montar({ seleccionCondicion: juridica, seleccionPrioridad: '2' });
    expect(screen.getByRole('textbox', { name: 'Prioridad' })).toHaveValue('2');
    expect(screen.getByText('1 a 999; la menor se evalúa primero y no se repite en el tipo.')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Prioridad'), { target: { value: '1x5' } });
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, seleccionPrioridad: '15' });
  });

  it('volver a «Sin condición» quita también la prioridad', async () => {
    const usuario = userEvent.setup();
    const { onCambiar, borrador } = montar({ seleccionCondicion: juridica, seleccionPrioridad: '2' });

    await usuario.click(screen.getByRole('combobox', { name: 'Se escoge cuando' }));
    await usuario.click(await screen.findByRole('option', { name: 'Sin condición' }));
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, seleccionCondicion: null, seleccionPrioridad: '' });
  });

  it('una condición armada por la API se describe, se conserva y no se ofrece como atajo nuevo', () => {
    const { onCambiar } = montar({ seleccionCondicion: riesgoAlto, seleccionPrioridad: '1' });
    expect(screen.getByRole('combobox', { name: 'Se escoge cuando' })).toHaveTextContent('Condición personalizada');
    expect(screen.getByText('Se escoge cuando nivelRiesgo es uno de Alto.')).toBeInTheDocument();
    expect(onCambiar).not.toHaveBeenCalled();
  });

  it('muestra los errores de la prioridad y de la condición en su campo', () => {
    montar({ seleccionCondicion: juridica, seleccionPrioridad: '' }, [
      { codigo: 'prioridad-requerida', campo: 'seleccion.prioridad' },
      { codigo: 'condicion-invalida', campo: 'seleccion.condicion' },
    ]);
    expect(screen.getByText('Escribe la prioridad: sin ella no se sabe en qué orden se evalúa.')).toBeInTheDocument();
    expect(screen.getByText('La condición de selección no es válida para este tipo de vínculo.')).toBeInTheDocument();
  });

  it('la prioridad repetida que responde la API se muestra en el campo', () => {
    montar({ seleccionCondicion: juridica, seleccionPrioridad: '1' }, [{ codigo: 'prioridad-duplicada', campo: 'seleccion.prioridad' }]);
    expect(screen.getByText('Otro flujo de este tipo de vínculo ya usa esa prioridad: elige otra.')).toBeInTheDocument();
  });

  it('en solo lectura (otra versión o sin permiso) no deja cambiar nada', () => {
    montar({ seleccionCondicion: juridica, seleccionPrioridad: '2' }, [], true);
    expect(screen.getByRole('combobox', { name: 'Se escoge cuando' })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByRole('textbox', { name: 'Prioridad' })).toBeDisabled();
  });
});
