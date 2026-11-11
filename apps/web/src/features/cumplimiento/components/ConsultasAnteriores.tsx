import { Box, Button, Chip, Stack, Typography } from '@mui/material';
import type { ConsultaAnteriorDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { FileDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useDescargarEvidencia } from '../api';
import { fuenteManual } from '../consultaManual';

/** Consultas de listas anteriores a la vigente; las hechas por fuera se distinguen («Manual · fuente») y su PDF se abre igual que el de las automáticas. */
export function ConsultasAnteriores({ empresaId, expedienteId, anteriores, puedeDescargar }: { empresaId: string; expedienteId: string; anteriores: readonly ConsultaAnteriorDto[]; puedeDescargar: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const descarga = useDescargarEvidencia(empresaId);
  return (
    <Box component="details" sx={{ '& > summary': { cursor: 'pointer', color: 'text.secondary', fontSize: 12 } }}>
      <summary>{t('cumplimiento.panel.anteriores', { count: anteriores.length })}</summary>
      <Stack component="ul" spacing={0.75} sx={{ listStyle: 'none', p: 0, m: 0, mt: 1 }}>
        {anteriores.map((a) => (
          <Stack key={`${a.referenciaExterna}-${a.fecha}`} component="li" direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Typography component="span" sx={{ fontSize: 13 }}>
              {formatearFecha(a.fecha)}
            </Typography>
            <Typography component="span" sx={{ fontSize: 13 }}>
              {a.proveedor}
            </Typography>
            {fuenteManual(a.proveedor) !== null && <Chip size="small" variant="outlined" label={t('cumplimiento.panel.manual')} />}
            <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
              {t('cumplimiento.panel.anteriorCoincidencias', { count: a.coincidencias })}
            </Typography>
            {puedeDescargar && a.conEvidencia && (
              <Button
                size="small"
                variant="text"
                startIcon={descarga.isPending && descarga.variables.referencia === a.referenciaExterna ? <IconoCarga /> : <FileDown size={16} aria-hidden />}
                disabled={descarga.isPending}
                onClick={() => descarga.mutate({ expedienteId, referencia: a.referenciaExterna }, { onError: (e) => mostrar(mensajeDeError(e, t), 'error') })}
              >
                {t('cumplimiento.panel.evidencia')}
              </Button>
            )}
          </Stack>
        ))}
      </Stack>
    </Box>
  );
}
