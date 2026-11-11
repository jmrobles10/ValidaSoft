import { Box, Button, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, Tooltip, Typography } from '@mui/material';
import type { AreaDto } from '@validasoft/api-client';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-react';
import { AlineadoConCampo, CampoNumero } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { areaNueva, areasElegibles, moverEtapa, nuevaClave, type AreaEnEditor, type ErrorFlujo, type EtapaEnEditor } from '../editor';

export interface EditorEtapasProps {
  etapas: EtapaEnEditor[];
  areas: readonly AreaDto[];
  onCambiar: (etapas: EtapaEnEditor[]) => void;
  /** Errores visibles (ya filtrados por "intentado"). */
  errores: readonly ErrorFlujo[];
  soloLectura: boolean;
}

/** Etapas en secuencia; en cada una, áreas en paralelo con sus aprobadores, permiso de rechazo, automática y SLA. */
export function EditorEtapas({ etapas, areas, onCambiar, errores, soloLectura }: EditorEtapasProps) {
  const { t } = useTranslation();
  const error = (campo: string) => errores.find((e) => e.campo === campo);
  const cambiarArea = (i: number, j: number, cambio: Partial<AreaEnEditor>) =>
    onCambiar(etapas.map((e, ie) => (ie === i ? { ...e, areas: e.areas.map((a, ja) => (ja === j ? { ...a, ...cambio } : a)) } : e)));
  const quitarArea = (i: number, j: number) => onCambiar(etapas.map((e, ie) => (ie === i ? { ...e, areas: e.areas.filter((_, ja) => ja !== j) } : e)));
  const agregarArea = (i: number) => onCambiar(etapas.map((e, ie) => (ie === i ? { ...e, areas: [...e.areas, areaNueva()] } : e)));

  return (
    <Stack spacing={1.5}>
      {etapas.map((etapa, i) => {
        const orden = String(i + 1);
        const errorEtapa = error(`etapa.${String(i)}`);
        return (
          <Box key={etapa.clave} component="section" aria-label={t('flujos.editor.etapa', { orden })} sx={{ border: 1, borderColor: errorEtapa ? 'error.main' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
              <Box>
                <Typography component="h4" sx={{ fontWeight: 600, fontSize: 14 }}>
                  {t('flujos.editor.etapa', { orden })}
                </Typography>
                <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>
                  {errorEtapa ? t(`flujos.editor.errores.${errorEtapa.codigo}`) : t('flujos.editor.etapaAyuda')}
                </Typography>
              </Box>
              {!soloLectura && (
                <Stack direction="row" spacing={0.25}>
                  <Tooltip title={t('flujos.editor.subirEtapa', { orden })}>
                    <span>
                      <IconButton size="small" aria-label={t('flujos.editor.subirEtapa', { orden })} disabled={i === 0} onClick={() => onCambiar(moverEtapa(etapas, i, -1))}>
                        <ArrowUp size={16} strokeWidth={1.75} />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <Tooltip title={t('flujos.editor.bajarEtapa', { orden })}>
                    <span>
                      <IconButton size="small" aria-label={t('flujos.editor.bajarEtapa', { orden })} disabled={i === etapas.length - 1} onClick={() => onCambiar(moverEtapa(etapas, i, 1))}>
                        <ArrowDown size={16} strokeWidth={1.75} />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <Tooltip title={t('flujos.editor.quitarEtapa', { orden })}>
                    <IconButton size="small" aria-label={t('flujos.editor.quitarEtapa', { orden })} onClick={() => onCambiar(etapas.filter((_, ie) => ie !== i))}>
                      <Trash2 size={16} strokeWidth={1.75} />
                    </IconButton>
                  </Tooltip>
                </Stack>
              )}
            </Stack>

            <Stack spacing={1}>
              {etapa.areas.map((area, j) => {
                const errorArea = error(`area.${String(i)}.${String(j)}`);
                const elegibles = areasElegibles(areas, etapas, area.areaId);
                const miembros = areas.find((a) => a.id === area.areaId)?.miembros.length;
                return (
                  <Box
                    key={`${etapa.clave}-${String(j)}`}
                    sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'minmax(160px,1fr) 104px 104px', lg: 'minmax(180px,1fr) 104px 104px auto' }, columnGap: 1, rowGap: 0.5, alignItems: 'start' }}
                  >
                    <TextField
                      select
                      size="small"
                      label={t('flujos.editor.area')}
                      value={area.areaId}
                      onChange={(e) => cambiarArea(i, j, { areaId: e.target.value })}
                      disabled={soloLectura}
                      error={Boolean(errorArea && !errorArea.aviso)}
                      helperText={errorArea ? t(`flujos.editor.errores.${errorArea.codigo}`) : miembros !== undefined ? t('flujos.editor.miembros', { count: miembros }) : ' '}
                      sx={errorArea?.aviso ? { '& .MuiFormHelperText-root': { color: 'estado.advertencia.texto' } } : undefined}
                    >
                      <MenuItem value="" disabled>
                        {t('flujos.editor.elegirArea')}
                      </MenuItem>
                      {elegibles.map((a) => (
                        <MenuItem key={a.id} value={a.id}>
                          {a.nombre}
                        </MenuItem>
                      ))}
                    </TextField>
                    <CampoNumero
                      etiqueta={t('flujos.editor.aprobadores')}
                      valor={area.automatica ? 1 : area.aprobadoresRequeridos}
                      onCambiar={(texto) => cambiarArea(i, j, { aprobadoresRequeridos: Number(texto) })}
                      deshabilitado={soloLectura || area.automatica}
                    />
                    <CampoNumero
                      etiqueta={t('flujos.editor.sla')}
                      valor={area.slaHoras}
                      onCambiar={(texto) => cambiarArea(i, j, { slaHoras: texto === '' ? null : Number(texto) })}
                      deshabilitado={soloLectura}
                    />
                    {/* Switches y «Quitar área» a la altura de los campos (no de sus etiquetas); en pantallas medianas, en una línea aparte. */}
                    <AlineadoConCampo desde="lg" columna={{ xs: '1 / -1', lg: 'auto' }}>
                    <FormControlLabel
                      control={<Switch checked={area.puedeRechazarExpediente && !area.automatica} onChange={(e) => cambiarArea(i, j, { puedeRechazarExpediente: e.target.checked })} />}
                      label={t('flujos.editor.puedeRechazar')}
                      disabled={soloLectura || area.automatica}
                      sx={{ mx: 0, '& .MuiFormControlLabel-label': { fontSize: 13, whiteSpace: 'nowrap' } }}
                    />
                    <Tooltip title={t('flujos.editor.automaticaAyuda')}>
                      <FormControlLabel
                        control={<Switch checked={area.automatica} onChange={(e) => cambiarArea(i, j, { automatica: e.target.checked, puedeRechazarExpediente: e.target.checked ? false : area.puedeRechazarExpediente })} />}
                        label={t('flujos.editor.automatica')}
                        disabled={soloLectura}
                        sx={{ mx: 0, '& .MuiFormControlLabel-label': { fontSize: 13, whiteSpace: 'nowrap' } }}
                      />
                    </Tooltip>
                    {!soloLectura && (
                      <IconButton size="small" aria-label={t('flujos.editor.quitarArea', { orden })} onClick={() => quitarArea(i, j)} sx={{ ml: 'auto' }}>
                        <X size={16} strokeWidth={1.75} />
                      </IconButton>
                    )}
                    </AlineadoConCampo>
                  </Box>
                );
              })}
            </Stack>
            {!soloLectura && (
              <Button size="small" variant="text" startIcon={<Plus size={14} aria-hidden />} onClick={() => agregarArea(i)} sx={{ mt: 0.5 }}>
                {t('flujos.editor.agregarArea')}
              </Button>
            )}
          </Box>
        );
      })}
      {error('etapas') && (
        <Typography color="error" sx={{ fontSize: 12 }}>
          {t('flujos.editor.errores.etapas-requeridas')}
        </Typography>
      )}
      {!soloLectura && (
        <div>
          <Button variant="outlined" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...etapas, { clave: nuevaClave(), areas: [areaNueva()] }])}>
            {t('flujos.editor.agregarEtapa')}
          </Button>
        </div>
      )}
    </Stack>
  );
}
