import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Skeleton,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import type { AdjuntoDeCumplimientoDto, ConceptoCumplimiento, ExpedienteDto, RegistrarEstudioCumplimientoDto } from '@validasoft/api-client';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useMatrizDeRiesgo } from '../../configuracion/matrizRiesgo/api';
import { useCalculoDeMatriz } from '../api';
import {
  ESTUDIO_VACIO,
  estudioADto,
  evidenciasPosibles,
  factoresManuales,
  respuestasADto,
  respuestasCompletas,
  validarEstudio,
  type BorradorEstudio,
  type ContextoMatriz,
  type ErrorEstudio,
} from '../modelo';
import { ArchivosDelEstudio } from './ArchivosDelEstudio';
import { AvisoDelEstudio } from './AvisoDelEstudio';
import { EvidenciasDelEstudio } from './EvidenciasDelEstudio';
import { SeccionMatriz } from './SeccionMatriz';
import { SeccionRiesgoManual } from './SeccionRiesgoManual';

export interface DialogoEstudioProps {
  expediente: ExpedienteDto;
  empresaId: string;
  /** Vigencia del flujo; `null` = la de la configuración (30 días). */
  vigenciaDias: number | null;
  ocupado: boolean;
  error: unknown;
  onGuardar: (dto: RegistrarEstudioCumplimientoDto) => void;
  onCerrar: () => void;
}

/**
 * Estudio del oficial. Con la matriz de riesgo de la empresa activa, el nivel y la debida diligencia salen del cálculo (el
 * oficial responde los factores manuales y solo puede ajustar el nivel con una justificación); sin ella elige ambos a mano
 * (nivel 1–3 y debida diligencia simplificada, ordinaria o intensificada, esta última obligatoria con riesgo alto). Además:
 * conflicto de interés, concepto, observaciones, evidencias del expediente y archivos propios del estudio.
 */
export function DialogoEstudio({ expediente, empresaId, vigenciaDias, ocupado, error, onGuardar, onCerrar }: DialogoEstudioProps) {
  const { t } = useTranslation();
  const consultaMatriz = useMatrizDeRiesgo(empresaId);
  const matriz = consultaMatriz.data;
  const conMatriz = matriz?.activa === true;
  const factores = useMemo(() => (matriz ? factoresManuales(matriz) : []), [matriz]);
  const [borrador, setBorrador] = useState<BorradorEstudio>(ESTUDIO_VACIO);
  const [archivos, setArchivos] = useState<AdjuntoDeCumplimientoDto[]>([]);
  const [intentado, setIntentado] = useState(false);
  const completas = respuestasCompletas(factores, borrador.respuestas);
  const calculo = useCalculoDeMatriz(empresaId, expediente.id, respuestasADto(factores, borrador.respuestas), conMatriz && completas);
  const resultado = conMatriz && completas ? (calculo.data ?? null) : null;
  const contexto: ContextoMatriz | undefined = conMatriz ? { factoresManuales: factores, niveles: matriz.niveles, resultado } : undefined;
  const completo: BorradorEstudio = { ...borrador, adjuntos: archivos.map((a) => a.id) };
  const errores = validarEstudio(completo, contexto);
  const visible = (codigo: ErrorEstudio) => intentado && errores.includes(codigo);
  const posibles = evidenciasPosibles(expediente);
  const cambiar = (cambio: Partial<BorradorEstudio>) => setBorrador((b) => ({ ...b, ...cambio }));
  const listoParaMostrar = matriz !== undefined;

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 640, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-estudio">
      <DialogTitle id="titulo-estudio">
        {t('cumplimiento.estudio.titulo')}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
          {vigenciaDias ? t('cumplimiento.estudio.ayuda', { dias: String(vigenciaDias) }) : t('cumplimiento.estudio.ayudaSinDias')}
        </Typography>
      </DialogTitle>
      <DialogContent>
        {consultaMatriz.isError && (
          <Box sx={{ pt: 1 }}>
            <ErrorRecuperable mensaje={`${t('cumplimiento.matriz.error')} ${mensajeDeError(consultaMatriz.error, t)}`} onReintentar={() => void consultaMatriz.refetch()} />
          </Box>
        )}
        {consultaMatriz.isPending && (
          <Stack spacing={2} sx={{ pt: 1 }} role="status" aria-busy aria-label={t('cumplimiento.matriz.cargando')}>
            <Skeleton variant="rounded" height={32} />
            <Skeleton variant="rounded" height={56} />
            <Skeleton variant="rounded" height={96} />
          </Stack>
        )}
        {listoParaMostrar && (
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            {conMatriz ? (
              <SeccionMatriz
                matriz={matriz}
                factores={factores}
                borrador={borrador}
                cambiar={cambiar}
                resultado={resultado}
                recalculando={calculo.isFetching}
                errorDeCalculo={calculo.error}
                visible={visible}
              />
            ) : (
              <SeccionRiesgoManual borrador={borrador} cambiar={cambiar} visible={visible} />
            )}
            <FormControlLabel
              control={<Switch checked={borrador.conflictoDeInteres} onChange={(e) => cambiar({ conflictoDeInteres: e.target.checked })} />}
              label={t('cumplimiento.estudio.conflictoSi')}
              sx={{ mx: 0 }}
            />
            <TextField
              select
              label={t('cumplimiento.panel.estudio.concepto')}
              value={borrador.concepto ?? ''}
              onChange={(e) => cambiar({ concepto: e.target.value as ConceptoCumplimiento })}
              error={visible('concepto-requerido')}
              helperText={visible('concepto-requerido') ? t('cumplimiento.errores.concepto-requerido') : undefined}
            >
              {(['Favorable', 'Desfavorable'] as const).map((c) => (
                <MenuItem key={c} value={c}>
                  {t(`cumplimiento.conceptos.${c}`)}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label={t('cumplimiento.panel.estudio.observaciones')}
              value={borrador.observaciones}
              onChange={(e) => cambiar({ observaciones: e.target.value })}
              multiline
              minRows={3}
              error={visible('observaciones-requeridas')}
              helperText={visible('observaciones-requeridas') ? t('cumplimiento.errores.observaciones-requeridas') : undefined}
              slotProps={{ htmlInput: { maxLength: 4000 } }}
            />
            <EvidenciasDelEstudio posibles={posibles} elegidas={borrador.evidencias} onCambiar={(evidencias) => cambiar({ evidencias })} />
            <ArchivosDelEstudio
              empresaId={empresaId}
              expedienteId={expediente.id}
              archivos={archivos}
              onSubido={(a) => setArchivos((previos) => [...previos, a])}
              onQuitar={(id) => setArchivos((previos) => previos.filter((a) => a.id !== id))}
            />
            <AvisoDelEstudio expediente={expediente} concepto={borrador.concepto} vigenciaDias={vigenciaDias} />
            {error !== null && error !== undefined && <Alert severity="error">{mensajeDeError(error, t)}</Alert>}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button
          variant="contained"
          disabled={ocupado || !listoParaMostrar}
          startIcon={ocupado ? <IconoCarga /> : undefined}
          onClick={() => {
            setIntentado(true);
            if (errores.length === 0) onGuardar(estudioADto(completo, contexto));
          }}
        >
          {t('cumplimiento.estudio.guardar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
