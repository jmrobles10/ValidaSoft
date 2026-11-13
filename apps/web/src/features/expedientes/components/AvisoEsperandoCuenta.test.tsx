import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { AvisoEsperandoCuenta } from './AvisoEsperandoCuenta';

describe('AvisoEsperandoCuenta', () => {
  it('dice que el expediente espera la cuenta bancaria designada y lleva al vínculo para aprobarla', () => {
    render(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <MemoryRouter>
          <AvisoEsperandoCuenta empresaId="emp-1" vinculoId="vin-9" />
        </MemoryRouter>
      </ThemeProvider>,
    );

    const aviso = screen.getByRole('alert');
    expect(aviso).toHaveTextContent('Falta la cuenta bancaria designada');
    expect(aviso).toHaveTextContent(/dos personas de la empresa aprueben la cuenta/);
    expect(aviso).toHaveTextContent(/si la rechazan, sigue esperando/);
    expect(screen.getByRole('link', { name: 'Ver la cuenta' })).toHaveAttribute('href', '/e/emp-1/vinculos/vin-9');
  });
});
