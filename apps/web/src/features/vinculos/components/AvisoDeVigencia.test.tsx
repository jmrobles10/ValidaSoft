import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it } from 'vitest';
import { AvisoDeVigencia } from './AvisoDeVigencia';

function montar(vinculo: Parameters<typeof AvisoDeVigencia>[0]['vinculo']) {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <AvisoDeVigencia vinculo={vinculo} />
    </ThemeProvider>,
  );
}

describe('aviso de la vigencia del vínculo', () => {
  it('por vencer: dice cuándo vence, en la fecha de calendario (sin correrla por zona horaria)', () => {
    montar({ estado: 'PorVencer', vigenteHasta: '2027-11-06', expedienteAbiertoId: null });
    expect(screen.getByText('La validación de este tercero vence pronto')).toBeInTheDocument();
    expect(screen.getByText(/Vence el 06\/11\/2027\./)).toBeInTheDocument();
  });

  it('vencido con la renovación abierta: lo dice; sin renovación, también', () => {
    const { unmount } = montar({ estado: 'Vencido', vigenteHasta: '2027-11-06', expedienteAbiertoId: 'e1' });
    expect(screen.getByText('La validación de este tercero venció')).toBeInTheDocument();
    expect(screen.getByText(/Tiene una renovación abierta para ratificar o actualizar su información\./)).toBeInTheDocument();
    unmount();
    montar({ estado: 'Vencido', vigenteHasta: '2027-11-06', expedienteAbiertoId: null });
    expect(screen.getByText('Venció el 06/11/2027. Aún no se abre su renovación.')).toBeInTheDocument();
  });

  it('un vínculo vencido por un documento no inventa una fecha de vigencia', () => {
    montar({ estado: 'Vencido', vigenteHasta: null, expedienteAbiertoId: 'e1' });
    expect(screen.getByText('La validación de este tercero venció')).toBeInTheDocument();
    expect(screen.queryByText(/Venció el/)).not.toBeInTheDocument();
  });

  it('activo, bloqueado o por vencer sin fecha no muestra nada', () => {
    const { container } = montar({ estado: 'Activo', vigenteHasta: '2027-11-06', expedienteAbiertoId: null });
    expect(container).toBeEmptyDOMElement();
  });
});
