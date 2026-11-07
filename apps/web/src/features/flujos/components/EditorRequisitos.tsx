import { Autocomplete, Box, Button, Chip, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, Tooltip, Typography } from '@mui/material';
import type { AreaDto, TipoDocumentoTenantDto } from '@validasoft/api-client';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { MAXIMO_APROBADORES, requisitoNuevo, type ErrorFlujo, type EtapaEnEditor, type RequisitoEnEditor } from '../editor';

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
 * y cuántas personas deben aprobarlo (vacío = las del área). Sin documentos, el flujo decide por área (FL-1).
 */
export function EditorRequisitos({ requisitos, etapas, areas, tiposDocumento, onCambiar, errores, soloLectura }: EditorRequisitosProps) {
  const { t } = useTranslation();
  const areasDelFlujo = etapas.flatMap((e) => e.areas.map((a) => a.areaId)).filter(Boolean);
  const nombreArea = (id: string) => areas.find((a) => a.id === id)?.nombre ?? id;
  const elegibles = tiposDocumento.filter((tipo) => tipo.activo && tipo.clase === 'Archivo');
  const cambiar = (k: number, cambio: Partial<RequisitoEnEditor>) => onCambiar(requisitos.map((r, i) => (i === k ? { ...r, ...cambio } : r)));
  const error = (k: number) => errores.find((e) => e.campo === `requisito.${String(k)}`);

  return (
    <Stack spacing={1.5}>
      <Typography color="text.secondary" sx={{ fontSize: 13 }}>
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
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} alignItems={{ md: 'flex-start' }}>
              <TextField
                select
                size="small"
                label={t('flujos.editor.requisitos.tipo')}
                value={r.tipoDocumentoTenantId}
                onChange={(ev) => cambiar(k, { tipoDocumentoTenantId: ev.target.value })}
                disabled={soloLectura}
                sx={{ minWidth: 240, flex: 1 }}
              >
                {(tipo && !elegibles.includes(tipo) ? [tipo, ...elegibles] : elegibles).map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.nombre}
                  </MenuItem>
                ))}
              </TextField>
              <Autocomplete
                multiple
                size="small"
                options={areasDelFlujo}
                value={r.areasQueRevisan.filter((a) => areasDelFlujo.includes(a))}
                onChange={(_, valor) => cambiar(k, { areasQueRevisan: valor })}
                getOptionLabel={nombreArea}
                disabled={soloLectura}
                renderTags={(valor, props) => valor.map((id, i) => <Chip {...props({ index: i })} key={id} size="small" label={nombreArea(id)} />)}
                renderInput={(params) => (
                  <TextField {...params} label={t('flujos.editor.requisitos.areas')} placeholder={r.areasQueRevisan.length === 0 ? t('flujos.editor.requisitos.todas') : undefined} />
                )}
                sx={{ flex: 1.4, minWidth: 220 }}
              />
              <TextField
                size="small"
                type="number"
                label={t('flujos.editor.requisitos.aprobadores')}
                value={r.aprobadoresRequeridos ?? ''}
                placeholder={t('flujos.editor.requisitos.losDelArea')}
                onChange={(ev) => cambiar(k, { aprobadoresRequeridos: ev.target.value === '' ? null : Number(ev.target.value) })}
                slotProps={{ htmlInput: { min: 1, max: MAXIMO_APROBADORES } }}
                disabled={soloLectura}
                sx={{ width: { md: 150 } }}
              />
              <FormControlLabel
                control={<Switch checked={r.obligatorio} onChange={(ev) => cambiar(k, { obligatorio: ev.target.checked })} disabled={soloLectura} />}
                label={t('flujos.editor.requisitos.obligatorio')}
                sx={{ mx: 0, flex: 'none' }}
              />
              {!soloLectura && (
                <Tooltip title={t('flujos.editor.requisitos.quitar')}>
                  <IconButton aria-label={t('flujos.editor.requisitos.quitar')} onClick={() => onCambiar(requisitos.filter((_, i) => i !== k))}>
                    <Trash2 size={16} strokeWidth={1.75} />
                  </IconButton>
                </Tooltip>
              )}
            </Stack>
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
