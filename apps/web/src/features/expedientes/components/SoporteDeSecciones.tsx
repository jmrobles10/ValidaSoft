import { Alert, Chip, Stack, Typography } from '@mui/material';
import type { SeccionRespaldadaDto, SoporteDeSeccionesDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { SeccionFicha } from '../../terceros/SeccionesVinculo';
import { TablaCruce } from './CruceSeccion';

/**
 * Lo que un documento exigido respalda de la ficha del tercero (F5, «Usar como soporte»): por cada sección que respalda, su
 * versión vigente y el resultado del cruce con esta versión del documento. Sin la lectura automática para la empresa, el aviso
 * de comparar a mano. Es evidencia: no decide nada.
 */
export function SoporteDeSecciones({ soporte }: { soporte: SoporteDeSeccionesDto }) {
  const { t } = useTranslation();
  if (soporte.secciones.length === 0 && !soporte.sinAnalisis) return null;
  return (
    <SeccionFicha titulo={t('cruce.soporte.titulo')}>
      <Stack spacing={1.25}>
        {soporte.discrepanciaCritica && <Alert severity="error">{t('cruce.discrepanciaCritica')}</Alert>}
        {soporte.sinAnalisis && <Alert severity="info">{t('cruce.soporte.sinAnalisis')}</Alert>}
        <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {soporte.secciones.map((s) => (
            <FilaDeSeccion key={`${s.seccion}-${String(s.periodo ?? '')}`} respaldada={s} />
          ))}
        </Stack>
      </Stack>
    </SeccionFicha>
  );
}

function FilaDeSeccion({ respaldada }: { respaldada: SeccionRespaldadaDto }) {
  const { t } = useTranslation();
  const { cruce } = respaldada;
  const nombre = respaldada.version === null ? t('cruce.soporte.seccionSinDiligenciar', { seccion: respaldada.nombre }) : t('cruce.soporte.seccion', { seccion: respaldada.nombre, n: String(respaldada.version) });
  return (
    <Stack component="li" spacing={0.75} sx={{ minWidth: 0 }}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography variant="body2" sx={{ fontWeight: 500 }}>
          {nombre}
        </Typography>
        {cruce === null && respaldada.version !== null && (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('cruce.soporte.sinCruce')}
          </Typography>
        )}
        {cruce && (
          <>
            <Chip
              size="small"
              variant="outlined"
              color={cruce.discrepanciaCritica ? 'error' : cruce.discrepancias > 0 ? 'warning' : 'success'}
              label={cruce.discrepanciaCritica ? t('cruce.soporte.criticas') : cruce.discrepancias > 0 ? t('cruce.soporte.difiere', { count: cruce.discrepancias }) : t('cruce.soporte.coincide')}
            />
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('cruce.resumen', { coinciden: String(cruce.coincidencias), difieren: String(cruce.discrepancias), noVerificables: String(cruce.noVerificables) })}
            </Typography>
          </>
        )}
      </Stack>
      {cruce && <TablaCruce cruce={cruce} />}
    </Stack>
  );
}
