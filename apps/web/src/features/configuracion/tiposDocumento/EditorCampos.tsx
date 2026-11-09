import { Box, Button, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, Typography } from '@mui/material';
import { ArrowDown, ArrowUp, CalendarPlus, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { campoNuevo, claveDesdeEtiqueta, conCampos, MAXIMO_CAMPOS, MODOS, TIPOS_CAMPO, type CampoBorrador, type ErrorBorrador, type Modo, type TipoCampo } from './modelo';
import { Seccion, type PropsSeccion } from './SeccionesTipo';

/** Cómo se entrega el documento y, si lleva formulario, qué campos llena el proveedor. */
export function EditorCampos({ borrador, cambiar, error, erroresCampos }: PropsSeccion & { erroresCampos: (indice: number) => ErrorBorrador | undefined }) {
  const { t } = useTranslation();
  const campos = borrador.campos;
  const cambiarCampo = (indice: number, parcial: Partial<CampoBorrador>) => cambiar({ campos: campos.map((c, i) => (i === indice ? { ...c, ...parcial } : c)) });
  const mover = (indice: number, paso: -1 | 1) => {
    const nuevos = [...campos];
    const [campo] = nuevos.splice(indice, 1);
    if (campo) nuevos.splice(indice + paso, 0, campo);
    cambiar({ campos: nuevos });
  };
  const tieneExpedicion = campos.some((c) => c.clave === 'fecha_expedicion');

  return (
    <Seccion titulo={t('tiposDocumento.secciones.entrega')} ayuda={t('tiposDocumento.entrega.ayuda')}>
      <TextField select label={t('tiposDocumento.entrega.modo')} value={borrador.modo} onChange={(e) => cambiar({ modo: e.target.value as Modo, ...(conCampos(e.target.value as Modo) && { ambito: 'PorVinculo' }) })} helperText={t(`tiposDocumento.entrega.modosAyuda.${borrador.modo}`)}>
        {MODOS.map((m) => (
          <MenuItem key={m} value={m}>
            {t(`tiposDocumento.entrega.modos.${m}`)}
          </MenuItem>
        ))}
      </TextField>

      {conCampos(borrador.modo) && (
        <>
          {error('campos') && <Typography color="error" variant="body2">{error('campos')}</Typography>}
          <Stack component="ol" spacing={1.5} sx={{ m: 0, p: 0, listStyle: 'none' }} aria-label={t('tiposDocumento.entrega.campos')}>
            {campos.map((c, i) => {
              const fallo = erroresCampos(i);
              return (
                <Box component="li" key={i} sx={{ border: 1, borderColor: fallo ? 'error.main' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                    <TextField
                      size="small"
                      required
                      label={t('tiposDocumento.entrega.etiqueta')}
                      value={c.etiqueta}
                      onChange={(e) => cambiarCampo(i, { etiqueta: e.target.value, ...(c.clave === claveDesdeEtiqueta(c.etiqueta) && { clave: claveDesdeEtiqueta(e.target.value) }) })}
                      sx={{ flex: 2 }}
                    />
                    <TextField
                      size="small"
                      required
                      label={t('tiposDocumento.entrega.clave')}
                      value={c.clave}
                      onChange={(e) => cambiarCampo(i, { clave: e.target.value.toLowerCase().replace(/[^a-z0-9_]/g, '_') })}
                      sx={{ flex: 2, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
                    />
                    <TextField size="small" select label={t('tiposDocumento.entrega.tipo')} value={c.tipo} onChange={(e) => cambiarCampo(i, { tipo: e.target.value as TipoCampo })} sx={{ flex: 1, minWidth: 120 }}>
                      {TIPOS_CAMPO.map((tc) => (
                        <MenuItem key={tc} value={tc}>
                          {t(`tiposDocumento.entrega.tipos.${tc}`)}
                        </MenuItem>
                      ))}
                    </TextField>
                  </Stack>
                  {c.tipo === 'Lista' && (
                    <TextField size="small" multiline minRows={2} fullWidth label={t('tiposDocumento.entrega.opciones')} helperText={t('tiposDocumento.entrega.opcionesAyuda')} value={c.opciones} onChange={(e) => cambiarCampo(i, { opciones: e.target.value })} sx={{ mt: 1.5 }} />
                  )}
                  <TextField size="small" fullWidth label={t('tiposDocumento.entrega.ayudaCampo')} value={c.ayuda} onChange={(e) => cambiarCampo(i, { ayuda: e.target.value })} sx={{ mt: 1.5 }} />
                  <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', mt: 1 }}>
                    <FormControlLabel control={<Switch checked={c.obligatorio} onChange={(e) => cambiarCampo(i, { obligatorio: e.target.checked })} />} label={t('tiposDocumento.entrega.obligatorio')} sx={{ mr: 'auto' }} />
                    <IconButton size="small" aria-label={t('tiposDocumento.entrega.subir', { campo: c.etiqueta || String(i + 1) })} disabled={i === 0} onClick={() => mover(i, -1)}>
                      <ArrowUp size={16} aria-hidden />
                    </IconButton>
                    <IconButton size="small" aria-label={t('tiposDocumento.entrega.bajar', { campo: c.etiqueta || String(i + 1) })} disabled={i === campos.length - 1} onClick={() => mover(i, 1)}>
                      <ArrowDown size={16} aria-hidden />
                    </IconButton>
                    <IconButton size="small" aria-label={t('tiposDocumento.entrega.quitar', { campo: c.etiqueta || String(i + 1) })} onClick={() => cambiar({ campos: campos.filter((_, j) => j !== i) })}>
                      <Trash2 size={16} aria-hidden />
                    </IconButton>
                  </Stack>
                  {fallo && (
                    <Typography color="error" variant="caption" sx={{ display: 'block' }}>
                      {t(`tiposDocumento.errores.${fallo.codigo}`)}
                    </Typography>
                  )}
                </Box>
              );
            })}
          </Stack>
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }} useFlexGap>
            <Button variant="outlined" startIcon={<Plus size={16} aria-hidden />} disabled={campos.length >= MAXIMO_CAMPOS} onClick={() => cambiar({ campos: [...campos, campoNuevo()] })}>
              {t('tiposDocumento.entrega.agregar')}
            </Button>
            {!tieneExpedicion && (
              <Button
                variant="text"
                startIcon={<CalendarPlus size={16} aria-hidden />}
                onClick={() => cambiar({ campos: [...campos, { ...campoNuevo(), clave: 'fecha_expedicion', etiqueta: t('tiposDocumento.entrega.fechaExpedicion'), tipo: 'Fecha', obligatorio: true }] })}
              >
                {t('tiposDocumento.entrega.agregarExpedicion')}
              </Button>
            )}
          </Stack>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('tiposDocumento.entrega.reservadas')}
          </Typography>
        </>
      )}
    </Seccion>
  );
}
