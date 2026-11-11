import { Checkbox, FormControlLabel, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { MAXIMO_PERIODOS, SECCIONES_FICHA, seccionFicha } from '../../terceros/ficha/secciones';
import type { RequisitoEnEditor } from '../editor';
import { MAXIMO_DIAS_REVALIDACION, MAXIMO_VIGENCIA_VINCULO_DIAS } from '../vigencia';
import { CampoDias } from './CampoDias';

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
 * soporte», la vigencia propia de lo que la empresa acepta (VV-1; vacía = no vence sola), el plazo de revalidación (RV-4; vacío = 15
 * días) y la nota de que Legal y Financiera no se piden a la persona natural.
 */
export function OpcionesSeccion({
  requisito,
  onCambiar,
  deshabilitado,
  errorPeriodos,
  errorVigencia = null,
  errorRevalidacion = null,
}: Props & { errorPeriodos: string | null; errorVigencia?: string | null; errorRevalidacion?: string | null }) {
  const { t } = useTranslation();
  const definicion = seccionFicha(requisito.seccion);
  return (
    <Stack spacing={0.75} sx={{ mt: 1 }}>
      <Stack direction="row" spacing={2} useFlexGap sx={{ alignItems: 'flex-start', flexWrap: 'wrap' }}>
        {definicion?.porPeriodo && (
          <TextField
            size="small"
            type="number"
            label={t('fichaEmpresa.editor.periodos')}
            value={requisito.periodos ?? ''}
            placeholder="1"
            onChange={(ev) => onCambiar({ periodos: ev.target.value === '' ? null : Number(ev.target.value) })}
            error={Boolean(errorPeriodos)}
            helperText={errorPeriodos ?? t('fichaEmpresa.editor.periodosAyuda')}
            slotProps={{ htmlInput: { min: 1, max: MAXIMO_PERIODOS } }}
            disabled={deshabilitado}
            sx={{ width: 200 }}
          />
        )}
        <FormControlLabel
          control={<Checkbox checked={requisito.exigeSoporte} onChange={(ev) => onCambiar({ exigeSoporte: ev.target.checked })} disabled={deshabilitado} />}
          label={t('fichaEmpresa.editor.exigeSoporte')}
          sx={{ mx: 0, minHeight: 40 }}
        />
        <CampoDias
          label={t('fichaEmpresa.editor.vigenciaDias')}
          valor={String(requisito.vigenciaDias ?? '')}
          maximo={MAXIMO_VIGENCIA_VINCULO_DIAS}
          placeholder={t('fichaEmpresa.editor.noVence')}
          onCambiar={(valor) => onCambiar({ vigenciaDias: valor === '' ? null : Number(valor) })}
          error={Boolean(errorVigencia)}
          helperText={errorVigencia ?? t('fichaEmpresa.editor.vigenciaDiasAyuda')}
          disabled={deshabilitado}
          sx={{ width: 230 }}
        />
        <CampoDias
          label={t('fichaEmpresa.editor.diasRevalidacion')}
          valor={String(requisito.diasRevalidacion ?? '')}
          maximo={MAXIMO_DIAS_REVALIDACION}
          placeholder="15"
          onCambiar={(valor) => onCambiar({ diasRevalidacion: valor === '' ? null : Number(valor) })}
          error={Boolean(errorRevalidacion)}
          helperText={errorRevalidacion ?? t('fichaEmpresa.editor.diasRevalidacionAyuda')}
          disabled={deshabilitado}
          sx={{ width: 230 }}
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
