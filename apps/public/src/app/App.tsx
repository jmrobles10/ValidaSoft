import { CssBaseline, ThemeProvider } from '@mui/material';
import { QueryClientProvider } from '@tanstack/react-query';
import { configurarCliente } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { useEffect, useState } from 'react';
import { RouterProvider } from 'react-router';
import { envolverConRenovacion } from '../sesion/renovacion';
import { useSesionTercero } from '../sesion/sesionTercero';
import { crearClienteConsultas } from '../shared/api';
import { Avisos } from '../shared/avisos';
import { crearRouter } from './router';

// Las rutas `/public/v1` solo aceptan la sesión del tercero (Bearer) y no usan empresa activa. Un 401 se intenta
// resolver renovando con la cookie (una vez); si tampoco, la sesión se da por terminada.
configurarCliente({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
  obtenerToken: () => useSesionTercero.getState().sesion?.accessToken ?? null,
  obtenerEmpresa: () => null,
  alNoAutenticado: () => useSesionTercero.getState().vencer(),
  fetch: envolverConRenovacion((...argumentos) => globalThis.fetch(...argumentos), () => useSesionTercero.getState().renovarAhora()),
});

// Al recargar, la cookie de la sesión (si sigue vigente) devuelve a la persona adentro sin pedirle el enlace otra vez.
void useSesionTercero.getState().restaurar();

// Tema del tercero: densidad cómoda, controles de 44 px y radios de 18 en tarjetas y diálogos. Claro por defecto.
const tema = createValidasoftTheme('light', { app: 'public' });

export function App() {
  const [clienteConsultas] = useState(crearClienteConsultas);
  const [router] = useState(crearRouter);
  const terceroId = useSesionTercero((s) => s.sesion?.terceroId ?? null);

  // Otra persona (o ninguna): nada de la caché anterior debe verse.
  useEffect(() => {
    clienteConsultas.clear();
  }, [terceroId, clienteConsultas]);

  return (
    <ThemeProvider theme={tema}>
      <CssBaseline enableColorScheme />
      <QueryClientProvider client={clienteConsultas}>
        <RouterProvider router={router} />
        <Avisos />
      </QueryClientProvider>
    </ThemeProvider>
  );
}
