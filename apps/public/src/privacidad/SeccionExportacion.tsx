import { Alert, Box, Button, Card, Stack, Typography } from '@mui/material';
import type { ExportacionBovedaDto } from '@validasoft/api-client';
import { ChipEstado, EsqueletoTarjetas, formatearFechaHora, type Estado } from '@validasoft/ui';
import { Download, PackageOpen } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable } from '../shared/componentes';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { useDescargarExportacion, useExportaciones, useExportar } from './api';
import { enCurso, esEstadoDeExportacion, tamanoLegible } from './modelo';

const ESTADO_DE_LA_EXPORTACION: Readonly<Record<string, Estado>> = {
  Pendiente: 'EnRevision',
  Generando: 'EnRevision',
  Lista: 'Aprobado',
  Fallida: 'Rechazado',
  Vencida: 'Vencido',
};

function FilaExportacion({ exportacion }: { exportacion: ExportacionBovedaDto }) {
  const { t } = useTranslation();
  const descargar = useDescargarExportacion();
  const mostrar = useAvisos((s) => s.mostrar);
  const lista = exportacion.estado === 'Lista';
  const etiquetaDeEstado = (estado: string) => (esEstadoDeExportacion(estado) ? t(`privacidad.exportacion.estado.${estado}`) : estado);
  const detalle = lista
    ? t('privacidad.exportacion.detalleLista', {
        archivos: String(exportacion.archivos),
        tamano: tamanoLegible(exportacion.bytes ?? 0),
        hasta: exportacion.expiraEn ? formatearFechaHora(exportacion.expiraEn) : '—',
      })
    : exportacion.estado === 'Fallida'
      ? t('privacidad.exportacion.detalleFallida')
      : exportacion.estado === 'Vencida'
        ? t('privacidad.exportacion.detalleVencida')
        : t('privacidad.exportacion.detalleEnCurso');

  return (
    <Box
      component="li"
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: 'minmax(0, 1fr) auto', sm: 'minmax(0, 1fr) max-content auto' },
        alignItems: 'center',
        columnGap: 1.5,
        rowGap: 0.5,
        py: 1,
        px: { xs: 1.5, sm: 2 },
        borderTop: 1,
        borderColor: 'divider',
        listStyle: 'none',
      }}
    >
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontWeight: 500, fontSize: 14, lineHeight: '20px' }}>{t('privacidad.exportacion.pedidaEl', { fecha: formatearFechaHora(exportacion.solicitadaEn) })}</Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>{detalle}</Typography>
        {lista && exportacion.sha256 && (
          <Box component="details" sx={{ mt: 0.5, fontSize: 12, color: 'text.secondary' }}>
            <Box component="summary" sx={{ cursor: 'pointer' }}>
              {t('privacidad.exportacion.comoVerificar')}
            </Box>
            <Typography sx={{ fontSize: 12, mt: 0.5 }}>{t('privacidad.exportacion.comoVerificarTexto', { kid: exportacion.kid ?? '—' })}</Typography>
            <Typography variant="mono" component="p" sx={{ fontSize: 11, overflowWrap: 'anywhere', mt: 0.5 }}>
              SHA-256 ZIP: {exportacion.sha256}
            </Typography>
            <Typography variant="mono" component="p" sx={{ fontSize: 11, overflowWrap: 'anywhere' }}>
              SHA-256 manifiesto: {exportacion.hashManifiesto}
            </Typography>
          </Box>
        )}
      </Box>
      <ChipEstado estado={ESTADO_DE_LA_EXPORTACION[exportacion.estado] ?? 'Pendiente'} etiqueta={etiquetaDeEstado(exportacion.estado)} size="medium" />
      {lista ? (
        <Button
          variant="text"
          startIcon={<Download size={16} aria-hidden />}
          disabled={descargar.isPending}
          onClick={() =>
            descargar.mutate(exportacion.id, {
              onSuccess: () => mostrar(t('privacidad.exportacion.descargando')),
              onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
            })
          }
          sx={{ gridColumn: { xs: '1 / -1', sm: 'auto' }, justifySelf: { xs: 'start', sm: 'end' } }}
        >
          {t('privacidad.exportacion.descargar')}
        </Button>
      ) : (
        <Box aria-hidden sx={{ display: { xs: 'none', sm: 'block' } }} />
      )}
    </Box>
  );
}

/**
 * Exportar mi bóveda (OD-9): el tercero pide un ZIP con sus archivos, sus datos y un manifiesto firmado; se genera en segundo plano
 * y se baja por una URL prefirmada. Avisa antes qué pasará: se prepara solo y le llega un correo.
 */
export function SeccionExportacion() {
  const { t } = useTranslation();
  const exportaciones = useExportaciones();
  const exportar = useExportar();
  const mostrar = useAvisos((s) => s.mostrar);
  const preparando = (exportaciones.data ?? []).some(enCurso) || exportar.isPending;

  return (
    <Card component="section" aria-labelledby="privacidad-exportacion" sx={{ p: 0, overflow: 'hidden' }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ alignItems: { sm: 'center' }, p: { xs: 1.5, sm: 2 } }}>
        <Box aria-hidden sx={(th) => ({ display: 'grid', placeItems: 'center', flex: 'none', width: 36, height: 36, borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2', color: 'text.secondary' })}>
          <PackageOpen size={20} />
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography id="privacidad-exportacion" component="h2" sx={{ fontWeight: 600, fontSize: 16, lineHeight: '22px' }}>
            {t('privacidad.exportacion.titulo')}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px' }}>{t('privacidad.exportacion.texto')}</Typography>
        </Box>
        <Button
          variant="contained"
          disabled={preparando}
          onClick={() =>
            exportar.mutate(undefined, {
              onSuccess: () => mostrar(t('privacidad.exportacion.pedida')),
            })
          }
          sx={{ width: { xs: '100%', sm: 'auto' }, flex: 'none' }}
        >
          {preparando ? t('privacidad.exportacion.preparando') : t('privacidad.exportacion.pedir')}
        </Button>
      </Stack>
      {exportar.isError && (
        <Alert severity="error" sx={{ mx: { xs: 1.5, sm: 2 }, mb: 1.5 }}>
          {mensajeDeError(exportar.error, t)}
        </Alert>
      )}
      {exportaciones.isPending ? (
        <Box sx={{ px: 2, pb: 2 }}>
          <EsqueletoTarjetas cantidad={1} lineas={2} />
        </Box>
      ) : exportaciones.isError ? (
        <Box sx={{ px: 2, pb: 2 }}>
          <ErrorRecuperable error={exportaciones.error} onReintentar={() => void exportaciones.refetch()} />
        </Box>
      ) : exportaciones.data.length > 0 ? (
        <Box component="ul" aria-label={t('privacidad.exportacion.lista')} sx={{ p: 0, m: 0 }}>
          {exportaciones.data.map((e) => (
            <FilaExportacion key={e.id} exportacion={e} />
          ))}
        </Box>
      ) : null}
    </Card>
  );
}
