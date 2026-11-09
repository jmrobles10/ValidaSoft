import { CssBaseline, ThemeProvider } from '@mui/material';
import { QueryClientProvider } from '@tanstack/react-query';
import { configurarCliente } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { useEffect, useMemo, useRef, useState } from 'react';
import { RouterProvider } from 'react-router';
import { PantallaSesion } from '../sesion/PantallaSesion';
import { useSesion } from '../sesion/sesionStore';
import { usePuenteEmbed } from '../sesion/usePuenteEmbed';
import { Avisos } from '../shared/avisos';
import { crearClienteConsultas } from '../shared/consultas';
import { usePreferencias } from '../shared/preferencias';
import { crearRouter } from './router';

// El token se lee en cada petición; un 401 de la API marca la sesión como vencida y se pide otra al contenedor.
configurarCliente({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
  obtenerToken: () => useSesion.getState().sesion?.token ?? null,
  alNoAutenticado: () => useSesion.getState().marcar('vencida'),
});

export function App() {
  const modo = usePreferencias((s) => s.modo);
  const tema = useMemo(() => createValidasoftTheme(modo, { app: 'web' }), [modo]);
  const [clienteConsultas] = useState(crearClienteConsultas);
  const [router] = useState(crearRouter);
  const { embebida } = usePuenteEmbed();
  const estado = useSesion((s) => s.estado);
  const usuarioId = useSesion((s) => s.sesion?.usuarioId ?? null);
  const usuarioAnterior = useRef(usuarioId);

  // Otra persona (o ninguna): nada de la caché anterior debe verse. Solo al cambiar de persona o salir: con la primera
  // sesión (de nadie a alguien) la página ya lanzó sus consultas y vaciar la caché las cancelaba (se quedaba en
  // «Cargando…» en el build de producción).
  useEffect(() => {
    if (usuarioAnterior.current !== null && usuarioAnterior.current !== usuarioId) clienteConsultas.clear();
    usuarioAnterior.current = usuarioId;
  }, [usuarioId, clienteConsultas]);

  return (
    <ThemeProvider theme={tema}>
      <CssBaseline enableColorScheme />
      <QueryClientProvider client={clienteConsultas}>
        {estado === 'activa' ? <RouterProvider router={router} /> : <PantallaSesion estado={estado} embebida={embebida} />}
        <Avisos />
      </QueryClientProvider>
    </ThemeProvider>
  );
}
