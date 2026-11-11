import { Alert, Box, FormControlLabel, FormHelperText, FormLabel, MenuItem, Skeleton, Stack, Switch, TextField, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import type { DebidaDiligencia, FactorDeRiesgoDto, MatrizDeRiesgoDto, ResultadoDeMatrizDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../../shared/errores';
import {
  claveRiesgo,
  DEBIDAS_EN_ORDEN,
  debidaEfectiva,
  debidaMinima,
  esAjusteDeNivel,
  intensidad,
  LONGITUD_MINIMA_JUSTIFICACION,
  nivelEfectivo,
  respuestasCompletas,
  type BorradorEstudio,
  type ErrorEstudio,
} from '../modelo';
import { ResultadoMatriz } from './ResultadoMatriz';

export interface SeccionMatrizProps {
  matriz: MatrizDeRiesgoDto;
  factores: readonly FactorDeRiesgoDto[];
  borrador: BorradorEstudio;
  cambiar: (cambio: Partial<BorradorEstudio>) => void;
  resultado: ResultadoDeMatrizDto | null;
  recalculando: boolean;
  errorDeCalculo: unknown;
  visible: (codigo: ErrorEstudio) => boolean;
}

/**
 * Estudio con matriz activa (CU-17): el oficial responde los factores manuales, ve el resultado en vivo (puntaje, nivel, piso,
 * debida diligencia y desglose) y, si su criterio difiere, ajusta el nivel con una justificación. La debida diligencia nunca
 * queda más ligera que la que exige el nivel.
 */
export function SeccionMatriz({ matriz, factores, borrador, cambiar, resultado, recalculando, errorDeCalculo, visible }: SeccionMatrizProps) {
  const { t } = useTranslation();
  const completas = respuestasCompletas(factores, borrador.respuestas);
  const nivel = nivelEfectivo(borrador, resultado);
  const minima = nivel === null ? null : debidaMinima(matriz.niveles, nivel);
  const ajuste = esAjusteDeNivel(borrador, resultado);
  const faltaJustificacion = ajuste && borrador.justificacionDeNivel.trim().length < LONGITUD_MINIMA_JUSTIFICACION;

  return (
    <Stack spacing={2} component="section" aria-label={t('cumplimiento.matriz.titulo')}>
      <Box>
        <Typography sx={{ fontSize: 13, fontWeight: 500 }}>{t('cumplimiento.matriz.titulo')}</Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('cumplimiento.matriz.ayuda')}</Typography>
      </Box>
      {factores.length > 0 && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 1.5 }}>
          {factores.map((f) => (
            <TextField
              key={f.codigo}
              select
              label={f.nombre}
              value={borrador.respuestas[f.codigo] ?? ''}
              onChange={(e) => cambiar({ respuestas: { ...borrador.respuestas, [f.codigo]: e.target.value } })}
              error={visible('respuestas-incompletas') && !borrador.respuestas[f.codigo]}
              helperText={f.ayuda ?? undefined}
              slotProps={{ select: { displayEmpty: true } }}
            >
              <MenuItem value="" disabled>
                {t('cumplimiento.matriz.elige')}
              </MenuItem>
              {f.opciones.map((o) => (
                <MenuItem key={o.codigo} value={o.codigo}>
                  {o.nombre}
                </MenuItem>
              ))}
            </TextField>
          ))}
        </Box>
      )}
      {visible('respuestas-incompletas') && <FormHelperText error>{t('cumplimiento.errores.respuestas-incompletas')}</FormHelperText>}

      {!completas ? (
        <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.matriz.responde')}</Typography>
      ) : errorDeCalculo !== null && errorDeCalculo !== undefined && !resultado ? (
        <Alert severity="error">{`${t('cumplimiento.matriz.errorCalculo')} ${mensajeDeError(errorDeCalculo, t)}`}</Alert>
      ) : resultado ? (
        <ResultadoMatriz resultado={resultado} recalculando={recalculando} />
      ) : (
        <Skeleton variant="rounded" height={104} role="status" aria-busy aria-label={t('cumplimiento.matriz.calculando')} />
      )}

      {resultado && nivel !== null && minima !== null && (
        <>
          <Box>
            <FormControlLabel
              control={
                <Switch
                  checked={borrador.ajustarNivel}
                  onChange={(e) => cambiar(e.target.checked ? { ajustarNivel: true, nivelRiesgo: resultado.nivel as 1 | 2 | 3 } : { ajustarNivel: false, nivelRiesgo: null, justificacionDeNivel: '' })}
                />
              }
              label={t('cumplimiento.matriz.ajustar')}
              sx={{ mx: 0 }}
            />
            <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('cumplimiento.matriz.ajustarAyuda')}</Typography>
          </Box>
          {borrador.ajustarNivel && (
            <Stack spacing={1.5}>
              <Box>
                <FormLabel id="nivel-ajustado" sx={{ fontSize: 13 }}>
                  {t('cumplimiento.matriz.nivelAjustado')}
                </FormLabel>
                <ToggleButtonGroup
                  exclusive
                  size="small"
                  aria-labelledby="nivel-ajustado"
                  value={borrador.nivelRiesgo}
                  onChange={(_, valor: 1 | 2 | 3 | null) => valor !== null && cambiar({ nivelRiesgo: valor })}
                  sx={{ display: 'flex', mt: 0.5 }}
                >
                  {([1, 2, 3] as const).map((n) => (
                    <ToggleButton key={n} value={n} sx={{ flex: 1 }}>
                      {t(`cumplimiento.riesgos.${claveRiesgo(n)}`)}
                    </ToggleButton>
                  ))}
                </ToggleButtonGroup>
              </Box>
              {ajuste && (
                <TextField
                  label={t('cumplimiento.matriz.justificacion')}
                  value={borrador.justificacionDeNivel}
                  onChange={(e) => cambiar({ justificacionDeNivel: e.target.value })}
                  multiline
                  minRows={2}
                  required
                  error={visible('justificacion-de-nivel-requerida') && faltaJustificacion}
                  helperText={visible('justificacion-de-nivel-requerida') && faltaJustificacion ? t('cumplimiento.errores.justificacion-de-nivel-requerida') : t('cumplimiento.matriz.justificacionAyuda')}
                  slotProps={{ htmlInput: { maxLength: 4000 } }}
                />
              )}
            </Stack>
          )}
          <TextField
            select
            label={t('cumplimiento.panel.estudio.debida')}
            value={debidaEfectiva(borrador, matriz.niveles, nivel)}
            onChange={(e) => cambiar({ debidaElegida: e.target.value as DebidaDiligencia })}
            helperText={t('cumplimiento.matriz.debidaMinima', { debida: t(`cumplimiento.debida.${minima}`) })}
          >
            {DEBIDAS_EN_ORDEN.map((d) => (
              <MenuItem key={d} value={d} disabled={intensidad(d) < intensidad(minima)}>
                {t(`cumplimiento.debida.${d}`)}
              </MenuItem>
            ))}
          </TextField>
        </>
      )}
    </Stack>
  );
}
