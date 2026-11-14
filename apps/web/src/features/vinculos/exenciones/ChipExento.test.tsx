import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ExencionDeVinculoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it } from 'vitest';
import { ChipExento, ChipExentoDeFila } from './ChipExento';

function exencion(parcial: Partial<ExencionDeVinculoDto> = {}): ExencionDeVinculoDto {
  return {
    id: 'ex1',
    todos: false,
    objetivos: [{ tipo: 'Documento', nombre: 'Registro Único Tributario' }],
    justificacion: 'La renueva Contabilidad.',
    hasta: '2026-12-31',
    por: 'u1',
    creadaEn: '2026-11-10T15:00:00Z',
    estado: 'Vigente',
    ...parcial,
  };
}

const conTema = (hijo: React.ReactNode) => <ThemeProvider theme={createValidasoftTheme('light')}>{hijo}</ThemeProvider>;

describe('insignia «Exento» (EX-6)', () => {
  it('el vínculo con una exención vigente muestra «Exento» y su tooltip dice de qué y hasta cuándo', async () => {
    render(conTema(<ChipExento exenciones={[exencion()]} />));

    const chip = screen.getByText('Exento');
    await userEvent.setup().hover(chip);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Exento: Registro Único Tributario. Hasta el 31/12/2026');
  });

  it('una exención total se lee «Exento de todo»', () => {
    render(conTema(<ChipExento exenciones={[exencion({ todos: true, objetivos: [], hasta: null })]} />));

    expect(screen.getByText('Exento de todo')).toBeInTheDocument();
  });

  it('sin exenciones vigentes (revocadas, vencidas o ninguna) no muestra nada', () => {
    const { container } = render(conTema(<ChipExento exenciones={[exencion({ estado: 'Revocada' }), exencion({ id: 'ex2', estado: 'Vencida' })]} />));
    expect(container).toBeEmptyDOMElement();
    expect(render(conTema(<ChipExento exenciones={undefined} />)).container).toBeEmptyDOMElement();
  });

  it('en la fila de un vencimiento nombra el documento eximido y la fecha', async () => {
    render(conTema(<ChipExentoDeFila exencion={{ id: 'ex1', todos: false, justificacion: 'x', hasta: null }} documento="Cámara de comercio" />));

    await userEvent.setup().hover(screen.getByText('Exento'));
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Exento: Cámara de comercio. Hasta que se revoque');
  });

  it('en la fila sin exención no muestra nada', () => {
    expect(render(conTema(<ChipExentoDeFila exencion={null} documento="RUT" />)).container).toBeEmptyDOMElement();
  });
});
