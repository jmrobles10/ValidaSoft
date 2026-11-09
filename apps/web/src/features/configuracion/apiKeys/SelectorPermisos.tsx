import { Box, Checkbox, FormControlLabel, Stack, Typography } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { alternarGrupo, alternarScope, GRUPOS_PERMISOS } from './scopes';

/** Multiselección de permisos agrupada: cada grupo se marca entero o permiso por permiso (con su scope en mono). */
export function SelectorPermisos({ valor, onCambiar }: { valor: readonly string[]; onCambiar: (scopes: string[]) => void }) {
  const { t } = useTranslation();
  return (
    <Box component="fieldset" sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
      <Typography component="legend" sx={{ fontSize: 14, fontWeight: 600 }}>
        {t('apiKeys.campos.permisos')}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
        {t('apiKeys.campos.permisosAyuda', { n: String(valor.length) })}
      </Typography>
      <Box sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px` }}>
        {GRUPOS_PERMISOS.map(({ grupo, claves }, i) => {
          const marcados = claves.filter((c) => valor.includes(SCOPES[c])).length;
          return (
            <Box key={grupo} sx={{ px: 1.5, py: 0.75, borderTop: i === 0 ? 0 : 1, borderColor: 'divider' }}>
              <FormControlLabel
                sx={{ mx: 0, '& .MuiFormControlLabel-label': { fontSize: 13, fontWeight: 600 } }}
                control={
                  <Checkbox
                    size="small"
                    checked={marcados === claves.length}
                    indeterminate={marcados > 0 && marcados < claves.length}
                    onChange={() => onCambiar(alternarGrupo(valor, grupo))}
                  />
                }
                label={t(`apiKeys.grupos.${grupo}`)}
              />
              <Stack sx={{ pl: 3.5 }}>
                {claves.map((clave) => {
                  const scope = SCOPES[clave];
                  return (
                    <FormControlLabel
                      key={clave}
                      sx={{ mx: 0, minHeight: 32 }}
                      control={<Checkbox size="small" checked={valor.includes(scope)} onChange={() => onCambiar(alternarScope(valor, scope))} />}
                      label={
                        <Box component="span" sx={{ display: 'inline-flex', flexWrap: 'wrap', alignItems: 'baseline', columnGap: 1 }}>
                          <Typography component="span" variant="body2">
                            {t(`apiKeys.permisos.${clave}`)}
                          </Typography>
                          <Typography component="span" variant="monoTabla" color="text.secondary">
                            {scope}
                          </Typography>
                        </Box>
                      }
                    />
                  );
                })}
              </Stack>
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}
