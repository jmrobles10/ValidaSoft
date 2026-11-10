import { Chip, Link, Stack, Typography } from '@mui/material';
import type { AceptacionDeSeccionDto, VinculoEnFichaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { SeccionFicha } from '../SeccionesVinculo';

interface Props {
  empresaId: string;
  aceptaciones: readonly AceptacionDeSeccionDto[];
  vinculos: readonly VinculoEnFichaDto[];
}

/**
 * Lo que la empresa activa tiene aceptado de la sección, por vínculo (FM-29): la versión y la fecha (con su expediente)
 * y, si el tercero guardó otra, «Versión nueva pendiente de revisión» con el enlace a la revalidación que la revisa.
 */
export function AceptacionesSeccion({ empresaId, aceptaciones, vinculos }: Props) {
  const { t } = useTranslation();
  if (aceptaciones.length === 0) return null;
  const nombreVinculo = (id: string) => vinculos.find((v) => v.vinculo.id === id)?.vinculo.tipoVinculo.nombre ?? t('fichaEmpresa.aceptaciones.titulo');
  const expediente = (id: string) => `/e/${empresaId}/expedientes/${id}`;

  return (
    <SeccionFicha titulo={t('fichaEmpresa.aceptaciones.titulo')}>
      <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {aceptaciones.map((a) => {
          const vinculo = nombreVinculo(a.vinculoId);
          return (
            <Stack component="li" key={a.vinculoId} spacing={0.5}>
              <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <Typography variant="body2">
                  {a.versionAceptada !== null
                    ? t('fichaEmpresa.aceptaciones.aceptada', { vinculo, n: String(a.versionAceptada), fecha: a.aceptadaEn ? formatearFecha(a.aceptadaEn) : '—' })
                    : t('fichaEmpresa.aceptaciones.sinAceptar', { vinculo })}
                </Typography>
                {a.expedienteId && a.folio && (
                  <Link component={RouterLink} to={expediente(a.expedienteId)} variant="body2" aria-label={t('fichaEmpresa.aceptaciones.verExpediente', { folio: a.folio })}>
                    {t('fichaEmpresa.aceptaciones.expediente', { folio: a.folio })}
                  </Link>
                )}
              </Stack>
              {a.versionNuevaPendiente && (
                <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                  <Chip size="small" color="warning" variant="outlined" label={t('fichaEmpresa.aceptaciones.pendiente')} />
                  {a.revalidacion && (
                    <Link
                      component={RouterLink}
                      to={expediente(a.revalidacion.expedienteId)}
                      variant="body2"
                      aria-label={t('fichaEmpresa.aceptaciones.verRevalidacion', { folio: a.revalidacion.folio })}
                    >
                      {t('fichaEmpresa.aceptaciones.revalidacion', {
                        folio: a.revalidacion.folio,
                        n: String(a.revalidacion.version ?? '—'),
                        fecha: formatearFecha(a.revalidacion.plazoHasta),
                      })}
                    </Link>
                  )}
                </Stack>
              )}
            </Stack>
          );
        })}
      </Stack>
    </SeccionFicha>
  );
}
