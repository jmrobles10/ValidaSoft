import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { expedienteConSecciones, FINANCIERA_PENDIENTE, GENERAL_OBSERVADA } from './pruebasSeccion';
import { RequisitosDelExpediente } from './RequisitosDelExpediente';

function montar(soloLectura = false) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <MemoryRouter>
        <RequisitosDelExpediente expediente={expedienteConSecciones([FINANCIERA_PENDIENTE, GENERAL_OBSERVADA])} vinculoId="v1" soloLectura={soloLectura} />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

const tarjeta = (nombre: string) => {
  const li = screen.getByRole('heading', { name: nombre }).closest('li');
  if (!li) throw new Error(`falta la tarjeta de ${nombre}`);
  return within(li);
};

describe('Sección de «Mi información» en el expediente del tercero (F2)', () => {
  it('observada: muestra los motivos, la nota y los datos señalados, y lleva a corregirla en el wizard', () => {
    montar();
    const general = tarjeta('Información general');
    expect(general.getByText('Observado')).toBeInTheDocument();
    expect(general.getByText('Un dato no coincide con el soporte.')).toBeInTheDocument();
    expect(general.getByText('“La razón social no coincide con el RUT.”')).toBeInTheDocument();
    expect(general.getByText('Datos por corregir: Razón social, Dirección')).toBeInTheDocument();
    expect(general.getByRole('link', { name: 'Corregir en Mi información' })).toHaveAttribute('href', '/ficha/general');
    // Una sección no se sube como documento.
    expect(general.queryByRole('link', { name: /Subir/ })).not.toBeInTheDocument();
  });

  it('pendiente: dice lo que le falta para la empresa y lleva al wizard del periodo pedido', () => {
    montar();
    const financiera = tarjeta('Información financiera 2025');
    expect(financiera.getByText('Lo que le falta para Constructora Norte')).toBeInTheDocument();
    expect(financiera.getByText('Falta el soporte de la información financiera.')).toBeInTheDocument();
    expect(financiera.getByText('Aún no la diligencias')).toBeInTheDocument();
    expect(financiera.getByRole('link', { name: 'Completar en Mi información' })).toHaveAttribute('href', '/ficha/financiera?periodo=2025');
  });

  it('en solo lectura no ofrece ir al wizard', () => {
    montar(true);
    expect(screen.queryByRole('link', { name: /en Mi información/ })).not.toBeInTheDocument();
  });
});
