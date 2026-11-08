import { FormControlLabel, Switch, ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AlineadoConCampo, medidasDeCampo } from './campos';
import { createValidasoftTheme } from './tema';
import { COLOR } from './tokens';
import { contraste } from './marcaEmpresa';

function celda(app: 'web' | 'public', conEtiqueta = true) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app })}>
      <AlineadoConCampo conEtiqueta={conEtiqueta}>
        <span data-testid="contenido">x</span>
      </AlineadoConCampo>
    </ThemeProvider>,
  );
  const contenedor = screen.getByTestId('contenido').parentElement;
  if (!contenedor) throw new Error('AlineadoConCampo no envolvió el contenido');
  return contenedor;
}

describe('AlineadoConCampo', () => {
  it('deja arriba el espacio de la etiqueta y centra en el alto del control (web 22 + 32, public 24 + 44)', () => {
    expect(medidasDeCampo('web')).toEqual({ etiqueta: 22, control: 32 });
    expect(medidasDeCampo('public')).toEqual({ etiqueta: 24, control: 44 });
    expect(celda('web')).toHaveStyle({ paddingTop: '22px', minHeight: '32px', alignItems: 'center', boxSizing: 'content-box' });
  });

  it('en filas sin etiqueta solo centra en el control', () => {
    expect(celda('public', false)).toHaveStyle({ paddingTop: '0px', minHeight: '44px' });
  });
});

describe('switch del tema', () => {
  it('apagado se lee como «no»: perilla en texto secundario con contraste suficiente sobre la pista', () => {
    for (const modo of ['light', 'dark'] as const) {
      const c = COLOR[modo];
      // La perilla apagada (texto secundario) contra la pista (superficie nivel 1): ≥ 3:1 para un control no textual.
      expect(contraste(c.textoSecundario, c.superficieNivel1)).toBeGreaterThanOrEqual(3);
      // Y el borde de la pista contra la superficie, también ≥ 3:1 en claro (en oscuro lo apoya la perilla).
      if (modo === 'light') expect(contraste(c.bordeControl, c.superficieNivel1)).toBeGreaterThanOrEqual(3);
    }
  });

  it('el switch apagado se renderiza habilitado y con su etiqueta', () => {
    render(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <FormControlLabel control={<Switch checked={false} onChange={() => undefined} />} label="Automática" />
      </ThemeProvider>,
    );
    const interruptor = screen.getByRole('checkbox', { name: 'Automática' });
    expect(interruptor).not.toBeChecked();
    expect(interruptor).toBeEnabled();
  });
});
