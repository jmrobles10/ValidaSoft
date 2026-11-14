import {
  Alert,
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  FormGroup,
  Radio,
  RadioGroup,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { CampoFecha, FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { ShieldOff } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useTiposDocumento } from '../../flujos/api';
import { nombreSeccion, SECCIONES_FICHA } from '../../terceros/ficha/secciones';
import { useEximirVinculo } from './api';
import { aPeticion, errorDeExencion, faltaAlEximir, formularioInicial, MAXIMO_JUSTIFICACION, type FormularioExencion } from './modelo';

export interface DialogoEximirProps {
  empresaId: string;
  vinculoId: string;
  /** Tercero al que se exime (título del diálogo). */
  tercero?: string | null;
  /** Tipo de documento que llega elegido (al eximir desde una fila de vencimientos). */
  documentoInicial?: string | null;
  onCerrar: () => void;
}

const ANCHO = { width: 520, maxWidth: 'calc(100% - 32px)' } as const;

/** Alterna un valor de un conjunto sin mutarlo. */
function alternar(conjunto: ReadonlySet<string>, valor: string): ReadonlySet<string> {
  const nuevo = new Set(conjunto);
  if (nuevo.has(valor)) nuevo.delete(valor);
  else nuevo.add(valor);
  return nuevo;
}

/**
 * Eximir al vínculo (EX-1, EX-5): de todo o de documentos y secciones concretos, con justificación obligatoria y fecha final opcional. Lo que el vínculo
 * tiene abierto y la exención cubre se exime en el acto (la API lo hace en la misma transacción). La API vuelve a validar todo: aquí solo se avisa lo que falta.
 */
export function DialogoEximir({ empresaId, vinculoId, tercero, documentoInicial, onCerrar }: DialogoEximirProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const permisos = usePermisos(empresaId);
  const mutacion = useEximirVinculo(empresaId, vinculoId);
  const puedeListarTipos = permisos.puede(SCOPES.configuracionLeer);
  const tipos = useTiposDocumento(puedeListarTipos);
  const [formulario, setFormulario] = useState<FormularioExencion>(() => formularioInicial(documentoInicial));
  const [justificacionTocada, setJustificacionTocada] = useState(false);

  const documentos = (tipos.data ?? []).filter((tipo) => tipo.activo || formulario.documentos.has(tipo.id));
  const pendiente = faltaAlEximir(formulario, t);
  const justificacionVacia = !formulario.justificacion.trim();
  const ocupado = mutacion.isPending;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (pendiente) return;
    mutacion.mutate(aPeticion(formulario), {
      onSuccess: () => {
        mostrar(t('exenciones.dialogo.exito'));
        onCerrar();
      },
    });
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: ANCHO } }} aria-labelledby="titulo-eximir">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-eximir">
          {t('exenciones.dialogo.titulo')}
          {tercero && (
            <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
              {tercero}
            </Typography>
          )}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="info">{t('exenciones.dialogo.ayuda')}</Alert>
            {mutacion.isError && <Alert severity="error">{errorDeExencion(mutacion.error, t)}</Alert>}

            <RadioGroup
              aria-label={t('exenciones.dialogo.alcance')}
              value={formulario.todos ? 'todo' : 'algunos'}
              onChange={(e) => setFormulario((f) => ({ ...f, todos: e.target.value === 'todo' }))}
            >
              <OpcionDeAlcance valor="algunos" titulo={t('exenciones.dialogo.algunos')} ayuda={t('exenciones.dialogo.algunosAyuda')} />
              <OpcionDeAlcance valor="todo" titulo={t('exenciones.dialogo.todo')} ayuda={t('exenciones.dialogo.todoAyuda')} />
            </RadioGroup>

            {!formulario.todos && (
              <Stack spacing={1.5}>
                <Box>
                  <Typography variant="subtitle2" component="h3" sx={{ mb: 0.5 }}>
                    {t('exenciones.dialogo.documentos')}
                  </Typography>
                  {puedeListarTipos && tipos.isPending ? (
                    <Stack spacing={0.75} role="status" aria-busy="true" aria-label={t('exenciones.dialogo.cargandoTipos')}>
                      {[0, 1, 2].map((i) => (
                        <Skeleton key={i} variant="rounded" height={22} width={i === 1 ? '55%' : '70%'} />
                      ))}
                    </Stack>
                  ) : documentos.length === 0 ? (
                    <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                      {t('exenciones.dialogo.sinTipos')}
                    </Typography>
                  ) : (
                    <FormGroup sx={{ maxHeight: 180, overflowY: 'auto', flexWrap: 'nowrap' }}>
                      {documentos.map((tipo) => (
                        <FormControlLabel
                          key={tipo.id}
                          label={tipo.nombre}
                          control={<Checkbox size="small" checked={formulario.documentos.has(tipo.id)} onChange={() => setFormulario((f) => ({ ...f, documentos: alternar(f.documentos, tipo.id) }))} />}
                        />
                      ))}
                    </FormGroup>
                  )}
                </Box>
                <Box>
                  <Typography variant="subtitle2" component="h3" sx={{ mb: 0.5 }}>
                    {t('exenciones.dialogo.secciones')}
                  </Typography>
                  <FormGroup>
                    {SECCIONES_FICHA.map((seccion) => (
                      <FormControlLabel
                        key={seccion.codigo}
                        label={nombreSeccion(seccion.codigo, t)}
                        control={<Checkbox size="small" checked={formulario.secciones.has(seccion.codigo)} onChange={() => setFormulario((f) => ({ ...f, secciones: alternar(f.secciones, seccion.codigo) }))} />}
                      />
                    ))}
                  </FormGroup>
                </Box>
              </Stack>
            )}

            <TextField
              required
              label={t('exenciones.dialogo.justificacion')}
              value={formulario.justificacion}
              onChange={(e) => setFormulario((f) => ({ ...f, justificacion: e.target.value.slice(0, MAXIMO_JUSTIFICACION) }))}
              onBlur={() => setJustificacionTocada(true)}
              multiline
              minRows={3}
              error={justificacionTocada && justificacionVacia}
              helperText={
                <Stack component="span" direction="row" sx={{ justifyContent: 'space-between' }} spacing={1}>
                  <span>{justificacionTocada && justificacionVacia ? t('acciones.requerido') : t('exenciones.dialogo.justificacionAyuda')}</span>
                  <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary', flex: 'none' }}>
                    {formulario.justificacion.length} / {MAXIMO_JUSTIFICACION}
                  </Typography>
                </Stack>
              }
            />
            <CampoFecha
              etiqueta={t('exenciones.dialogo.hasta')}
              ayuda={t('exenciones.dialogo.hastaAyuda')}
              valor={formulario.hasta}
              onCambiar={(hasta) => setFormulario((f) => ({ ...f, hasta }))}
              sx={{ maxWidth: 260 }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {pendiente && <FaltaParaEnviar>{pendiente}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={ocupado || pendiente !== null} startIcon={ocupado ? <IconoCarga /> : <ShieldOff size={16} aria-hidden />}>
            {t('exenciones.dialogo.confirmar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}

function OpcionDeAlcance({ valor, titulo, ayuda }: { valor: string; titulo: string; ayuda: string }) {
  return (
    <FormControlLabel
      value={valor}
      control={<Radio size="small" />}
      sx={{ alignItems: 'flex-start', mx: 0, py: 0.25 }}
      label={
        <Box sx={{ pt: '5px' }}>
          <Typography component="span" sx={{ display: 'block', fontWeight: 600, fontSize: 14 }}>
            {titulo}
          </Typography>
          <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 13 }}>
            {ayuda}
          </Typography>
        </Box>
      }
    />
  );
}
