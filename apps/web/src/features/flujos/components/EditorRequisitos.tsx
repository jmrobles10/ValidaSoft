import { Autocomplete, Box, Button, Chip, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, Tooltip, Typography } from '@mui/material';
import type { AreaDto, TipoDocumentoTenantDto } from '@validasoft/api-client';
import { AlineadoConCampo } from '@validasoft/ui';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { resumenTipo } from '../../configuracion/tiposDocumento/resumen';
import { atajoDe, condicionDe, describirCondicion, type AtajoCondicion } from '../condiciones';
import { MAXIMO_APROBADORES, requisitoNuevo, sePuedePedir, type ErrorFlujo, type EtapaEnEditor, type RequisitoEnEditor } from '../editor';

/** Chips de áreas a la vista en «Lo revisan»; el resto va en un chip «+N». */
const CHIPS_VISIBLES = 2;

export interface EditorRequisitosProps {
  requisitos: RequisitoEnEditor[];
  etapas: readonly EtapaEnEditor[];
  areas: readonly AreaDto[];
  tiposDocumento: readonly TipoDocumentoTenantDto[];
  onCambiar: (requisitos: RequisitoEnEditor[]) => void;
  errores: readonly ErrorFlujo[];
  soloLectura: boolean;
}

/**
 * Documentos que pide el flujo (RQ-1): tipo, obligatorio, qué áreas lo revisan (ninguna elegida = todas las del flujo)
 * y cuántas personas deben aprobarlo (vacío = las del área); a quién se le pide (RQ-12: todos, solo personas jurídicas o
 * naturales; una condición armada por la API se describe y se conserva). Sin documentos, el flujo decide por área (FL-1).
 */
