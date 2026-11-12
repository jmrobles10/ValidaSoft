import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { AvisoActualizacionNoAprobada } from './AvisoActualizacionNoAprobada';

function montar(vinculo: Parameters<typeof AvisoActualizacionNoAprobada>[0]['vinculo']) {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <MemoryRouter>
        <AvisoActualizacionNoAprobada vinculo={vinculo} empresaId="emp" />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

describe('aviso de la actualización que no se aprobó (VR-13)', () => {
  it('rechazada: lo dice con la fecha, que el vínculo conserva lo aprobado y lleva al expediente', () => {
    montar({ actualizacionNoAprobada: { resultado: 'Rechazado', expedienteId: 'x1', fecha: '2026-11-12T15:00:00Z' } });

    expect(screen.getByText('La última actualización del tercero se rechazó')).toBeInTheDocument();
    expect(screen.getByText(/El 12\/11\/2026 se rechazó lo que el tercero actualizó\. El vínculo conserva lo que ya estaba aprobado/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver el expediente' })).toHaveAttribute('href', '/e/emp/expedientes/x1');
  });

  it('anulada: dice que se anuló la revisión', () => {
    montar({ actualizacionNoAprobada: { resultado: 'Anulado', expedienteId: 'x1', fecha: '2026-11-12T15:00:00Z' } });

    expect(screen.getByText('La última actualización del tercero se anuló')).toBeInTheDocument();
    expect(screen.getByText(/se anuló la revisión de lo que el tercero actualizó/)).toBeInTheDocument();
  });

  it('sin una actualización que no se aprobó no muestra nada', () => {
    const { container } = montar({ actualizacionNoAprobada: null });
    expect(container).toBeEmptyDOMElement();
  });
});
