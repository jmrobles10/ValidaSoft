import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router';
import { Avisos } from '../shared/avisos';

/** Una pantalla con el tema, TanStack Query (sin reintentos), un enrutador en memoria y los avisos (Snackbar), como la monta la app. */
export function montarPantalla(pantalla: ReactNode, ruta = '/') {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>{pantalla}</MemoryRouter>
        <Avisos />
      </QueryClientProvider>
    </ThemeProvider>,
  );
}