export function EditorRequisitos({ requisitos, etapas, areas, tiposDocumento, onCambiar, errores, soloLectura }: EditorRequisitosProps) {
  const { t } = useTranslation();
  const areasDelFlujo = etapas.flatMap((e) => e.areas.map((a) => a.areaId)).filter(Boolean);
  const nombreArea = (id: string) => areas.find((a) => a.id === id)?.nombre ?? id;
  const elegibles = tiposDocumento.filter(sePuedePedir);
  const cambiar = (k: number, cambio: Partial<RequisitoEnEditor>) => onCambiar(requisitos.map((r, i) => (i === k ? { ...r, ...cambio } : r)));
  const error = (k: number) => errores.find((e) => e.campo === `requisito.${String(k)}`);

  return (
    <Stack spacing={1.5}>
      <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
        {requisitos.length === 0 ? t('flujos.editor.requisitos.vacio') : t('flujos.editor.requisitos.ayuda')}
      </Typography>
      {requisitos.map((r, k) => {
        const e = error(k);
        const tipo = tiposDocumento.find((x) => x.id === r.tipoDocumentoTenantId);
        return (
          <Box
            key={r.clave}
            component="section"
            aria-label={tipo?.nombre ?? t('flujos.editor.requisitos.nuevo')}
            sx={{ border: 1, borderColor: e ? 'error.main' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}
          >
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: {
                  xs: '1fr',
                  sm: 'minmax(180px,1fr) minmax(180px,1fr)',
                  lg: 'minmax(180px,1.2fr) minmax(180px,1.2fr) minmax(170px,1fr) 120px auto',
                },
                columnGap: 1.5,
                rowGap: 1,
                alignItems: 'start',
              }}
            >
              <TextField
                select
                size="small"
                label={t('flujos.editor.requisitos.tipo')}
                value={r.tipoDocumentoTenantId}
                onChange={(ev) => cambiar(k, { tipoDocumentoTenantId: ev.target.value })}
                disabled={soloLectura}
              >
                {(tipo && !elegibles.includes(tipo) ? [tipo, ...elegibles] : elegibles).map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.nombre}
                  </MenuItem>
                ))}
              </TextField>
              {/* Chips en una sola línea (los que no caben se cortan con puntos suspensivos; desde la tercera área, «+N»):
                  el campo mide lo mismo que los demás de la fila en vez de crecer hacia abajo. */}
              <Autocomplete
                multiple
                size="small"
                disableCloseOnSelect
                // Cada chip tiene su ✕: el botón de limpiar todo solo restaba espacio a los chips.
                disableClearable
                options={areasDelFlujo}
                value={r.areasQueRevisan.filter((a) => areasDelFlujo.includes(a))}
                onChange={(_, valor) => cambiar(k, { areasQueRevisan: valor })}
                getOptionLabel={nombreArea}
                disabled={soloLectura}
                renderValue={(valor, getItemProps) => {
                  const ocultas = valor.slice(CHIPS_VISIBLES);
                  return (
                    <Box className="chips-areas" sx={{ display: 'flex', alignItems: 'center', flex: '0 1 auto', minWidth: 0, overflow: 'hidden', pl: 0.5 }}>
                      {valor.slice(0, CHIPS_VISIBLES).map((id, index) => {
                        const { key, ...props } = getItemProps({ index });
                        return <Chip key={key} {...props} size="small" label={nombreArea(id)} title={nombreArea(id)} sx={{ minWidth: 0, flexShrink: 1 }} />;
                      })}
                      {ocultas.length > 0 && (
                        <Chip size="small" variant="outlined" label={`+${String(ocultas.length)}`} title={ocultas.map(nombreArea).join(', ')} sx={{ flex: 'none', m: '2px' }} />
                      )}
                    </Box>
                  );
                }}
                renderInput={(params) => (
                  <TextField {...params} label={t('flujos.editor.requisitos.areas')} placeholder={r.areasQueRevisan.length === 0 ? t('flujos.editor.requisitos.todas') : undefined} />
                )}
                // MUI limita cada chip a `calc(100% - 6px)` de su contenedor, que aquí es la fila de chips (se encogían sin
                // necesidad): tope fijo, y solo se cortan con puntos suspensivos cuando de verdad no caben.
                // Con chips, el hueco para escribir se achica (se puede seguir escribiendo) para dejarles el espacio.
                sx={{
                  minWidth: 0,
                  '& .MuiOutlinedInput-root': { flexWrap: 'nowrap' },
                  '& .MuiAutocomplete-tag': { maxWidth: 160 },
                  '& .chips-areas + .MuiAutocomplete-input': { minWidth: 8, px: 0.5 },
                }}
              />
              <TextField
                select
                size="small"
                label={t('flujos.editor.requisitos.sePideA')}
                value={atajoDe(r.condicion)}
                onChange={(ev) => cambiar(k, { condicion: condicionDe(ev.target.value as AtajoCondicion, r.condicion) })}
                disabled={soloLectura}
              >
                {(['siempre', 'juridica', 'natural'] as const).map((a) => (
                  <MenuItem key={a} value={a}>
                    {t(`flujos.editor.requisitos.atajos.${a}`)}
                  </MenuItem>
                ))}
                {atajoDe(r.condicion) === 'personalizada' && <MenuItem value="personalizada">{t('flujos.editor.requisitos.atajos.personalizada')}</MenuItem>}
              </TextField>
              <TextField
                size="small"
                type="number"
                label={t('flujos.editor.requisitos.aprobadores')}
                value={r.aprobadoresRequeridos ?? ''}
                placeholder={t('flujos.editor.requisitos.losDelArea')}
                onChange={(ev) => cambiar(k, { aprobadoresRequeridos: ev.target.value === '' ? null : Number(ev.target.value) })}
                slotProps={{ htmlInput: { min: 1, max: MAXIMO_APROBADORES } }}
                disabled={soloLectura}
              />
              <AlineadoConCampo desde="sm">
              <FormControlLabel
                control={<Switch checked={r.obligatorio} onChange={(ev) => cambiar(k, { obligatorio: ev.target.checked })} disabled={soloLectura} />}
                label={t('flujos.editor.requisitos.obligatorio')}
                sx={{ mx: 0, flex: 'none' }}
              />
              {!soloLectura && (
                <Tooltip title={t('flujos.editor.requisitos.quitar')}>
                  <IconButton size="small" aria-label={t('flujos.editor.requisitos.quitar')} onClick={() => onCambiar(requisitos.filter((_, i) => i !== k))} sx={{ ml: 'auto' }}>
                    <Trash2 size={16} strokeWidth={1.75} />
                  </IconButton>
                </Tooltip>
              )}
              </AlineadoConCampo>
            </Box>
            {tipo && (
              <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12, mt: 0.75 }} data-resumen-tipo="">
                {resumenTipo(tipo, t)}
              </Typography>
            )}
            {r.condicion && atajoDe(r.condicion) === 'personalizada' && (
              <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5 }}>
                {t('flujos.editor.requisitos.personalizada', { condicion: describirCondicion(r.condicion, t) })}
              </Typography>
            )}
            {e && (
              <Typography color="error" sx={{ fontSize: 12, mt: 1 }}>
                {t(`flujos.editor.errores.${e.codigo}`)}
              </Typography>
            )}
          </Box>
        );
      })}
      {!soloLectura && (
        <Box>
          <Button variant="outlined" size="small" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...requisitos, requisitoNuevo()])}>
            {t('flujos.editor.requisitos.agregar')}
          </Button>
        </Box>
      )}
    </Stack>
  );
}
