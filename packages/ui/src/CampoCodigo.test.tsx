import { ThemeProvider } from '@mui/material';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { CampoCodigo, normalizarCodigoNumerico } from './CampoCodigo';
import { createValidasoftTheme } from './tema';

function Controlado({ inicial = '', alCambiar }: { inicial?: string; alCambiar?: (valor: string) => void }) {
  const [valor, setValor] = useState(inicial);
  return (
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'web' })}>
      <CampoCodigo
        valor={valor}
        onCambiar={(nuevo) => {
          setValor(nuevo);
          alCambiar?.(nuevo);
        }}
        etiqueta="Código de 6 dígitos"
        etiquetaDigito={(n) => `Dígito ${String(n)} de 6`}
      />
    </ThemeProvider>
  );
}

const casilla = (n: number) => screen.getByLabelText(`Dígito ${String(n)} de 6`);

describe('normalizarCodigoNumerico', () => {
  it('deja solo dígitos y recorta al largo', () => {
    expect(normalizarCodigoNumerico(' 481 72-9 99')).toBe('481729');
    expect(normalizarCodigoNumerico('12ab34', 3)).toBe('123');
    expect(normalizarCodigoNumerico('')).toBe('');
  });
});

describe('CampoCodigo', () => {
  it('tiene seis casillas dentro de un grupo con nombre', () => {
    render(<Controlado />);
    expect(screen.getByRole('group', { name: 'Código de 6 dígitos' })).toBeInTheDocument();
    for (let n = 1; n <= 6; n += 1) expect(casilla(n)).toBeInTheDocument();
    expect(casilla(1)).toHaveAttribute('autocomplete', 'one-time-code');
    expect(casilla(2)).toHaveAttribute('autocomplete', 'off');
    expect(casilla(1)).toHaveAttribute('inputmode', 'numeric');
  });

  it('escribir un dígito pasa a la casilla siguiente y los símbolos se ignoran', async () => {
    const cambios: string[] = [];
    render(<Controlado alCambiar={(v) => cambios.push(v)} />);
    await userEvent.click(casilla(1));
    await userEvent.keyboard('4a8');
    expect(cambios.at(-1)).toBe('48');
    expect(casilla(3)).toHaveFocus();
  });

  it('pegar o autocompletar reparte los dígitos desde la casilla', () => {
    const cambios: string[] = [];
    render(<Controlado alCambiar={(v) => cambios.push(v)} />);
    fireEvent.paste(casilla(1), { clipboardData: { getData: () => '481 729' } });
    expect(cambios.at(-1)).toBe('481729');
    for (const [i, d] of Array.from('481729').entries()) expect(casilla(i + 1)).toHaveValue(d);
  });

  it('retroceso en una casilla vacía vuelve a la anterior y la borra', async () => {
    const cambios: string[] = [];
    render(<Controlado inicial="12" alCambiar={(v) => cambios.push(v)} />);
    await userEvent.click(casilla(3));
    await userEvent.keyboard('{Backspace}');
    expect(cambios.at(-1)).toBe('1');
    expect(casilla(2)).toHaveFocus();
  });

  it('marca el error en cada casilla y se puede deshabilitar', () => {
    render(
      <ThemeProvider theme={createValidasoftTheme('light', { app: 'web' })}>
        <CampoCodigo valor="" onCambiar={() => undefined} etiqueta="Código" etiquetaDigito={(n) => `Dígito ${String(n)} de 6`} error deshabilitado />
      </ThemeProvider>,
    );
    expect(casilla(1)).toHaveAttribute('aria-invalid', 'true');
    expect(casilla(1)).toBeDisabled();
  });
});
