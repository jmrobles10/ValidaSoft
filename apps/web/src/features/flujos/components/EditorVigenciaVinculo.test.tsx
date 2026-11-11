import { ThemeProvider } from '@mui/material';
import { fireEvent, render, screen } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import { borradorVacio, type ErrorFlujo } from '../editor';
import { EditorVigenciaVinculo } from './EditorVigenciaVinculo';

function montar(cambios: Partial<ReturnType<typeof borradorVacio>> = {}, errores: ErrorFlujo[] = [], soloLectura = false) {
  const onCambiar = vi.fn();
  const borrador = { ...borradorVacio(), ...cambios };
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <EditorVigenciaVinculo borrador={borrador} onCambiar={onCambiar} errores={errores} soloLectura={soloLectura} />
    </ThemeProvider>,
  );
  return { onCambiar, borrador };
}

describe('EditorVigenciaVinculo', () => {
  it('sin vigencia (0 días) deja los avisos y la gracia inactivos y explica que no vence por tiempo', () => {
    montar();
    expect(screen.getByText('Vigencia del vínculo')).toBeInTheDocument();
    expect(screen.getByLabelText('Días')).toHaveValue('0');
    expect(screen.getByText('0 = sin vencimiento periódico.')).toBeInTheDocument();
    expect(screen.getByLabelText('Avisos (días antes de vencer)')).toBeDisabled();
    expect(screen.getByLabelText('Gracia (días)')).toBeDisabled();
  });

  it('con vigencia activa los avisos y la gracia, y cada cambio llega completo al borrador', () => {
    const { onCambiar, borrador } = montar({ vigenciaDias: '365' });
    expect(screen.getByLabelText('Avisos (días antes de vencer)')).toBeEnabled();

    fireEvent.change(screen.getByLabelText('Avisos (días antes de vencer)'), { target: { value: '60, 30' } });
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, vigenciaAvisos: '60, 30' });
    fireEvent.change(screen.getByLabelText('Gracia (días)'), { target: { value: '10' } });
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, vigenciaGracia: '10' });
    fireEvent.change(screen.getByLabelText('Días'), { target: { value: '180' } });
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, vigenciaDias: '180' });
  });

  it('los días son un campo de texto del sistema (no el numérico del navegador) y solo dejan dígitos', () => {
    const { onCambiar, borrador } = montar({ vigenciaDias: '365' });
    expect(screen.getByRole('textbox', { name: 'Días' })).toHaveAttribute('inputmode', 'numeric');
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Días'), { target: { value: '1a8-0' } });
    expect(onCambiar).toHaveBeenLastCalledWith({ ...borrador, vigenciaDias: '180' });
  });

  it('muestra el error de cada campo', () => {
    montar({ vigenciaDias: '30', vigenciaAvisos: '30' }, [
      { codigo: 'aviso-vigencia-invalido', campo: 'vigencia.avisos' },
      { codigo: 'vigencia-sin-secciones', campo: 'vigencia.dias' },
    ]);
    expect(screen.getByText('Hasta 5 avisos, cada uno entre 1 y los días de vigencia menos uno.')).toBeInTheDocument();
    expect(screen.getByText('Con vigencia, el flujo debe pedir al menos una sección de la información del tercero.')).toBeInTheDocument();
  });

  it('en solo lectura no se edita nada', () => {
    montar({ vigenciaDias: '365' }, [], true);
    expect(screen.getByLabelText('Días')).toBeDisabled();
    expect(screen.getByLabelText('Avisos (días antes de vencer)')).toBeDisabled();
  });
});
