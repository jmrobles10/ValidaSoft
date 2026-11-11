import { Checkbox, FormControlLabel, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { CampoNumero } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { SECCIONES_FICHA, seccionFicha } from '../../terceros/ficha/secciones';
import type { RequisitoEnEditor } from '../editor';

interface Props {
  requisito: RequisitoEnEditor;
  onCambiar: (cambio: Partial<RequisitoEnEditor>) => void;
  deshabilitado: boolean;
}

/** Qué sección de la información del tercero pide el requisito (en lugar del tipo de documento). */
export function SelectorSeccion({ requisito, onCambiar, deshabilitado }: Props) {
  const { t } = useTranslation();
  return (
    <TextField
      select
      size="small"
      label={t('fichaEmpresa.editor.seccion')}
      value={requisito.seccion ?? ''}
      // Al cambiar de sección los periodos solo siguen si la nueva va por periodo.
      onChange={(ev) => onCambiar({ seccion: ev.target.value, periodos: seccionFicha(ev.target.value)?.porPeriodo ? requisito.periodos : null })}
      disabled={deshabilitado}
    >
      {SECCIONES_FICHA.map((s) => (
        <MenuItem key={s.codigo} value={s.codigo}>
          {t(`fichaEmpresa.secciones.${s.codigo}`)}
        </MenuItem>
      ))}
    </TextField>
  );
}

/**
 * Lo propio de un requisito de sección (FM-25): «Periodos» en la financiera (1–5; vacío = el último cierre), «Exige
 * soporte» y la nota de que Legal y Financiera no se piden a la persona natural.
 */
export function OpcionesSeccion({ requisito, onCambiar, deshabilitado, errorPeriodos }: Props & { errorPeriodos: string | null }) {
  const { t } = useTranslation();
  const definicion = seccionFicha(requisito.seccion);
  return (
    <Stack spacing={0.75} sx={{ mt: 1 }}>
      <Stack direction="row" spacing={2} useFlexGap sx={{ alignItems: 'flex-start', flexWrap: 'wrap' }}>
        {definicion?.porPeriodo && (
          <CampoNumero
            etiqueta={t('fichaEmpresa.editor.periodos')}
            valor={requisito.periodos}
            placeholder="1"
            onCambiar={(texto) => onCambiar({ periodos: texto === '' ? null : Number(texto) })}
            error={Boolean(errorPeriodos)}
            ayuda={errorPeriodos ?? t('fichaEmpresa.editor.periodosAyuda')}
            deshabilitado={deshabilitado}
            sx={{ width: 200 }}
          />
        )}
        <FormControlLabel
          control={<Checkbox checked={requisito.exigeSoporte} onChange={(ev) => onCambiar({ exigeSoporte: ev.target.checked })} disabled={deshabilitado} />}
          label={t('fichaEmpresa.editor.exigeSoporte')}
          sx={{ mx: 0, minHeight: 40 }}
        />
      </Stack>
      {!definicion?.porPeriodo && errorPeriodos && (
        <Typography color="error" sx={{ fontSize: 12 }}>
          {errorPeriodos}
        </Typography>
      )}
      <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12 }}>
        {definicion?.soloJuridica ? t('fichaEmpresa.editor.noAplicaNatural') : t('fichaEmpresa.editor.ayuda')}
      </Typography>
    </Stack>
  );
}
