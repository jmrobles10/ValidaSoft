import { Box, Button, Stack, Typography } from '@mui/material';
import type { ExpedientePublicoDto, RequisitoPublicoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { ClipboardList, PencilLine } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Observacion } from './Observacion';
import { accionDeRequisito, enlaceDeSeccion, observacionesDeRequisito } from './requisitos';

/**
 * Una sección de «Mi información» que pide la empresa (F2): su estado y, si le toca al tercero, lo que le falta para esa
 * empresa (pendiente) o lo que observó con los datos señalados (observada), con el botón que lleva al wizard de la
 * sección. Aquí no se sube nada: la información se diligencia una vez y le llega a cada empresa.
 */
export function TarjetaRequisitoSeccion({ requisito, expediente, conAccion }: { requisito: RequisitoPublicoDto; expediente: ExpedientePublicoDto; conAccion: boolean }) {
  const { t } = useTranslation();
  const empresa = expediente.empresa.nombre;
  const accion = accionDeRequisito(requisito, expediente);
  const version = requisito.versionActual;
  const faltantes = requisito.estado === 'Pendiente' ? (requisito.seccion?.completitud?.faltantes ?? []) : [];
  const observaciones = requisito.estado === 'Observado' ? observacionesDeRequisito(requisito, expediente) : [];
  const boton = conAccion && (accion === 'subir' || accion === 'corregir') ? accion : null;

  return (
    <Box component="li" sx={{ py: 1, px: { xs: 1.5, sm: 2 }, minHeight: 44 }}>
      <Stack direction="row" spacing={1.25} sx={{ alignItems: 'flex-start' }}>
        <Box sx={{ color: 'text.secondary', pt: '2px', display: 'flex' }} aria-hidden>
          <ClipboardList size={16} />
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography component="h4" sx={{ fontWeight: 500, fontSize: 14, lineHeight: '20px' }}>
            {requisito.tipoDocumento.nombre}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {version ? t('expediente.seccion.version', { n: String(version.n), fecha: formatearFecha(version.cargadaEn) }) : t('expediente.seccion.sinVersion')}
          </Typography>
        </Box>
        <ChipEstado estado={requisito.estado} size="medium" />
      </Stack>

      {faltantes.length > 0 && (
        <Box sx={(th) => ({ mt: 1, py: 1, px: 1.5, borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2' })}>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px', mb: 0.25 }}>{t('expediente.seccion.faltanTitulo', { empresa })}</Typography>
          <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
            {faltantes.map((f, i) => (
              <Typography component="li" key={`${f.campo ?? f.regla ?? ''}-${String(i)}`} sx={{ fontSize: 14, lineHeight: '20px' }}>
                {f.mensaje}
              </Typography>
            ))}
          </Stack>
        </Box>
      )}

      {observaciones.map((o) => (
        <Observacion key={o.fecha} observacion={o} />
      ))}

      {boton && (
        <Box sx={{ mt: 1, pl: 3.25 }}>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>{t('expediente.seccion.ayuda')}</Typography>
          <Button
            component={RouterLink}
            to={enlaceDeSeccion(requisito)}
            variant="contained"
            size="large"
            startIcon={<PencilLine size={16} aria-hidden />}
            sx={{ mt: 1 }}
          >
            {boton === 'corregir' ? t('expediente.seccion.corregir') : t('expediente.seccion.completar')}
          </Button>
        </Box>
      )}
    </Box>
  );
}
