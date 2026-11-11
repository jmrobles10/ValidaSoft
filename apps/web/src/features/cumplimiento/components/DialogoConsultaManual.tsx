import { Alert, AlertTitle, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormHelperText, FormLabel, Skeleton, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import type { ExpedienteDto, RegistrarConsultaManualDto } from '@validasoft/api-client';
import { FileText, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { CampoFecha, CampoNumero } from '../../../shared/camposTransitorios';
import { mensajeDeError } from '../../../shared/errores';
import { usePersonasDeConsulta } from '../api';
import {
  coincidenciaVacia,
  consultaManualADto,
  consultaManualVacia,
  DIAS_MAXIMOS_DE_CONSULTA,
  fechaIso,
  restarDias,
  validarConsultaManual,
  type BorradorConsultaManual,
  type ErrorConsultaManual,
} from '../consultaManual';
import { formatearPeso } from '../modelo';
import { FilaCoincidenciaManual } from './FilaCoincidenciaManual';
import { SelectorDeArchivo } from './SelectorDeArchivo';

export interface DialogoConsultaManualProps {
  expediente: ExpedienteDto;
  empresaId: string;
  ocupado: boolean;
  error: unknown;
  onGuardar: (dto: RegistrarConsultaManualDto) => void;
  onCerrar: () => void;
}

/**
 * Registra una consulta de listas que el oficial hizo por fuera: sube el PDF, dice dónde y cuándo la hizo y, si las hubo,
 * cada coincidencia con la persona de la consulta a la que corresponde. Entra como una consulta más.
 */
export function DialogoConsultaManual({ expediente, empresaId, ocupado, error, onGuardar, onCerrar }: DialogoConsultaManualProps) {
  const { t } = useTranslation();
  const [hoy] = useState(() => fechaIso(new Date()));
  const [borrador, setBorrador] = useState<BorradorConsultaManual>(() => consultaManualVacia(hoy));
  const [intentado, setIntentado] = useState(false);
  const personas = usePersonasDeConsulta(empresaId, expediente.id);
  const errores = validarConsultaManual(borrador, hoy);
  const visible = (codigo: ErrorConsultaManual) => intentado && errores.includes(codigo);
  const cambiar = (cambio: Partial<BorradorConsultaManual>) => setBorrador((b) => ({ ...b, ...cambio }));
  const adjunto = borrador.adjunto;

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 620, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-consulta-manual">
      <DialogTitle id="titulo-consulta-manual">
        {t('cumplimiento.manual.titulo')}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
          {t('cumplimiento.manual.ayuda')}
        </Typography>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          <Box>
            <FormLabel sx={{ fontSize: 13 }}>{t('cumplimiento.manual.archivo')}</FormLabel>
            <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mt: 0.5 }}>
              {adjunto && (
                <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', minWidth: 0 }}>
                  <FileText size={16} aria-hidden />
                  <Typography sx={{ fontSize: 14, overflowWrap: 'anywhere' }}>{`${adjunto.nombre} · ${formatearPeso(adjunto.tamano)}`}</Typography>
                </Stack>
              )}
              <SelectorDeArchivo
                empresaId={empresaId}
                expedienteId={expediente.id}
                proposito="ConsultaManual"
                etiqueta={adjunto ? t('cumplimiento.manual.cambiarArchivo') : t('cumplimiento.manual.elegirArchivo')}
                onSubido={(a) => cambiar({ adjunto: a })}
              />
            </Stack>
            <FormHelperText error={visible('archivo')} sx={{ mx: 0 }}>
              {visible('archivo') ? t('cumplimiento.manual.faltaArchivo') : t('cumplimiento.manual.archivoAyuda')}
            </FormHelperText>
          </Box>
          <TextField
            label={t('cumplimiento.manual.fuente')}
            value={borrador.fuente}
            onChange={(e) => cambiar({ fuente: e.target.value })}
            error={visible('fuente')}
            helperText={visible('fuente') ? t('cumplimiento.manual.faltaFuente') : t('cumplimiento.manual.fuenteAyuda')}
            slotProps={{ htmlInput: { maxLength: 100 } }}
          />
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 1.5, alignItems: 'start' }}>
            <CampoFecha
              etiqueta={t('cumplimiento.manual.fecha')}
              valor={borrador.fecha}
              onCambiar={(fecha) => cambiar({ fecha })}
              minimo={restarDias(hoy, DIAS_MAXIMOS_DE_CONSULTA)}
              maximo={hoy}
              requerido
              fullWidth
              error={visible('fecha') || visible('fecha-fuera-de-plazo')}
              ayuda={visible('fecha') ? t('cumplimiento.manual.faltaFecha') : t('cumplimiento.manual.fechaAyuda')}
            />
            <CampoNumero
              etiqueta={t('cumplimiento.manual.vigencia')}
              valor={borrador.vigenciaMeses}
              onCambiar={(vigenciaMeses) => cambiar({ vigenciaMeses })}
              maxDigitos={2}
              fullWidth
              error={visible('vigencia')}
              ayuda={t('cumplimiento.manual.vigenciaAyuda')}
            />
          </Box>
          <Box>
            <FormLabel id="hubo-coincidencias" sx={{ fontSize: 13 }}>
              {t('cumplimiento.manual.hubo')}
            </FormLabel>
            <ToggleButtonGroup
              exclusive
              size="small"
              aria-labelledby="hubo-coincidencias"
              value={borrador.hubo}
              onChange={(_, valor: boolean | null) => {
                if (valor === null) return;
                cambiar({ hubo: valor, coincidencias: valor && borrador.coincidencias.length === 0 ? [coincidenciaVacia()] : borrador.coincidencias });
              }}
              sx={{ display: 'flex', mt: 0.5 }}
            >
              <ToggleButton value={false} sx={{ flex: 1 }}>
                {t('cumplimiento.manual.noHubo')}
              </ToggleButton>
              <ToggleButton value sx={{ flex: 1 }}>
                {t('cumplimiento.manual.siHubo')}
              </ToggleButton>
            </ToggleButtonGroup>
          </Box>
          {borrador.hubo && (
            <Stack spacing={1.5}>
              {personas.isPending ? (
                <Skeleton variant="rounded" height={120} role="status" aria-busy aria-label={t('cumplimiento.manual.hubo')} />
              ) : personas.isError ? (
                <Alert severity="error">{mensajeDeError(personas.error, t)}</Alert>
              ) : (
                <>
                  <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0 }}>
                    {borrador.coincidencias.map((c, i) => (
                      <FilaCoincidenciaManual
                        key={c.clave}
                        numero={i + 1}
                        coincidencia={c}
                        personas={personas.data.personas}
                        claves={personas.data.claves}
                        conErrores={visible('coincidencias')}
                        onCambiar={(cambio) => cambiar({ coincidencias: borrador.coincidencias.map((x) => (x.clave === c.clave ? { ...x, ...cambio } : x)) })}
                        onQuitar={() => cambiar({ coincidencias: borrador.coincidencias.filter((x) => x.clave !== c.clave) })}
                      />
                    ))}
                  </Stack>
                  <Box>
                    <Button size="small" variant="outlined" startIcon={<Plus size={16} aria-hidden />} onClick={() => cambiar({ coincidencias: [...borrador.coincidencias, coincidenciaVacia()] })}>
                      {t('cumplimiento.manual.agregar')}
                    </Button>
                  </Box>
                  <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>
                    {t('cumplimiento.manual.personas', { personas: personas.data.personas.map((p) => p.nombre).join(', ') })}
                  </Typography>
                </>
              )}
              {visible('coincidencias') && <FormHelperText error>{t('cumplimiento.manual.faltaCoincidencia')}</FormHelperText>}
            </Stack>
          )}
          <Alert severity="info">
            <AlertTitle>{t('cumplimiento.estudio.avisoTitulo')}</AlertTitle>
            {borrador.hubo ? t('cumplimiento.manual.pasaraCon') : t('cumplimiento.manual.pasaraSin')}
          </Alert>
          {error !== null && error !== undefined && <Alert severity="error">{mensajeDeError(error, t)}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button
          variant="contained"
          disabled={ocupado}
          startIcon={ocupado ? <IconoCarga /> : undefined}
          onClick={() => {
            setIntentado(true);
            if (errores.length === 0) onGuardar(consultaManualADto(borrador, hoy));
          }}
        >
          {t('cumplimiento.manual.guardar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
