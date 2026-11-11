import { Box, MenuItem, TextField, Typography } from '@mui/material';
import { CampoNumero } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import type { BorradorFlujo, ErrorFlujo } from '../editor';
import { MODOS_RECORDATORIO, type ModoRecordatorio } from '../recordatorio';

interface Props {
  borrador: BorradorFlujo;
  onCambiar: (borrador: BorradorFlujo) => void;
  errores: readonly ErrorFlujo[];
  soloLectura: boolean;
}

/**
 * Recordatorio automático de observaciones del flujo (OP-19): cada cuántos días se le recuerda al tercero lo que le falta
 * corregir mientras el expediente siga devuelto. Por omisión sigue lo que la empresa configuró en Plantillas; el flujo puede
 * apagarlo o fijar sus propios días (aunque la empresa lo tenga apagado).
 */
export function EditorRecordatorioFlujo({ borrador, onCambiar, errores, soloLectura }: Props) {
  const { t } = useTranslation();
  const errorDias = errores.find((e) => e.campo === 'recordatorio.dias');
  const cadaDias = borrador.recordatorioModo === 'CadaDias';

  return (
    <Box component="fieldset" disabled={soloLectura} sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
      <Typography component="legend" sx={{ fontSize: 13, fontWeight: 600, mb: 0.5 }}>
        {t('flujoRecordatorio.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, mb: 1.5 }}>{t('flujoRecordatorio.ayuda')}</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '280px 160px' }, gap: 2, alignItems: 'start' }}>
        <TextField
          select
          label={t('flujoRecordatorio.modo')}
          value={borrador.recordatorioModo}
          disabled={soloLectura}
          onChange={(e) => onCambiar({ ...borrador, recordatorioModo: e.target.value as ModoRecordatorio })}
          helperText={t(`flujoRecordatorio.modos.${borrador.recordatorioModo}.ayuda`)}
        >
          {MODOS_RECORDATORIO.map((modo) => (
            <MenuItem key={modo} value={modo}>
              {t(`flujoRecordatorio.modos.${modo}.titulo`)}
            </MenuItem>
          ))}
        </TextField>
        {cadaDias && (
          <CampoNumero
            etiqueta={t('flujoRecordatorio.dias')}
            valor={borrador.recordatorioDias}
            maxDigitos={2}
            deshabilitado={soloLectura}
            onCambiar={(texto) => onCambiar({ ...borrador, recordatorioDias: texto })}
            error={Boolean(errorDias)}
            ayuda={errorDias ? t('flujos.editor.errores.recordatorio-invalido') : t('flujoRecordatorio.diasAyuda')}
          />
        )}
      </Box>
    </Box>
  );
}
