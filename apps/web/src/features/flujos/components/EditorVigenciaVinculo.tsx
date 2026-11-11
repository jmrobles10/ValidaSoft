import { Box, TextField, Typography } from '@mui/material';
import { CampoNumero } from '@validasoft/ui';
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
        <CampoNumero
          etiqueta={t('flujoVigencia.dias')}
          valor={borrador.vigenciaDias}
          maxDigitos={String(MAXIMO_VIGENCIA_VINCULO_DIAS).length}
          onCambiar={(texto) => onCambiar({ ...borrador, vigenciaDias: texto })}
          error={Boolean(error('vigencia.dias'))}
          ayuda={error('vigencia.dias') ?? t('flujoVigencia.diasAyuda')}
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
        <CampoNumero
          etiqueta={t('flujoVigencia.gracia')}
          valor={borrador.vigenciaGracia}
          maxDigitos={String(MAXIMO_GRACIA_VIGENCIA_DIAS).length}
          deshabilitado={soloLectura || !activa}
          onCambiar={(texto) => onCambiar({ ...borrador, vigenciaGracia: texto })}
          error={Boolean(error('vigencia.gracia'))}
          ayuda={error('vigencia.gracia') ?? t('flujoVigencia.graciaAyuda')}
        />
      </Box>
    </Box>
  );
}
