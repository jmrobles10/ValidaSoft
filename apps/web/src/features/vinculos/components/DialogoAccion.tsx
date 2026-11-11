import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Radio, RadioGroup, Skeleton, Stack, TextField, Typography } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { accionPermitidaPorEstado, esErrorApi, type DefinicionAccion, type VinculoDto } from '@validasoft/api-client';
import { RotateCcw } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { IconoCarga } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { ICONO_ACCION, MAXIMO_TEXTO, PALABRA_ANULAR } from '../acciones';
import { useAccionVinculo, useTercero, useViabilidadDeReintegro } from '../api';
import { identificacionDe } from '../tercero';

export interface DialogoAccionProps {
  definicion: DefinicionAccion;
  vinculo: VinculoDto;
  empresaId: string;
  onCerrar: () => void;
}

const ANCHO = { width: 460, maxWidth: 'calc(100% - 32px)' } as const;
/** Forma mínima de un correo (la API valida el resto). */
const CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

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
  // El error de cada campo sale cuando la persona ya lo tocó; mientras falte algo, el botón de enviar está deshabilitado.
  const [textoTocado, setTextoTocado] = useState(false);
  const [confirmacionTocada, setConfirmacionTocada] = useState(false);
  const [recargando, setRecargando] = useState(false);
  // Al reintegrar (DS-3): revalidar (por omisión) o restituir sin revalidar, solo si lo aprobado sigue vigente.
  const [revalidar, setRevalidar] = useState(true);
  const viabilidad = useViabilidadDeReintegro(empresaId, vinculo.id, accion === 'reintegrar');
  const puedeSinRevalidar = viabilidad.data?.puedeSinRevalidar === true;

  const conTexto = requiere !== 'correo';
  const vacio = conTexto && !texto.trim();
  const correoInvalido = !conTexto && !CORREO.test(texto.trim());
  const faltaConfirmar = accion === 'anular' && confirmacion.trim().toUpperCase() !== PALABRA_ANULAR;
  const pendiente = vacio
    ? t('acciones.faltaCampo', { campo: t(`acciones.${accion}.campo`) })
    : correoInvalido
      ? t('acciones.correoInvalido')
      : faltaConfirmar
        ? t('acciones.anular.confirmarAyuda', { palabra: PALABRA_ANULAR })
        : null;
  const Icono = ICONO_ACCION[accion];
  const ocupado = mutacion.isPending || recargando;
  const conflicto = esErrorApi(mutacion.error) && mutacion.error.esConflictoDeVersion;
  // Solo si el cambio llegó antes de enviar: la recarga que dispara la propia acción no cuenta.
  const yaNoAplica = !mutacion.isPending && !mutacion.isSuccess && !accionPermitidaPorEstado(accion, vinculo);

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (pendiente) return;
    mutacion.mutate(
      { accion, vinculo, texto: texto.trim(), hasta: hasta ? new Date(`${hasta}T23:59:59`).toISOString() : null, vigenciaHoras: vigencia, revalidar: revalidar || !puedeSinRevalidar },
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
        <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
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
      <FormularioDeDialogo onSubmit={enviar}>
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
            {accion === 'reintegrar' && !yaNoAplica && (
              <Box>
                {viabilidad.isPending ? (
                  <Stack spacing={1} aria-busy>
                    <Skeleton variant="rounded" height={44} />
                    <Skeleton variant="rounded" height={44} />
                  </Stack>
                ) : (
                  <>
                    <RadioGroup
                      aria-label={t('acciones.reintegrar.como')}
                      value={revalidar || !puedeSinRevalidar ? 'revalidar' : 'sin'}
                      onChange={(e) => setRevalidar(e.target.value === 'revalidar')}
                    >
                      <FormControlLabel
                        value="revalidar"
                        control={<Radio size="small" />}
                        sx={{ alignItems: 'flex-start', mx: 0, py: 0.25 }}
                        label={
                          <Box sx={{ pt: '5px' }}>
                            <Typography component="span" sx={{ display: 'block', fontWeight: 600, fontSize: 14 }}>
                              {t('acciones.reintegrar.revalidar')}
                            </Typography>
                            <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 13 }}>
                              {t('acciones.reintegrar.revalidarAyuda')}
                            </Typography>
                          </Box>
                        }
                      />
                      <FormControlLabel
                        value="sin"
                        disabled={!puedeSinRevalidar}
                        control={<Radio size="small" />}
                        sx={{ alignItems: 'flex-start', mx: 0, py: 0.25 }}
                        label={
                          <Box sx={{ pt: '5px' }}>
                            <Typography component="span" sx={{ display: 'block', fontWeight: 600, fontSize: 14 }}>
                              {t('acciones.reintegrar.sinRevalidar')}
                            </Typography>
                            <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 13 }}>
                              {t('acciones.reintegrar.sinRevalidarAyuda')}
                            </Typography>
                          </Box>
                        }
                      />
                    </RadioGroup>
                    {viabilidad.isError ? (
                      <Alert severity="warning" sx={{ mt: 1 }}>
                        {t('acciones.reintegrar.sinViabilidad')}
                      </Alert>
                    ) : (
                      !puedeSinRevalidar && (
                        <Alert severity="info" sx={{ mt: 1 }}>
                          <Typography component="span" sx={{ display: 'block', fontSize: 13, fontWeight: 600 }}>
                            {t('acciones.reintegrar.noSePuede')}
                          </Typography>
                          <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
                            {viabilidad.data.motivos.map((motivo) => (
                              <li key={motivo}>{motivo}</li>
                            ))}
                          </Box>
                        </Alert>
                      )
                    )}
                  </>
                )}
              </Box>
            )}
            <TextField
              required={conTexto}
              type={conTexto ? 'text' : 'email'}
              label={t(`acciones.${accion}.campo`)}
              value={texto}
              onChange={(e) => setTexto(e.target.value.slice(0, MAXIMO_TEXTO))}
              multiline={conTexto}
              minRows={conTexto ? 3 : undefined}
              onBlur={() => setTextoTocado(true)}
              error={textoTocado && (vacio || correoInvalido)}
              helperText={
                conTexto ? (
                  <Stack component="span" direction="row" sx={{ justifyContent: 'space-between' }} spacing={1}>
                    <span>{textoTocado && vacio ? t('acciones.requerido') : t('acciones.quedaEnHistorial')}</span>
                    <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary', flex: 'none' }}>
                      {texto.length} / {MAXIMO_TEXTO}
                    </Typography>
                  </Stack>
                ) : textoTocado && correoInvalido ? (
                  t('acciones.correoInvalido')
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
                onBlur={() => setConfirmacionTocada(true)}
                error={confirmacionTocada && faltaConfirmar}
                helperText={confirmacionTocada && faltaConfirmar ? t('acciones.anular.confirmarAyuda', { palabra: PALABRA_ANULAR }) : undefined}
                slotProps={{ htmlInput: { autoComplete: 'off', spellCheck: false } }}
                sx={{ '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
                disabled={yaNoAplica}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          {!yaNoAplica && pendiente ? (
            <FaltaParaEnviar>{pendiente}</FaltaParaEnviar>
          ) : (
            conTexto && (
              <Typography variant="caption" sx={{ color: 'text.secondary', mr: 'auto', fontWeight: 400 }}>
                {t('acciones.registro')}
              </Typography>
            )
          )}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {yaNoAplica ? t('comun.cerrar') : t('comun.cancelar')}
          </Button>
          {!yaNoAplica && (
            <Button
              type="submit"
              variant="contained"
              color={destructiva ? 'error' : 'primary'}
              disabled={ocupado || pendiente !== null}
              startIcon={mutacion.isPending ? <IconoCarga /> : <Icono size={16} aria-hidden />}
            >
              {t(`acciones.${accion}.confirmar`)}
            </Button>
          )}
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
