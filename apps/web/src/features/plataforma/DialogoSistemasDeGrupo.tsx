import { Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, Typography } from '@mui/material';
import type { SistemaIntegradoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { Ban, Play } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, ErrorRecuperable } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useCambiarSistemaDeGrupo, useSistemasDeGrupo } from './apiIdentidad';

interface Cambio {
  sistema: SistemaIntegradoDto;
  desactivar: boolean;
}

function FilaSistema({ sistema, alCambiar }: { sistema: SistemaIntegradoDto; alCambiar: (cambio: Cambio) => void }) {
  const { t } = useTranslation();
  return (
    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', py: 1.25, borderTop: 1, borderColor: 'divider', '&:first-of-type': { borderTop: 0 } }}>
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography sx={{ fontWeight: 600 }}>{sistema.nombre}</Typography>
          <Chip size="small" variant="outlined" label={sistema.codigo} sx={{ fontFamily: (th) => th.typography.mono.fontFamily }} />
          <Chip size="small" variant="outlined" color={sistema.activo ? 'success' : 'default'} label={sistema.activo ? t('comun.activo') : t('comun.inactivo')} />
        </Stack>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('consola.sistemas.detalle', { fecha: formatearFecha(sistema.creadoEn), count: sistema.origenesPermitidos.length })}
        </Typography>
      </Box>
      <Button
        size="small"
        variant="outlined"
        color={sistema.activo ? 'error' : 'primary'}
        startIcon={sistema.activo ? <Ban size={14} aria-hidden /> : <Play size={14} aria-hidden />}
        aria-label={t(sistema.activo ? 'consola.sistemas.desactivarA' : 'consola.sistemas.reactivarA', { nombre: sistema.nombre })}
        onClick={() => alCambiar({ sistema, desactivar: sistema.activo })}
      >
        {sistema.activo ? t('comun.desactivar') : t('consola.sistemas.reactivar')}
      </Button>
    </Stack>
  );
}

/**
 * Los sistemas integrados de un grupo (PI-7): desactivar uno cierra de inmediato el acceso de sus llaves y de los tokens que emitió, y reactivarlo no revive lo
 * anterior (las llaves revocadas siguen revocadas y los tokens vencen). Cada cambio pide confirmar con la passkey.
 */
export function DialogoSistemasDeGrupo({ tenantId, grupo, onCerrar }: { tenantId: string; grupo: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const sistemas = useSistemasDeGrupo(tenantId, true);
  const cambiar = useCambiarSistemaDeGrupo(tenantId);
  const [pendiente, setPendiente] = useState<Cambio | null>(null);

  const confirmar = () => {
    if (!pendiente) return;
    const { sistema, desactivar } = pendiente;
    cambiar.mutate(
      { sistemaId: sistema.id, desactivar },
      {
        onSuccess: () => mostrar(t(desactivar ? 'consola.sistemas.desactivado' : 'consola.sistemas.reactivado', { nombre: sistema.nombre })),
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
        onSettled: () => setPendiente(null),
      },
    );
  };

  return (
    <Dialog open onClose={onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 600, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-sistemas-grupo">
      <DialogTitle id="titulo-sistemas-grupo">
        {t('consola.sistemas.titulo')}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
          {grupo}
        </Typography>
      </DialogTitle>
      <DialogContent>
        <Alert severity="info" sx={{ mb: 1.5 }}>
          {t('consola.sistemas.ayuda')}
        </Alert>
        {sistemas.isError && <ErrorRecuperable mensaje={mensajeDeError(sistemas.error, t)} onReintentar={() => void sistemas.refetch()} />}
        {sistemas.isPending && <Skeleton variant="rounded" height={72} role="status" aria-busy aria-label={t('comun.cargando')} />}
        {sistemas.data?.length === 0 && (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('consola.sistemas.vacio')}
          </Typography>
        )}
        {sistemas.data?.map((s) => (
          <FilaSistema key={s.id} sistema={s} alCambiar={setPendiente} />
        ))}
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} sx={{ color: 'text.primary' }}>
          {t('comun.cerrar')}
        </Button>
      </DialogActions>
      {pendiente && (
        <DialogoConfirmacion
          titulo={t(pendiente.desactivar ? 'consola.sistemas.desactivarTitulo' : 'consola.sistemas.reactivarTitulo', { nombre: pendiente.sistema.nombre })}
          subtitulo={pendiente.sistema.codigo}
          mensaje={t(pendiente.desactivar ? 'consola.sistemas.desactivarAyuda' : 'consola.sistemas.reactivarAyuda')}
          confirmar={pendiente.desactivar ? t('comun.desactivar') : t('consola.sistemas.reactivar')}
          tono={pendiente.desactivar ? 'error' : 'primary'}
          ocupado={cambiar.isPending}
          onConfirmar={confirmar}
          onCerrar={() => setPendiente(null)}
        />
      )}
    </Dialog>
  );
}
