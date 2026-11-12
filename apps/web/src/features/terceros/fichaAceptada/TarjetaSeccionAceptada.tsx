import { Box, Button, Chip, Collapse, Divider, Link, Stack, Typography } from '@mui/material';
import type { SeccionAceptadaVistaDto, SoporteSeccionDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { ChevronDown, FileText } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../../shared/componentes';
import { ComparacionSeccion } from '../../expedientes/components/ComparacionSeccion';
import { DatosDeSeccion } from '../ficha/DatosDeSeccion';
import { esObjeto } from '../ficha/formato';
import { SinSoporte } from '../ficha/SinSoporte';
import { SeccionFicha } from '../SeccionesVinculo';

interface Props {
  empresaId: string;
  /** El tercero de la ficha: con él se descarga el PDF de la declaración PEP de las personas aceptadas. */
  terceroId?: string;
  seccion: SeccionAceptadaVistaDto;
}

/**
 * Una sección de la ficha aceptada (FD-3). Arriba, **lo que la empresa aceptó**: versión, fecha, expediente, soporte y (a pedido) los
 * datos. Si el tercero guardó después una versión nueva, va **aparte**, en un bloque propio con borde: qué cambió frente a lo
 * aceptado, su soporte (o que no trae un documento nuevo) y la revalidación que la revisa. Lo aceptado nunca se mezcla con lo nuevo.
 */
export function TarjetaSeccionAceptada({ empresaId, terceroId, seccion }: Props) {
  const { t } = useTranslation();
  const [verDatos, setVerDatos] = useState(false);
  const { aceptada, cambioPosterior } = seccion;
  const nombre = seccion.periodo ? t('fichaMaestra.nombreConPeriodo', { nombre: seccion.nombre, periodo: String(seccion.periodo) }) : seccion.nombre;

  return (
    <Tarjeta
      nivel="h3"
      etiqueta={nombre}
      titulo={
        <Stack direction="row" spacing={1} component="span" useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <span>{nombre}</span>
          <Chip size="small" color="success" variant="outlined" label={t('fichaAceptada.aceptada.chip', { n: String(aceptada.version) })} />
          {cambioPosterior && <Chip size="small" color="warning" variant="outlined" label={t('fichaEmpresa.aceptaciones.pendiente')} />}
        </Stack>
      }
    >
      <Stack spacing={1.5} divider={<Divider flexItem />}>
        <SeccionFicha titulo={t('fichaAceptada.aceptada.version', { n: String(aceptada.version), fecha: formatearFecha(aceptada.aceptadaEn) })}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            {aceptada.expedienteId && aceptada.folio ? (
              <Link
                component={RouterLink}
                to={`/e/${empresaId}/expedientes/${aceptada.expedienteId}`}
                variant="body2"
                aria-label={t('fichaAceptada.aceptada.verExpediente', { folio: aceptada.folio })}
              >
                {t('fichaAceptada.aceptada.expediente', { folio: aceptada.folio })}
              </Link>
            ) : (
              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                {t('fichaAceptada.aceptada.importada')}
              </Typography>
            )}
          </Stack>
        </SeccionFicha>

        <SeccionFicha titulo={t('fichaAceptada.aceptada.soportes')}>
          <Soportes soportes={aceptada.soportes} vacio={t('fichaAceptada.aceptada.sinSoportes')} />
        </SeccionFicha>

        <Box>
          <Button
            size="small"
            variant="text"
            onClick={() => setVerDatos((v) => !v)}
            aria-expanded={verDatos}
            endIcon={<ChevronDown size={16} aria-hidden style={{ transform: verDatos ? 'rotate(180deg)' : undefined, transition: 'transform 150ms' }} />}
            sx={{ ml: -1 }}
          >
            {verDatos ? t('fichaAceptada.aceptada.ocultarDatos') : t('fichaAceptada.aceptada.verDatos')}
          </Button>
          <Collapse in={verDatos} unmountOnExit>
            <Box sx={{ pt: 1 }}>
              <SeccionFicha titulo={t('fichaAceptada.aceptada.datos')}>
                <DatosDeSeccion datos={esObjeto(aceptada.datos) ? aceptada.datos : {}} empresaId={empresaId} terceroId={terceroId} seccion={seccion.seccion} version={aceptada.version} />
              </SeccionFicha>
            </Box>
          </Collapse>
        </Box>

        {cambioPosterior && (
          <Box
            component="section"
            aria-label={t('fichaAceptada.posterior.titulo')}
            sx={{
              p: 1.5,
              border: 1,
              borderColor: 'estado.advertencia.borde',
              borderRadius: (th) => `${String(th.radio.md)}px`,
              bgcolor: 'estado.advertencia.fondo',
            }}
          >
            <Stack spacing={1.25}>
              <Box>
                <Typography variant="h5" component="h4" sx={{ color: 'estado.advertencia.texto' }}>
                  {t('fichaAceptada.posterior.titulo')}
                </Typography>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  {t('fichaAceptada.posterior.version', { n: String(cambioPosterior.version), fecha: formatearFecha(cambioPosterior.fecha) })}
                </Typography>
              </Box>
              {cambioPosterior.sinSoporte && <SinSoporte sinSoporte={cambioPosterior.sinSoporte} />}
              <SeccionFicha titulo={t('fichaAceptada.posterior.cambios')}>
                <ComparacionSeccion cambios={cambioPosterior.cambios} versionAceptada={aceptada.version} />
              </SeccionFicha>
              <SeccionFicha titulo={t('fichaAceptada.posterior.soportes')}>
                <Soportes soportes={cambioPosterior.soportes} vacio={t('fichaAceptada.posterior.sinSoportes')} />
              </SeccionFicha>
              {cambioPosterior.revalidacion && (
                <Link
                  component={RouterLink}
                  to={`/e/${empresaId}/expedientes/${cambioPosterior.revalidacion.expedienteId}`}
                  variant="body2"
                  aria-label={t('fichaEmpresa.aceptaciones.verRevalidacion', { folio: cambioPosterior.revalidacion.folio })}
                >
                  {t('fichaEmpresa.aceptaciones.revalidacion', {
                    folio: cambioPosterior.revalidacion.folio,
                    n: String(cambioPosterior.revalidacion.version ?? '—'),
                    fecha: formatearFecha(cambioPosterior.revalidacion.plazoHasta),
                  })}
                </Link>
              )}
            </Stack>
          </Box>
        )}
      </Stack>
    </Tarjeta>
  );
}

/** Los soportes de una versión: el nombre del tipo y su versión; uno que el tercero retiró lo dice (RT-4). */
function Soportes({ soportes, vacio }: { soportes: readonly SoporteSeccionDto[]; vacio: string }) {
  const { t } = useTranslation();
  if (soportes.length === 0) {
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {vacio}
      </Typography>
    );
  }
  return (
    <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
      {soportes.map((s) => (
        <Stack component="li" key={`${s.documentoId}-${String(s.version)}`} direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <FileText size={16} strokeWidth={1.75} aria-hidden style={{ flex: 'none', opacity: 0.7 }} />
          <Typography variant="body2">{t('fichaAceptada.soporte.etiqueta', { nombre: s.nombre ?? s.tipoDocumento ?? '', n: String(s.version) })}</Typography>
          {s.retirado && <Chip size="small" color="warning" variant="outlined" label={t('fichaAceptada.soporte.retirado')} />}
        </Stack>
      ))}
    </Stack>
  );
}
