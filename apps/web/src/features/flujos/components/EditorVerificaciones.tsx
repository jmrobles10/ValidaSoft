import { Box, FormControlLabel, Stack, Switch, Typography } from '@mui/material';
import { CampoNumero } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { TIPOS_VERIFICACION, verificacionNueva, type ErrorFlujo, type VerificacionEnEditor } from '../editor';

export interface EditorVerificacionesProps {
  verificaciones: VerificacionEnEditor[];
  onCambiar: (verificaciones: VerificacionEnEditor[]) => void;
  errores: readonly ErrorFlujo[];
  soloLectura: boolean;
}

/** Número entero de un campo (vacío = `null`, lo que no es número queda como `NaN` para que la validación lo marque). */
function numero(valor: string): number | null {
  return valor.trim() === '' ? null : Number(valor);
}

/**
 * Verificaciones de cumplimiento que pide el flujo (VE-6): listas restrictivas y estudio del oficial. Cada una se pide o
 * no, bloquea o no la aprobación y tiene su vigencia (vacía = la de la configuración); las listas, además, cada cuánto se
 * reconsultan. El proveedor de listas no va aquí: es el que configuró la empresa.
 */
export function EditorVerificaciones({ verificaciones, onCambiar, errores, soloLectura }: EditorVerificacionesProps) {
  const { t } = useTranslation();
  const cambiar = (tipo: VerificacionEnEditor['tipo'], cambio: Partial<VerificacionEnEditor>) =>
    onCambiar(verificaciones.map((v) => (v.tipo === tipo ? { ...v, ...cambio } : v)));
  const alternar = (tipo: VerificacionEnEditor['tipo'], pedir: boolean) =>
    onCambiar(pedir ? [...verificaciones, verificacionNueva(tipo)] : verificaciones.filter((v) => v.tipo !== tipo));

  return (
    <Stack spacing={1.5}>
      <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('flujos.editor.verificaciones.ayuda')}</Typography>
      {TIPOS_VERIFICACION.map((tipo) => {
        const v = verificaciones.find((x) => x.tipo === tipo);
        const nombre = t(`flujos.editor.verificaciones.tipos.${tipo}`);
        const errorDe = (codigo: ErrorFlujo['codigo']) => errores.find((e) => e.campo === `verificacion.${tipo}` && e.codigo === codigo);
        const eVigencia = errorDe('vigencia-invalida');
        const eReconsulta = errorDe('reconsulta-invalida');
        return (
          <Box
            key={tipo}
            component="section"
            aria-label={nombre}
            sx={{ border: 1, borderColor: eVigencia || eReconsulta ? 'error.main' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}
          >
            <FormControlLabel
              control={<Switch checked={Boolean(v)} onChange={(e) => alternar(tipo, e.target.checked)} />}
              disabled={soloLectura}
              sx={{ alignItems: 'flex-start', mx: 0 }}
              label={
                <Box sx={{ pt: '6px' }}>
                  <Typography component="span" sx={{ display: 'block', fontSize: 14, fontWeight: 500 }}>
                    {t('flujos.editor.verificaciones.pedir', { tipo: nombre })}
                  </Typography>
                  <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 12 }}>
                    {t(`flujos.editor.verificaciones.ayudas.${tipo}`)}
                  </Typography>
                </Box>
              }
            />
            {v && (
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1.5, alignItems: 'flex-start', mt: 1.5, pl: { sm: 6 } }}>
                <FormControlLabel
                  control={<Switch checked={v.bloqueante} onChange={(e) => cambiar(tipo, { bloqueante: e.target.checked })} />}
                  disabled={soloLectura}
                  label={t('flujos.editor.verificaciones.bloqueante')}
                  sx={{ mx: 0, mt: 0.5 }}
                />
                {tipo === 'EstudioDeCumplimiento' ? (
                  <CampoNumero
                    etiqueta={t('flujos.editor.verificaciones.vigenciaDias')}
                    valor={v.vigenciaDias}
                    onCambiar={(texto) => cambiar(tipo, { vigenciaDias: numero(texto) })}
                    deshabilitado={soloLectura}
                    error={Boolean(eVigencia)}
                    ayuda={eVigencia ? t(`flujos.editor.errores.${eVigencia.codigo}`) : t('flujos.editor.verificaciones.vigenciaAyuda')}
                    sx={{ width: 200 }}
                  />
                ) : (
                  <CampoNumero
                    etiqueta={t('flujos.editor.verificaciones.reconsultaMeses')}
                    valor={v.frecuenciaReconsultaMeses}
                    onCambiar={(texto) => cambiar(tipo, { frecuenciaReconsultaMeses: numero(texto) })}
                    deshabilitado={soloLectura}
                    error={Boolean(eReconsulta)}
                    ayuda={eReconsulta ? t(`flujos.editor.errores.${eReconsulta.codigo}`) : t('flujos.editor.verificaciones.reconsultaAyuda')}
                    sx={{ width: 220 }}
                  />
                )}
              </Box>
            )}
          </Box>
        );
      })}
    </Stack>
  );
}
