import { Box, TextField, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import type { BorradorFlujo, ErrorFlujo } from '../editor';
import { MAXIMO_GRACIA_VIGENCIA_DIAS, MAXIMO_VIGENCIA_VINCULO_DIAS } from '../vigencia';

interface Props {
  borrador: BorradorFlujo;
  onCambiar: (borrador: BorradorFlujo) => void;
  errores: readonly ErrorFlujo[];
  soloLectura: boolean;
}

/**
 * Vigencia periódica del vínculo (VV-1, la «vigencia del proveedor» del SRM): a los N días de su última aprobación el vínculo
 * vence y el tercero debe ratificar o actualizar las secciones de la ficha que pide el flujo. 0 = sin vencimiento periódico. Los
 * avisos (el primero deja el vínculo «Por vencer») y la gracia se escriben como los del plazo de cargue.
 */
export function EditorVigenciaVinculo({ borrador, onCambiar, errores, soloLectura }: Props) {
  const { t } = useTranslation();
  const error = (campo: string) => {
    const e = errores.find((x) => x.campo === campo);
    return e ? t(`flujos.editor.errores.${e.codigo}`) : null;
  };
  const activa = borrador.vigenciaDias.trim() !== '' && borrador.vigenciaDias.trim() !== '0';

  return (
    <Box component="fieldset" disabled={soloLectura} sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
      <Typography component="legend" sx={{ fontSize: 13, fontWeight: 600, mb: 0.5 }}>
        {t('flujoVigencia.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, mb: 1.5 }}>{t('flujoVigencia.ayuda')}</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '160px minmax(0,320px) 160px' }, gap: 2, alignItems: 'start' }}>
        <TextField
          size="small"
          type="number"
          label={t('flujoVigencia.dias')}
          value={borrador.vigenciaDias}
          onChange={(e) => onCambiar({ ...borrador, vigenciaDias: e.target.value })}
          error={Boolean(error('vigencia.dias'))}
          helperText={error('vigencia.dias') ?? t('flujoVigencia.diasAyuda')}
          slotProps={{ htmlInput: { min: 0, max: MAXIMO_VIGENCIA_VINCULO_DIAS } }}
        />
        <TextField
          size="small"
          label={t('flujoVigencia.avisos')}
          value={borrador.vigenciaAvisos}
          disabled={soloLectura || !activa}
          onChange={(e) => onCambiar({ ...borrador, vigenciaAvisos: e.target.value })}
          error={Boolean(error('vigencia.avisos'))}
          helperText={error('vigencia.avisos') ?? t('flujoVigencia.avisosAyuda')}
        />
        <TextField
          size="small"
          type="number"
          label={t('flujoVigencia.gracia')}
          value={borrador.vigenciaGracia}
          disabled={soloLectura || !activa}
          onChange={(e) => onCambiar({ ...borrador, vigenciaGracia: e.target.value })}
          error={Boolean(error('vigencia.gracia'))}
          helperText={error('vigencia.gracia') ?? t('flujoVigencia.graciaAyuda')}
          slotProps={{ htmlInput: { min: 0, max: MAXIMO_GRACIA_VIGENCIA_DIAS } }}
        />
      </Box>
    </Box>
  );
}
