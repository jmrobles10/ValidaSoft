import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { accionPermitidaPorEstado, esErrorApi, type DefinicionAccion, type VinculoDto } from '@validasoft/api-client';
import { RotateCcw } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { ICONO_ACCION, MAXIMO_TEXTO, PALABRA_ANULAR } from '../acciones';
import { useAccionVinculo, useTercero } from '../api';
import { identificacionDe } from '../tercero';

export interface DialogoAccionProps {
  definicion: DefinicionAccion;
  vinculo: VinculoDto;
  empresaId: string;
  onCerrar: () => void;
}

const ANCHO = { width: 460, maxWidth: 'calc(100% - 32px)' } as const;

/**
 * Acción del vínculo con motivo, justificación o correo; se ejecuta con If-Match. Si alguien cambió el vínculo (412),
 * explica qué pasó y "Recargar" trae la versión vigente sin perder lo escrito.
 */
export function DialogoAccion({ definicion, vinculo, empresaId, onCerrar }: DialogoAccionProps) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const mutacion = useAccionVinculo(empresaId);
  const tercero = useTercero(vinculo.terceroId);
  const { accion, requiere, destructiva } = definicion;
  const [texto, setTexto] = useState(requiere === 'correo' ? (vinculo.invitacion?.correoDestino ?? vinculo.correoTercero) : '');
  const [hasta, setHasta] = useState('');
  const [vigencia, setVigencia] = useState(72);
  const [confirmacion, setConfirmacion] = useState('');
  const [intentado, setIntentado] = useState(false);
  const [recargando, setRecargando] = useState(false);

  const conTexto = requiere !== 'correo';
  const vacio = conTexto && !texto.trim();
  const faltaConfirmar = accion === 'anular' && confirmacion.trim().toUpperCase() !== PALABRA_ANULAR;
  const Icono = ICONO_ACCION[accion];
  const ocupado = mutacion.isPending || recargando;
  const conflicto = esErrorApi(mutacion.error) && mutacion.error.esConflictoDeVersion;
  const yaNoAplica = !accionPermitidaPorEstado(accion, vinculo);

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (vacio || faltaConfirmar) return;
    mutacion.mutate(
      { accion, vinculo, texto: texto.trim(), hasta: hasta ? new Date(`${hasta}T23:59:59`).toISOString() : null, vigenciaHoras: vigencia },
      {
        onSuccess: (resultado) => {
          mostrar(
            resultado.tipo === 'invitacion' ? t('acciones.invitar.exito', { correo: resultado.invitacion.correoDestino }) : t(`acciones.${accion}.exito`),
          );
          onCerrar();
        },
      },
    );
  };

  // La versión nueva llega por props al refrescar el detalle; lo escrito sigue en el estado del diálogo.
  const recargar = async () => {
    setRecargando(true);
    try {
      await invalidarRuta(cliente, '/api/v1/vinculos');
      mutacion.reset();
    } finally {
      setRecargando(false);
    }
  };

  const subtitulo = tercero.data ? `${tercero.data.nombre} · ${identificacionDe(tercero.data)}` : null;
  const encabezado = (titulo: string) => (
    <DialogTitle id="titulo-accion">
      {titulo}
      {subtitulo && (
        <Typography component="span" variant="body2" color="text.secondary" sx={{ display: 'block', mt: 0.25, fontWeight: 400 }}>
          {subtitulo}
        </Typography>
      )}
    </DialogTitle>
  );

  if (conflicto) {
    return (
      <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: ANCHO } }} aria-labelledby="titulo-accion">
        {encabezado(t('acciones.conflicto.titulo'))}
        <DialogContent>
          <Stack spacing={2}>
            <Typography>{t('acciones.conflicto.texto')}</Typography>
            {conTexto && texto.trim() && <Alert severity="info">{t('acciones.conflicto.conservado')}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {t('comun.cerrar')}
          </Button>
          <Button variant="contained" onClick={() => void recargar()} disabled={ocupado} startIcon={recargando ? <IconoCarga /> : <RotateCcw size={16} aria-hidden />}>
            {t('acciones.conflicto.recargar')}
          </Button>
        </DialogActions>
      </Dialog>
    );
  }

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: ANCHO } }} aria-labelledby="titulo-accion">
      <form onSubmit={enviar} noValidate>
        {encabezado(t(`acciones.${accion}.titulo`))}
        <DialogContent>
          <Stack spacing={2}>
            {yaNoAplica ? (
              <Alert severity="warning">{t('acciones.yaNoAplica')}</Alert>
            ) : destructiva ? (
              <Alert severity="warning">{t(`acciones.${accion}.ayuda`)}</Alert>
            ) : (
              <Typography>{t(`acciones.${accion}.ayuda`)}</Typography>
            )}
            {mutacion.isError && <Alert severity="error">{mensajeDeError(mutacion.error, t)}</Alert>}
            <TextField
              required={conTexto}
              type={conTexto ? 'text' : 'email'}
              label={t(`acciones.${accion}.campo`)}
              value={texto}
              onChange={(e) => setTexto(e.target.value.slice(0, MAXIMO_TEXTO))}
              multiline={conTexto}
              minRows={conTexto ? 3 : undefined}
              error={intentado && vacio}
              helperText={
                conTexto ? (
                  <Stack component="span" direction="row" justifyContent="space-between" spacing={1}>
                    <span>{intentado && vacio ? t('acciones.requerido') : t('acciones.quedaEnHistorial')}</span>
                    <Typography component="span" variant="monoTabla" color="text.secondary" sx={{ flex: 'none' }}>
                      {texto.length} / {MAXIMO_TEXTO}
                    </Typography>
                  </Stack>
                ) : undefined
              }
              disabled={yaNoAplica}
            />
            {accion === 'bloquear' && (
              <TextField type="date" label={t('acciones.bloquear.hasta')} value={hasta} onChange={(e) => setHasta(e.target.value)} disabled={yaNoAplica} />
            )}
            {accion === 'invitar' && (
              <TextField
                type="number"
                label={t('acciones.invitar.vigencia')}
                value={vigencia}
                onChange={(e) => setVigencia(Math.max(1, Number(e.target.value) || 72))}
                slotProps={{ htmlInput: { min: 1, max: 720 } }}
                disabled={yaNoAplica}
              />
            )}
            {accion === 'anular' && (
              <TextField
                required
                label={t('acciones.anular.confirmarPalabra', { palabra: PALABRA_ANULAR })}
                value={confirmacion}
                onChange={(e) => setConfirmacion(e.target.value)}
                error={intentado && faltaConfirmar}
                helperText={intentado && faltaConfirmar ? t('acciones.anular.confirmarAyuda', { palabra: PALABRA_ANULAR }) : undefined}
                slotProps={{ htmlInput: { autoComplete: 'off', spellCheck: false } }}
                sx={{ '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
                disabled={yaNoAplica}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          {conTexto && (
            <Typography variant="caption" color="text.secondary" sx={{ mr: 'auto', fontWeight: 400 }}>
              {t('acciones.registro')}
            </Typography>
          )}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {yaNoAplica ? t('comun.cerrar') : t('comun.cancelar')}
          </Button>
          {!yaNoAplica && (
            <Button
              type="submit"
              variant="contained"
              color={destructiva ? 'error' : 'primary'}
              disabled={ocupado}
              startIcon={mutacion.isPending ? <IconoCarga /> : <Icono size={16} aria-hidden />}
            >
              {t(`acciones.${accion}.confirmar`)}
            </Button>
          )}
        </DialogActions>
      </form>
    </Dialog>
  );
}
