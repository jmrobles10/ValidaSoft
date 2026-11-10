import { Box, Chip, Divider, Link, Stack, Typography } from '@mui/material';
import type { SeccionFichaDto, SoporteSeccionDto, VinculoEnFichaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../../shared/componentes';
import { SeccionFicha } from '../SeccionesVinculo';
import { DatosDeSeccion } from './DatosDeSeccion';
import { esObjeto } from './formato';

interface Props {
  empresaId: string;
  secciones: readonly SeccionFichaDto[];
  vinculos: readonly VinculoEnFichaDto[];
}

/**
 * «Información del tercero» en Tercero 360 (docs/14 §7): una tarjeta por sección vigente de la ficha maestra con su
 * estado (completa o lo que falta), versión y fecha, los datos en solo lectura y sus soportes. Sin acciones: aceptar u
 * observar llega con F2.
 */
export function InformacionTercero({ empresaId, secciones, vinculos }: Props) {
  const { t } = useTranslation();
  const titulo = t('fichaMaestra.titulo');
  return (
    <Stack component="section" aria-label={titulo} spacing={1.5} sx={{ minWidth: 0 }}>
      <Box>
        <Typography variant="h4" component="h2">
          {titulo}
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('fichaMaestra.ayuda')}
        </Typography>
      </Box>
      {secciones.length === 0 ? (
        <Tarjeta>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('fichaMaestra.vacia')}
          </Typography>
        </Tarjeta>
      ) : (
        secciones.map((s) => <TarjetaSeccion key={`${s.seccion}-${String(s.periodo)}`} empresaId={empresaId} seccion={s} vinculos={vinculos} />)
      )}
    </Stack>
  );
}

function TarjetaSeccion({ empresaId, seccion, vinculos }: { empresaId: string; seccion: SeccionFichaDto; vinculos: readonly VinculoEnFichaDto[] }) {
  const { t } = useTranslation();
  const nombre = seccion.periodo ? t('fichaMaestra.nombreConPeriodo', { nombre: seccion.nombre, periodo: String(seccion.periodo) }) : seccion.nombre;
  const completitud = seccion.completitud;
  const chip = completitud?.completa ? (
    <Chip size="small" color="success" variant="outlined" label={t('fichaMaestra.completa')} />
  ) : (
    <Chip size="small" color="warning" variant="outlined" label={t('fichaMaestra.faltan', { count: completitud?.faltantes.length ?? 0 })} />
  );

  return (
    <Tarjeta
      nivel="h3"
      etiqueta={nombre}
      titulo={
        <Stack direction="row" spacing={1} component="span" useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <span>{nombre}</span>
          {chip}
        </Stack>
      }
    >
      <Stack spacing={1.5} divider={<Divider flexItem />}>
        {seccion.version !== null && (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('fichaMaestra.version', { n: String(seccion.version), fecha: seccion.fecha ? formatearFecha(seccion.fecha) : '—' })}
          </Typography>
        )}
        {completitud && completitud.faltantes.length > 0 && (
          <SeccionFicha titulo={t('fichaMaestra.faltantes')}>
            <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
              {completitud.faltantes.map((f, i) => (
                <Typography component="li" variant="body2" key={`${f.campo ?? f.regla ?? ''}-${String(i)}`}>
                  {f.mensaje}
                </Typography>
              ))}
            </Box>
          </SeccionFicha>
        )}
        <SeccionFicha titulo={t('fichaMaestra.datos')}>
          <DatosDeSeccion datos={esObjeto(seccion.datos) ? seccion.datos : {}} />
        </SeccionFicha>
        <SeccionFicha titulo={t('fichaMaestra.soportes')}>
          {seccion.soportes.length === 0 ? (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('fichaMaestra.sinSoportes')}
            </Typography>
          ) : (
            <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
              {seccion.soportes.map((s) => (
                <Soporte key={`${s.documentoId}-${String(s.version)}`} empresaId={empresaId} soporte={s} vinculos={vinculos} />
              ))}
            </Stack>
          )}
        </SeccionFicha>
      </Stack>
    </Tarjeta>
  );
}

/** Un soporte: enlace al documento en el vínculo si la empresa lo puede ver; si no, solo que es del tercero. */
function Soporte({ empresaId, soporte, vinculos }: { empresaId: string; soporte: SoporteSeccionDto; vinculos: readonly VinculoEnFichaDto[] }) {
  const { t } = useTranslation();
  const visible = vinculos.flatMap((v) => (v.documentos ?? []).filter((d) => d.documentoId === soporte.documentoId).map((d) => ({ vinculoId: v.vinculo.id, tipo: d.tipo.nombre })))[0];
  return (
    <Stack component="li" direction="row" spacing={1} sx={{ alignItems: 'center' }}>
      <FileText size={16} strokeWidth={1.75} aria-hidden style={{ flex: 'none', opacity: 0.7 }} />
      {visible ? (
        <Link component={RouterLink} to={`/e/${empresaId}/vinculos/${visible.vinculoId}`} variant="body2" aria-label={t('fichaMaestra.verEnVinculo', { tipo: visible.tipo })}>
          {t('fichaMaestra.soporteVisible', { tipo: visible.tipo, n: String(soporte.version) })}
        </Link>
      ) : (
        <Typography variant="body2">{t('fichaMaestra.soporteDelTercero', { n: String(soporte.version) })}</Typography>
      )}
    </Stack>
  );
}
