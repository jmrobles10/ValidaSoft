import { Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Skeleton, Stack, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { generarCodigosRecuperacion, getObtenerEstadoCodigosRecuperacionQueryOptions, type CodigosDeRecuperacionDto } from '@validasoft/api-client';
import { formatearFechaHora } from '@validasoft/ui';
import { Copy, LifeBuoy, Printer } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeIngreso } from '../../ingreso/mensajes';
import { DialogoConfirmacion, ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { invalidarRuta } from '../../shared/consultas';
import { useAvisos } from '../../shared/useAvisos';
import { imprimirCodigos, textoDeCodigos } from './codigosDeRecuperacion';

const RUTA = '/api/v1/mi-cuenta/codigos-de-recuperacion';
const SIN_EMPRESA = { empresa: null } as const;

/** Los diez códigos, una sola vez: hay que copiarlos, imprimirlos o anotarlos, y confirmar que se guardaron para poder cerrar. */
function DialogoDeCodigos({ nuevos, onCerrar }: { nuevos: CodigosDeRecuperacionDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [guardados, setGuardados] = useState(false);
  const copiar = () => void navigator.clipboard.writeText(textoDeCodigos(nuevos.codigos)).then(() => mostrar(t('cuenta.recuperacion.copiados')));

  return (
    <Dialog open maxWidth={false} slotProps={{ paper: { sx: { width: 480, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-codigos-recuperacion">
      <DialogTitle id="titulo-codigos-recuperacion">{t('cuenta.recuperacion.nuevosTitulo')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Alert severity="warning">{t('cuenta.recuperacion.nuevosAviso')}</Alert>
          <Box
            component="ul"
            aria-label={t('cuenta.recuperacion.nuevosTitulo')}
            sx={{ listStyle: 'none', m: 0, p: 1.5, display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: 1, bgcolor: 'superficie.nivel2', borderRadius: (th) => `${String(th.radio.sm)}px` }}
          >
            {nuevos.codigos.map((codigo) => (
              <Box component="li" key={codigo} data-secreto="" sx={{ fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 14, letterSpacing: 0.5 }}>
                {codigo}
              </Box>
            ))}
          </Box>
          <Stack direction="row" spacing={1}>
            <Button size="small" variant="outlined" startIcon={<Copy size={14} aria-hidden />} onClick={copiar}>
              {t('cuenta.recuperacion.copiar')}
            </Button>
            <Button size="small" variant="outlined" startIcon={<Printer size={14} aria-hidden />} onClick={() => imprimirCodigos(nuevos, t('cuenta.recuperacion.impresionTitulo'), t('cuenta.recuperacion.impresionAyuda'))}>
              {t('cuenta.recuperacion.imprimir')}
            </Button>
          </Stack>
          <FormControlLabel control={<Checkbox checked={guardados} onChange={(e) => setGuardados(e.target.checked)} />} label={t('cuenta.recuperacion.guardados')} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onCerrar} disabled={!guardados}>
          {t('cuenta.recuperacion.listo')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/**
 * Códigos de recuperación (PI-18): diez códigos de un solo uso para entrar si se pierde la passkey. Aquí se ve cuántos quedan y se generan otros (los
 * anteriores dejan de servir); generarlos exige confirmar con la passkey (PI-13), que el cliente de la API pide solo.
 */
export function TarjetaCodigosDeRecuperacion() {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = useQuery(getObtenerEstadoCodigosRecuperacionQueryOptions({ request: SIN_EMPRESA }));
  const [confirmando, setConfirmando] = useState(false);
  const [nuevos, setNuevos] = useState<CodigosDeRecuperacionDto | null>(null);
  const generar = useMutation({
    mutationFn: () => generarCodigosRecuperacion(SIN_EMPRESA),
    onSuccess: (codigos) => {
      setConfirmando(false);
      setNuevos(codigos);
    },
    onError: (error) => mostrar(mensajeDeIngreso(error, t), 'error'),
    onSettled: () => invalidarRuta(cliente, RUTA),
  });

  const estado = consulta.data;
  // La API da siempre el tamaño del juego (diez); que haya uno lo dice la fecha en que se generó.
  const tiene = estado?.generadosEn != null;
  const pocos = tiene && estado.disponibles <= 2;

  return (
    <Tarjeta titulo={t('cuenta.recuperacion.titulo')}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
        {t('cuenta.recuperacion.ayuda')}
      </Typography>
      {consulta.isError && <ErrorRecuperable mensaje={t('cuenta.recuperacion.errorCarga')} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && <Skeleton variant="rounded" height={56} role="status" aria-busy aria-label={t('comun.cargando')} />}
      {estado && (
        <Stack spacing={1.5}>
          {tiene ? (
            <Alert severity={pocos ? 'warning' : 'info'} icon={<LifeBuoy size={18} aria-hidden />}>
              {t('cuenta.recuperacion.quedan', { count: estado.disponibles, total: estado.total })}
              {estado.generadosEn ? ` ${t('cuenta.recuperacion.generados', { fecha: formatearFechaHora(estado.generadosEn) })}` : ''}
              {pocos ? ` ${t('cuenta.recuperacion.pocos')}` : ''}
            </Alert>
          ) : (
            <Typography variant="body2">{t('cuenta.recuperacion.ninguno')}</Typography>
          )}
          <Button variant="outlined" onClick={() => setConfirmando(true)} sx={{ alignSelf: 'flex-start' }}>
            {tiene ? t('cuenta.recuperacion.generarNuevos') : t('cuenta.recuperacion.generar')}
          </Button>
        </Stack>
      )}
      {confirmando && (
        <DialogoConfirmacion
          titulo={t('cuenta.recuperacion.confirmarTitulo')}
          mensaje={tiene ? t('cuenta.recuperacion.confirmarReemplaza') : t('cuenta.recuperacion.confirmarNuevos')}
          confirmar={t('cuenta.recuperacion.generar')}
          tono="primary"
          ocupado={generar.isPending}
          onConfirmar={() => generar.mutate()}
          onCerrar={() => {
            generar.reset();
            setConfirmando(false);
          }}
        />
      )}
      {nuevos && <DialogoDeCodigos nuevos={nuevos} onCerrar={() => setNuevos(null)} />}
    </Tarjeta>
  );
}
