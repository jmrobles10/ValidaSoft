import { ThemeProvider } from '@mui/material';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import { borradorVacio, type ErrorFlujo } from '../editor';
import { EditorRecordatorioFlujo } from './EditorRecordatorioFlujo';

function montar(cambios: Partial<ReturnType<typeof borradorVacio>> = {}, errores: ErrorFlujo[] = [], soloLectura = false) {
  const onCambiar = vi.fn();
  const borrador = { ...borradorVacio(), ...cambios };
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <EditorRecordatorioFlujo borrador={borrador} onCambiar={onCambiar} errores={errores} soloLectura={soloLectura} />
    </ThemeProvider>,
  );
  return { onCambiar, borrador };
}

describe('EditorRecordatorioFlujo', () => {
  it('por omisión sigue lo que diga la empresa y no pide días', () => {
    montar();
    expect(screen.getByText('Recordatorio de observaciones')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Recordatorio automático' })).toHaveTextContent('Lo que diga la empresa');
    expect(screen.getByText('Se usa lo que la empresa configuró en Plantillas de correo.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Cada (días)')).not.toBeInTheDocument();
  });

  it('elegir «cada cierto número de días» llega al borrador', async () => {
    const usuario = userEvent.setup();
    const { onCambiar, borrador } = montar();

    await usuario.click(screen.getByRole('combobox', { name: 'Recordatorio automático' }));
    await usuario.click(await screen.findByRole('option', { name: 'Cada cierto número de días' }));
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, recordatorioModo: 'CadaDias' });
  });

  it('con «cada N días» muestra los días como texto del sistema y solo deja dígitos', () => {
    const { onCambiar, borrador } = montar({ recordatorioModo: 'CadaDias', recordatorioDias: '5' });
    expect(screen.getByRole('textbox', { name: 'Cada (días)' })).toHaveValue('5');
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Cada (días)'), { target: { value: '1a0' } });
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, recordatorioDias: '10' });
  });

  it('muestra el error de los días', () => {
    montar({ recordatorioModo: 'CadaDias', recordatorioDias: '0' }, [{ codigo: 'recordatorio-invalido', campo: 'recordatorio.dias' }]);
    expect(screen.getByText('Escribe un número entero de 1 a 60.')).toBeInTheDocument();
  });

  it('apagado explica que nunca recuerda solo y no muestra días', () => {
    montar({ recordatorioModo: 'Apagado' });
    expect(screen.getByText('No se le recuerda por su cuenta, aunque la empresa lo tenga encendido.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Cada (días)')).not.toBeInTheDocument();
  });
});
