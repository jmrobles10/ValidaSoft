import { Alert, Box, Button, Checkbox, FormControlLabel, IconButton, MenuItem, Stack, TextField, Tooltip, Typography } from '@mui/material';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { atributoNuevo, conOpciones, TIPOS_ATRIBUTO, type AtributoBorrador, type ErrorTipoVinculo, type TipoAtributo } from './modelo';

/**
 * Atributos permitidos del tipo: una fila por atributo dentro de un solo marco (clave, tipo, obligatorio y, para
 * Opcion/ListaOpciones, sus opciones). Los flujos los usan en «Se pide a».
 */
export function EditorAtributos({
  atributos,
  onCambiar,
  errorDe,
  editando,
}: {
  atributos: readonly AtributoBorrador[];
  onCambiar: (atributos: AtributoBorrador[]) => void;
  /** Error de la fila (ya filtrado por lo que la persona tocó). */
  errorDe: (indice: number) => ErrorTipoVinculo | undefined;
  /** Editando un tipo que ya existe: quitar o cambiar atributos afecta a los flujos. */
  editando: boolean;
}) {
  const { t } = useTranslation();
  const cambiar = (indice: number, parcial: Partial<AtributoBorrador>) => onCambiar(atributos.map((a, i) => (i === indice ? { ...a, ...parcial } : a)));

  return (
    <Box component="fieldset" sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
      <Typography component="legend" sx={{ fontSize: 14, fontWeight: 600 }}>
        {t('tiposVinculo.atributos.titulo')}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
        {t('tiposVinculo.atributos.ayuda')}
      </Typography>
      {editando && atributos.length > 0 && (
        <Alert severity="info" sx={{ mb: 1 }}>
          {t('tiposVinculo.atributos.avisoFlujos')}
        </Alert>
      )}
      {atributos.length > 0 && (
        <Box component="ol" aria-label={t('tiposVinculo.atributos.titulo')} sx={{ m: 0, mb: 1, p: 0, listStyle: 'none', border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px` }}>
          {atributos.map((a, i) => {
            const fallo = errorDe(i);
            const nombre = a.clave.trim() || String(i + 1);
            return (
              <Box component="li" key={i} sx={{ px: 1.5, py: 1, borderTop: i === 0 ? 0 : 1, borderColor: 'divider' }}>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ sm: 'center' }}>
                  <TextField
                    size="small"
                    required
                    label={t('tiposVinculo.atributos.clave')}
                    value={a.clave}
                    onChange={(e) => cambiar(i, { clave: e.target.value.replace(/\s/g, '') })}
                    error={fallo !== undefined && fallo.codigo !== 'opciones-requeridas'}
                    slotProps={{ htmlInput: { spellCheck: false, autoCapitalize: 'none' } }}
                    sx={{ flex: 1.4, minWidth: 0, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
                  />
                  <TextField
                    size="small"
                    select
                    label={t('tiposVinculo.atributos.tipo')}
                    value={a.tipo}
                    onChange={(e) => cambiar(i, { tipo: e.target.value as TipoAtributo })}
                    sx={{ flex: 1, minWidth: 150 }}
                  >
                    {TIPOS_ATRIBUTO.map((tipo) => (
                      <MenuItem key={tipo} value={tipo}>
                        {t(`tiposVinculo.atributos.tipos.${tipo}`)}
                      </MenuItem>
                    ))}
                  </TextField>
                  <FormControlLabel
                    control={<Checkbox size="small" checked={a.obligatorio} onChange={(e) => cambiar(i, { obligatorio: e.target.checked })} />}
                    label={t('tiposVinculo.atributos.obligatorio')}
                    sx={{ mx: 0, flex: 'none' }}
                  />
                  <Tooltip title={t('tiposVinculo.atributos.quitar', { clave: nombre })}>
                    <IconButton size="small" aria-label={t('tiposVinculo.atributos.quitar', { clave: nombre })} onClick={() => onCambiar(atributos.filter((_, j) => j !== i))} sx={{ alignSelf: { xs: 'flex-end', sm: 'center' } }}>
                      <Trash2 size={16} aria-hidden />
                    </IconButton>
                  </Tooltip>
                </Stack>
                {conOpciones(a.tipo) && (
                  <TextField
                    size="small"
                    multiline
                    minRows={2}
                    fullWidth
                    required
                    label={t('tiposVinculo.atributos.opciones')}
                    helperText={t('tiposVinculo.atributos.opcionesAyuda')}
                    value={a.opciones}
                    onChange={(e) => cambiar(i, { opciones: e.target.value })}
                    error={fallo?.codigo === 'opciones-requeridas'}
                    sx={{ mt: 1 }}
                  />
                )}
                {fallo && (
                  <Typography color="error" variant="caption" sx={{ display: 'block', mt: 0.5 }}>
                    {t(`tiposVinculo.errores.${fallo.codigo}`, { clave: nombre })}
                  </Typography>
                )}
              </Box>
            );
          })}
        </Box>
      )}
      <Button variant="text" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...atributos, atributoNuevo()])}>
        {t('tiposVinculo.atributos.agregar')}
      </Button>
    </Box>
  );
}
