import { Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Link, Skeleton, Stack, Typography } from '@mui/material';
import { ErrorEnLinea, PaginaEstado } from '@validasoft/ui';
import { ChevronLeft, Lock } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';

/** Página 403 propia: dice de quién es la sección y a quién pedir acceso (nunca "Forbidden"). */
export function SinPermiso({ seccion }: { seccion: string }) {
  const { t } = useTranslation();
  return <PaginaEstado icono={Lock} titulo={t('sinPermiso.titulo', { seccion })} texto={t('sinPermiso.ayuda')} rol="alert" />;
}

/** Error recuperable inline con reintento. */
export function ErrorRecuperable({ mensaje, onReintentar }: { mensaje: string; onReintentar?: () => void }) {
  const { t } = useTranslation();
  return <ErrorEnLinea mensaje={mensaje} onReintentar={onReintentar} textoReintentar={t('comun.reintentar')} />;
}

/** Enlace "‹ Vínculos" sobre el título de un detalle. */
export function EnlaceVolver({ a, children }: { a: string; children: ReactNode }) {
  return (
    <Link
      component={RouterLink}
      to={a}
      relative="path"
      sx={{ display: 'inline-flex', alignItems: 'center', gap: '2px', mb: 0.75, ml: -0.5, fontSize: 13 }}
    >
      <ChevronLeft size={16} strokeWidth={1.75} aria-hidden />
      {children}
    </Link>
  );
}

/** Título de página (24/32) con subtítulo y acciones alineadas abajo a la derecha. */
export function EncabezadoPagina({ titulo, subtitulo, acciones }: { titulo: ReactNode; subtitulo?: ReactNode; acciones?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'flex-end' }} justifyContent="space-between" sx={{ mb: 2 }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="h2" component="h1">
          {titulo}
        </Typography>
        {subtitulo && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
            {subtitulo}
          </Typography>
        )}
      </Box>
      {acciones && (
        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
          {acciones}
        </Stack>
      )}
    </Stack>
  );
}

/** Tarjeta de sección: borde sutil, radio 12, sin sombra. */
export function Tarjeta({ titulo, acciones, children, etiqueta }: { titulo?: ReactNode; acciones?: ReactNode; children: ReactNode; etiqueta?: string }) {
  return (
    <Box component="section" aria-label={etiqueta} sx={{ p: 2, border: 1, borderColor: 'divider', borderRadius: (t) => `${String(t.radio.md)}px`, bgcolor: 'background.paper', minWidth: 0 }}>
      {(titulo ?? acciones) && (
        <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={1} sx={{ mb: 1.5 }}>
          {titulo && (
            <Typography variant="h4" component="h2">
              {titulo}
            </Typography>
          )}
          {acciones}
        </Stack>
      )}
      {children}
    </Box>
  );
}

/** Esqueleto de una línea de tiempo mientras carga el historial. */
export function EsqueletoHistorial() {
  return (
    <Stack spacing={2} aria-busy>
      {[0, 1, 2].map((i) => (
        <Stack key={i} direction="row" spacing={1.5}>
          <Skeleton variant="circular" width={28} height={28} />
          <Box sx={{ flex: 1 }}>
            <Skeleton variant="rounded" height={14} width="60%" sx={{ mb: 1 }} />
            <Skeleton variant="rounded" height={12} width="40%" />
          </Box>
        </Stack>
      ))}
    </Stack>
  );
}

/** Spinner solo dentro del botón que ejecuta la acción. */
export function IconoCarga() {
  return <CircularProgress size={14} color="inherit" aria-hidden />;
}

/** Confirmación propia (window.confirm no funciona en iframes con sandbox). Dice qué pasa, no "¿Está seguro?". */
export function DialogoConfirmacion({
  titulo,
  subtitulo,
  mensaje,
  confirmar,
  icono,
  tono = 'error',
  ocupado = false,
  onConfirmar,
  onCerrar,
}: {
  titulo: string;
  subtitulo?: ReactNode;
  mensaje: ReactNode;
  confirmar: string;
  icono?: ReactNode;
  /** `error` para lo destructivo (por defecto); `primary` para confirmar algo que no destruye (publicar). */
  tono?: 'error' | 'primary';
  ocupado?: boolean;
  onConfirmar: () => void;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 440, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-confirmacion">
      <DialogTitle id="titulo-confirmacion">
        {titulo}
        {subtitulo && (
          <Typography variant="body2" color="text.secondary" component="span" sx={{ display: 'block', mt: 0.25, fontWeight: 400 }}>
            {subtitulo}
          </Typography>
        )}
      </DialogTitle>
      <DialogContent>
        <Typography>{mensaje}</Typography>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" color={tono} onClick={onConfirmar} disabled={ocupado} startIcon={ocupado ? <IconoCarga /> : icono}>
          {confirmar}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
