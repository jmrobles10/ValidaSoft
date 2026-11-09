import { Alert, AlertTitle, Box, Chip, Stack, Tooltip, Typography } from '@mui/material';
import type { HechosDto, HechosIaDto } from '@validasoft/api-client';
import { formatearFecha, LOCALE } from '@validasoft/ui';
import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { camposExtraidos, confianzaDe, estadoRevision, tipoNoCoincide, UMBRAL_CONFIANZA } from './revisionIa';
import { CamposIa } from './CamposIa';

/** El tipo canónico que pide el requisito y su nombre, para avisar si el archivo parece ser otro. */
export interface TipoEsperado {
  codigo: string | null;
  nombre: string;
}

export interface RevisionAutomaticaProps {
  hechos: HechosDto | null | undefined;
  tipoEsperado?: TipoEsperado | null;
  /** Cuántos datos extraídos se muestran antes de «Ver n datos más» (sin límite, todos). */
  limite?: number;
}

const PORCENTAJE = new Intl.NumberFormat(LOCALE, { style: 'percent', maximumFractionDigits: 0 });

/**
 * Panel «Revisión automática» de una versión (ADR-0012, IA-11): confianza, si se escaló a un modelo mayor, legibilidad,
 * tipo detectado, alertas de manipulación y los datos extraídos. Sin análisis visible para la empresa (`conIa` falso),
 * una línea discreta que pide revisarla a mano. El modelo solo va en texto pequeño.
 */
export function RevisionAutomatica({ hechos, tipoEsperado, limite }: RevisionAutomaticaProps) {
  const { t } = useTranslation();
  const estado = estadoRevision(hechos);
  if (estado === 'nada' || !hechos) return null;
  if (estado === 'sinAnalisis' || !hechos.ia) {
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12 }}>
        {t('ia.revision.sinAnalisis')}
      </Typography>
    );
  }
  return <ConAnalisis ia={hechos.ia} tipoEsperado={tipoEsperado} limite={limite} />;
}

function ConAnalisis({ ia, tipoEsperado, limite }: { ia: HechosIaDto; tipoEsperado?: TipoEsperado | null; limite?: number }) {
  const { t } = useTranslation();
  const confianza = confianzaDe(ia);
  const alta = confianza !== null && confianza >= UMBRAL_CONFIANZA;
  const tipos: Readonly<Record<string, string>> = t('ia.revision.tipos', { returnObjects: true });
  const nombreTipo = (codigo: string) => tipos[codigo.toUpperCase()] ?? codigo;
  const textos = { nombres: t('ia.revision.nombresCampos', { returnObjects: true }), si: t('ia.revision.si'), no: t('ia.revision.no'), vacio: t('ia.revision.vacio') };
  const campos = camposExtraidos(ia.campos, textos);
  const otroTipo = tipoNoCoincide(ia.tipoDetectado, tipoEsperado?.codigo);
  const recortado = (ia.paginasOmitidas ?? 0) > 0 && ia.paginasEnviadas !== null;

  return (
    <Box component="section" aria-label={t('ia.revision.titulo')}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mb: 0.75 }}>
        <Sparkles size={14} strokeWidth={1.75} aria-hidden />
        <Typography variant="overline" component="h3" sx={{ color: 'text.secondary' }}>
          {t('ia.revision.titulo')}
        </Typography>
        {confianza !== null && (
          <Tooltip title={t('ia.revision.confianzaAyuda', { umbral: PORCENTAJE.format(UMBRAL_CONFIANZA) })}>
            <Chip size="small" variant="outlined" color={alta ? 'success' : 'warning'} label={t('ia.revision.confianza', { porcentaje: PORCENTAJE.format(confianza) })} />
          </Tooltip>
        )}
        {ia.escalado && <Chip size="small" variant="outlined" label={t('ia.revision.escalado')} />}
      </Stack>
      <Stack spacing={1}>
        {otroTipo && tipoEsperado && (
          <Alert severity="warning">{t('ia.revision.tipoNoCoincide', { detectado: nombreTipo(ia.tipoDetectado), esperado: tipoEsperado.nombre })}</Alert>
        )}
        {ia.alertas.length > 0 && (
          <Alert severity="warning">
            <AlertTitle>{t('ia.revision.alertas')}</AlertTitle>
            <Box component="ul" sx={{ m: 0, pl: 2 }}>
              {ia.alertas.map((alerta) => (
                <li key={alerta}>{alerta}</li>
              ))}
            </Box>
          </Alert>
        )}
        <Box
          component="dl"
          sx={{ m: 0, display: 'grid', gridTemplateColumns: 'minmax(0, max-content) minmax(0, 1fr)', columnGap: 1.5, rowGap: 0.5, fontSize: 13, lineHeight: '18px' }}
        >
          <Box component="dt" sx={{ color: 'text.secondary' }}>
            {t('ia.revision.legible')}
          </Box>
          <Box component="dd" sx={{ m: 0, color: ia.legible ? undefined : 'estado.advertencia.texto' }}>
            {ia.legible ? t('ia.revision.legibleSi') : t('ia.revision.legibleNo')}
          </Box>
          <Box component="dt" sx={{ color: 'text.secondary' }}>
            {t('ia.revision.tipoDetectado')}
          </Box>
          <Box component="dd" sx={{ m: 0 }}>
            {nombreTipo(ia.tipoDetectado)}
          </Box>
        </Box>
        {campos.length > 0 ? (
          <CamposIa campos={campos} titulo={t('ia.revision.campos')} limite={limite} />
        ) : (
          <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 13 }}>
            {t('ia.revision.sinCampos')}
          </Typography>
        )}
        {ia.observaciones && (
          <Typography variant="body2" sx={{ fontSize: 13 }}>
            {t('ia.revision.observaciones', { texto: ia.observaciones })}
          </Typography>
        )}
        {recortado && (
          <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12 }}>
            {t('ia.revision.paginasOmitidas', { count: ia.paginasEnviadas ?? 0 })}
          </Typography>
        )}
        <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
          {t('ia.revision.analizado', { fecha: formatearFecha(ia.fecha), modelo: ia.modelo })}
        </Typography>
      </Stack>
    </Box>
  );
}
