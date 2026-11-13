import { ThemeProvider } from '@mui/material';
import { act, render, screen } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it } from 'vitest';
import { Avisos } from './avisos';
import { useAvisos } from './useAvisos';

const montar = () =>
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <Avisos />
    </ThemeProvider>,
  );

afterEach(() => act(() => useAvisos.getState().cerrar()));

describe('avisos', () => {
  it('un aviso de éxito común sale con su ícono de siempre, sin la animación de marca', () => {
    const { container } = montar();
    act(() => useAvisos.getState().mostrar('Guardado.'));
    expect(screen.getByRole('alert')).toHaveTextContent('Guardado.');
    expect(useAvisos.getState().actual?.celebrar).toBe(false);
    expect(container.ownerDocument.querySelector('.MuiAlert-filled')).not.toBeNull();
    expect(container.ownerDocument.querySelector('svg g.pop')).toBeNull();
  });

  it('un aviso que celebra lleva la animación de éxito de marca en lugar del ícono y la superficie de la página', () => {
    montar();
    act(() => useAvisos.getState().mostrar('Expediente aprobado.', 'success', { celebrar: true }));
    expect(screen.getByRole('alert')).toHaveTextContent('Expediente aprobado.');
    expect(document.querySelector('.MuiAlert-outlined svg g.pop')).not.toBeNull();
    expect(document.querySelector('.MuiAlert-filled')).toBeNull();
  });

  it('solo un aviso de éxito puede celebrar: un error con `celebrar` sale como error', () => {
    montar();
    act(() => useAvisos.getState().mostrar('No se pudo.', 'error', { celebrar: true }));
    expect(useAvisos.getState().actual?.celebrar).toBe(false);
    expect(document.querySelector('svg g.pop')).toBeNull();
  });
});
