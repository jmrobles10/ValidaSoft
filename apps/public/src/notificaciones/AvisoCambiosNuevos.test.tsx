import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createValidasoftTheme, AvisoCambiosNuevos } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';

describe('«Hay cambios nuevos»', () => {
  it('dice qué pasó, deja actualizar con un botón y no se esconde para el lector de pantalla', async () => {
    const usuario = userEvent.setup();
    const actualizar = vi.fn();
    render(
      <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
        <AvisoCambiosNuevos titulo="Hay cambios nuevos." texto="Esta información cambió mientras la editabas." accion="Actualizar" onActualizar={actualizar} />
      </ThemeProvider>,
    );

    const aviso = screen.getByRole('status');
    expect(aviso).toHaveTextContent('Hay cambios nuevos. Esta información cambió mientras la editabas.');
    await usuario.click(screen.getByRole('button', { name: 'Actualizar' }));
    expect(actualizar).toHaveBeenCalledOnce();
  });
});
