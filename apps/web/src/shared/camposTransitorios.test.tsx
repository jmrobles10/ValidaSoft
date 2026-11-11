import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createValidasoftTheme } from '@validasoft/ui';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { CampoFecha, CampoNumero } from './camposTransitorios';
import { isoDeTexto, mascaraDeFecha, textoDeIso } from './fechaEnTexto';

function Fecha({ minimo, maximo }: { minimo?: string; maximo?: string }) {
  const [valor, setValor] = useState('');
  return (
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <CampoFecha etiqueta="Fecha" valor={valor} onCambiar={setValor} minimo={minimo} maximo={maximo} />
      <output aria-label="iso">{valor}</output>
    </ThemeProvider>
  );
}

describe('Fecha en texto dd/mm/aaaa', () => {
  it('convierte entre ISO y texto, y rechaza las fechas que no existen', () => {
    expect(textoDeIso('2026-11-05')).toBe('05/11/2026');
    expect(textoDeIso('2026-11-05T10:00:00Z')).toBe('05/11/2026');
    expect(textoDeIso('')).toBe('');
    expect(isoDeTexto('05/11/2026')).toBe('2026-11-05');
    expect(isoDeTexto('31/02/2026')).toBe('');
    expect(isoDeTexto('05/11/26')).toBe('');
    expect(mascaraDeFecha('0511')).toBe('05/11');
    expect(mascaraDeFecha('05112026999')).toBe('05/11/2026');
    expect(mascaraDeFecha('ab1c')).toBe('1');
  });

  it('el campo pone las barras al escribir y emite ISO solo con la fecha completa y real', async () => {
    render(<Fecha />);
    const campo = screen.getByRole('textbox', { name: 'Fecha' });
    await userEvent.type(campo, '0511');
    expect(campo).toHaveValue('05/11');
    expect(screen.getByLabelText('iso')).toHaveTextContent('');
    await userEvent.type(campo, '2026');
    expect(campo).toHaveValue('05/11/2026');
    expect(screen.getByLabelText('iso')).toHaveTextContent('2026-11-05');
    await userEvent.clear(campo);
    await userEvent.type(campo, '31022026');
    expect(screen.getByLabelText('iso')).toHaveTextContent('');
    await userEvent.tab();
    expect(screen.getByText('Escribe una fecha válida (dd/mm/aaaa).')).toBeInTheDocument();
  });

  it('avisa si la fecha queda fuera del rango', async () => {
    render(<Fecha minimo="2026-10-12" maximo="2026-11-11" />);
    const campo = screen.getByRole('textbox', { name: 'Fecha' });
    await userEvent.type(campo, '01012026');
    await userEvent.tab();
    expect(screen.getByText('La fecha está fuera del rango permitido.')).toBeInTheDocument();
  });
});

function Numero() {
  const [valor, setValor] = useState('');
  return (
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <CampoNumero etiqueta="Meses" valor={valor} onCambiar={setValor} maxDigitos={2} />
    </ThemeProvider>
  );
}

describe('Número entero', () => {
  it('solo deja dígitos y respeta el máximo', async () => {
    render(<Numero />);
    const campo = screen.getByRole('textbox', { name: 'Meses' });
    await userEvent.type(campo, '1a2b3');
    expect(campo).toHaveValue('12');
  });
});
