import { Alert, Box, Button, IconButton, List, Skeleton, Stack, Tooltip, Typography } from '@mui/material';
import { BellOff, Settings } from 'lucide-react';
import type { ReactNode } from 'react';
import { ItemDeNotificacion, type MarcaDeNotificacion, type NotificacionVisible } from './ItemDeNotificacion';
import { useAhora } from './formato';
import { ErrorEnLinea, EstadoVacio } from '../patrones';

/** Los textos del panel: este paquete no conoce i18n, cada app los entrega ya traducidos. */
export interface TextosDelPanelDeNotificaciones {
  titulo: string;
  marcarTodas: string;
  preferencias: string;
  cargando: string;
  reintentar: string;
  vacio: string;
  vacioAyuda: string;
  /** Aviso al pie cuando la bandeja es solo la primera página. */
  hayMas: string;
  /** Aviso de que el stream no está disponible y la lista se refresca cada tanto; sin él no se muestra. */
  sinConexion?: string;
  /** Lo que leen los lectores de pantalla antes del título de una no leída. */
  sinLeer: string;
}

export interface PanelDeNotificacionesProps<N extends NotificacionVisible> {
  bandeja: { datos: N[]; noLeidas: number; siguienteCursor: string | null } | undefined;
  cargando: boolean;
  error: string | null;
  textos: TextosDelPanelDeNotificaciones;
  /** El stream no está disponible y la lista se refresca cada tanto (muestra `textos.sinConexion`). */
  respaldo?: boolean;
  marcandoTodas?: boolean;
  /**
   * Ícono de «Marcar todas» (p. ej. un spinner mientras se marca). Con ícono, a 375 px el título, la acción y el engranaje caben
   * en una fila solo sin él; sin ícono, el botón conserva 32 px de alto.
   */
  iconoMarcarTodas?: ReactNode;
  /** Alto de cada fila del esqueleto de carga (en px). */
  alturaEsqueleto: number;
  /** Marca de la empresa de cada aviso (portal del tercero); sin ella, el punto de «sin leer» va primero. */
  marcaDe?: (notificacion: N) => MarcaDeNotificacion;
  onReintentar: () => void;
  onAbrir: (notificacion: N) => void;
  onMarcarTodas: () => void;
  onPreferencias: () => void;
}

/** Contenido del panel de la campana: cabecera con acciones, lista (o carga, error o vacío) y avisos al pie. */
export function PanelDeNotificaciones<N extends NotificacionVisible>(props: PanelDeNotificacionesProps<N>) {
  const { bandeja, cargando, error, textos, respaldo = false, marcandoTodas = false, iconoMarcarTodas, alturaEsqueleto, marcaDe } = props;
  const { onReintentar, onAbrir, onMarcarTodas, onPreferencias } = props;
  const ahora = useAhora();
  const noLeidas = bandeja?.noLeidas ?? 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flex: 'none', pl: 2, pr: 1, py: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Typography id="titulo-notificaciones" component="h2" sx={{ flex: 1, fontSize: 15, lineHeight: '22px', fontWeight: 600 }}>
          {textos.titulo}
        </Typography>
        <Button
          size="small"
          variant="text"
          onClick={onMarcarTodas}
          disabled={noLeidas === 0 || marcandoTodas}
          startIcon={iconoMarcarTodas}
          sx={{ whiteSpace: 'nowrap', ...(iconoMarcarTodas ? { '& .MuiButton-startIcon': { display: { xs: 'none', sm: 'inherit' } } } : { minHeight: 32 }) }}
        >
          {textos.marcarTodas}
        </Button>
        <Tooltip title={textos.preferencias}>
          <IconButton size="small" aria-label={textos.preferencias} onClick={onPreferencias}>
            <Settings size={18} strokeWidth={1.75} />
          </IconButton>
        </Tooltip>
      </Stack>
      <Box sx={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
        {respaldo && textos.sinConexion && (
          <Alert severity="info" sx={{ borderRadius: 0 }}>
            {textos.sinConexion}
          </Alert>
        )}
        {cargando && !bandeja ? (
          <Stack spacing={1.5} sx={{ p: 2 }} aria-label={textos.cargando} role="status">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} variant="rounded" height={alturaEsqueleto} />
            ))}
          </Stack>
        ) : error && !bandeja ? (
          <Box sx={{ p: 2 }}>
            <ErrorEnLinea mensaje={error} onReintentar={onReintentar} textoReintentar={textos.reintentar} />
          </Box>
        ) : !bandeja || bandeja.datos.length === 0 ? (
          <EstadoVacio compacto icono={BellOff} titulo={textos.vacio} texto={textos.vacioAyuda} />
        ) : (
          <List disablePadding aria-labelledby="titulo-notificaciones">
            {bandeja.datos.map((n) => {
              const marca = marcaDe?.(n);
              return <ItemDeNotificacion key={n.id} notificacion={n} ahora={ahora} onAbrir={onAbrir} textoSinLeer={textos.sinLeer} {...(marca ? { marca } : {})} />;
            })}
          </List>
        )}
        {bandeja?.siguienteCursor && (
          <Typography variant="caption" component="p" sx={{ color: 'text.secondary', px: 2, py: 1.5, textAlign: 'center' }}>
            {textos.hayMas}
          </Typography>
        )}
      </Box>
    </Box>
  );
}
