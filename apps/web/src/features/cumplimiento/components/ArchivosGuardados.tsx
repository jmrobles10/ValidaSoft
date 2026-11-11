import { Box, Button, Stack, Typography } from '@mui/material';
import type { AdjuntoDeCumplimientoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { FileDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useDescargarAdjunto } from '../api';
import { formatearPeso } from '../modelo';

/** Archivos que el oficial subió y quedaron citados en el estudio; descargarlos queda en el historial del expediente. */
export function ArchivosGuardados({ empresaId, expedienteId, archivos, puedeDescargar }: { empresaId: string; expedienteId: string; archivos: readonly AdjuntoDeCumplimientoDto[]; puedeDescargar: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const descarga = useDescargarAdjunto(empresaId, expedienteId);
  if (archivos.length === 0) return <>—</>;
  return (
    <Stack component="ul" spacing={0.5} sx={{ listStyle: 'none', p: 0, m: 0 }}>
      {archivos.map((a) => (
        <Stack key={a.id} component="li" direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }} useFlexGap>
          <Box sx={{ minWidth: 0 }}>
            <Typography component="span" sx={{ fontSize: 14, overflowWrap: 'anywhere' }}>
              {a.nombre}
            </Typography>
            <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
              {` · ${t('cumplimiento.archivos.pesoYFecha', { peso: formatearPeso(a.tamano), fecha: formatearFecha(a.fecha) })}`}
            </Typography>
          </Box>
          {puedeDescargar && (
            <Button
              size="small"
              variant="text"
              aria-label={t('cumplimiento.archivos.descargar', { nombre: a.nombre })}
              startIcon={descarga.isPending && descarga.variables === a.id ? <IconoCarga /> : <FileDown size={16} aria-hidden />}
              disabled={descarga.isPending}
              onClick={() => descarga.mutate(a.id, { onError: (e) => mostrar(mensajeDeError(e, t), 'error') })}
            >
              {t('cumplimiento.panel.evidencia')}
            </Button>
          )}
        </Stack>
      ))}
    </Stack>
  );
}
