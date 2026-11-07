import { Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Stack, TextField, Typography } from '@mui/material';
import type { ExpedienteDto, MotivoTipificadoDto } from '@validasoft/api-client';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionExpediente } from '../api';
import { documentosDelArea, falloDeDecision, requisitosParaObservar, validarObservacion, versionesParaAprobar } from '../decisionArea';
import { MAXIMO_TEXTO_EXPEDIENTE, motivosPara, type DialogoDeArea } from '../reglas';
import { SelectorMotivos } from './SelectorMotivos';

type Marcas = Map<string, { motivos: string[]; observacion: string }>;

/**
 * La decisión del área en un paso (como en el SRM): «Aprobar área» aprueba los documentos que la persona vio, con su
 * versión (todo o nada); «Observar» marca los documentos con problema, cada uno con motivos y nota, y devuelve al
 * tercero una sola vez. Un 409 `version-superada` dice qué documento cambió y pide revisar de nuevo.
 */
export function DialogoDecisionArea({
  dialogo,
  expediente,
  empresaId,
  motivos,
  aplica: aplicaAlExpediente,
  onCerrar,
}: {
  dialogo: DialogoDeArea;
  expediente: ExpedienteDto;
  empresaId: string;
  motivos: readonly MotivoTipificadoDto[];
  aplica: boolean;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const mutacion = useAccionExpediente(empresaId, expediente.id);
  const [marcas, setMarcas] = useState<Marcas>(new Map());
  const [intentado, setIntentado] = useState(false);
  const area = dialogo.area;
  const observando = dialogo.tipo === 'observarArea';
  const documentos = documentosDelArea(expediente, area.areaId);
  const nombreDe = (requisitoId: string) => documentos.find((d) => d.requisito.id === requisitoId)?.requisito.tipoDocumento.nombre ?? '—';
  const aplica = aplicaAlExpediente || mutacion.isPending || mutacion.isSuccess;
  const errores = observando ? validarObservacion(marcas) : [];
  const fallo = mutacion.isError ? falloDeDecision(mutacion.error) : null;
  const disponibles = motivosPara(motivos, 'Observacion');
  const ocupado = mutacion.isPending;

  const cambiarMarca = (requisitoId: string, cambio: Partial<{ motivos: string[]; observacion: string }> | null) =>
    setMarcas((previas) => {
      const siguientes = new Map(previas);
      if (cambio === null) siguientes.delete(requisitoId);
      else siguientes.set(requisitoId, { motivos: [], observacion: '', ...previas.get(requisitoId), ...cambio });
      return siguientes;
    });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (!aplica || errores.length > 0) return;
    const decision = observando
      ? { veredicto: 'Observado' as const, requisitos: requisitosParaObservar(marcas, expediente) }
      : { veredicto: 'Aprobado' as const, versiones: versionesParaAprobar(dialogo.documentos) };
    mutacion.mutate(
      { tipo: 'veredictoArea', areaId: area.areaId, decision },
      {
        onSuccess: (resultado) => {
          mostrar(
            resultado.estado === 'Aprobado' && expediente.estado !== 'Aprobado'
              ? t('expedientes.exitoExpedienteAprobado', { folio: resultado.folio })
              : observando
                ? t('expedientes.decisionArea.exitoObservar', { count: marcas.size })
                : t('expedientes.decisionArea.exitoAprobar', { area: area.nombre }),
          );
          onCerrar();
        },
      },
    );
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 600, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-decision-area">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-decision-area">
          {observando ? t('expedientes.decisionArea.tituloObservar', { area: area.nombre }) : t('expedientes.decisionArea.tituloAprobar', { area: area.nombre })}
          <Typography component="span" variant="body2" color="text.secondary" sx={{ display: 'block', mt: 0.25, fontWeight: 400 }}>
            {expediente.folio} · {expediente.tercero.nombre}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {fallo?.tipo === 'version-superada' && (
              <Alert severity="warning" action={<Button color="inherit" size="small" onClick={onCerrar}>{t('expedientes.decisionArea.revisarDeNuevo')}</Button>}>
                {t('expedientes.decisionArea.versionSuperada', { documentos: fallo.requisitos.map(nombreDe).join(', ') || t('expedientes.decisionArea.algunDocumento') })}
              </Alert>
            )}
            {fallo && fallo.tipo !== 'version-superada' && <Alert severity="error">{mensajeDeError(mutacion.error, t)}</Alert>}
            {!aplica && !fallo && <Alert severity="warning">{t('expedientes.yaNoAplica')}</Alert>}

            {observando ? (
              <>
                <Typography>{t('expedientes.decisionArea.ayudaObservar')}</Typography>
                {intentado && errores.some((e) => e.tipo === 'sin-documentos') && <Alert severity="error">{t('expedientes.decisionArea.marcaUno')}</Alert>}
                {documentos.map(({ requisito }) => {
                  const marca = marcas.get(requisito.id);
                  const sinMotivos = intentado && errores.some((e) => e.tipo === 'sin-motivos' && e.requisitoId === requisito.id);
                  return (
                    <Box key={requisito.id} sx={{ border: 1, borderColor: marca ? 'estado.advertencia.borde' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.25 }}>
                      <FormControlLabel
                        sx={{ mx: 0 }}
                        disabled={!aplica || !requisito.versionActual}
                        control={<Checkbox checked={Boolean(marca)} onChange={(e) => cambiarMarca(requisito.id, e.target.checked ? {} : null)} />}
                        label={`${requisito.tipoDocumento.nombre}${requisito.versionActual ? ` · v${String(requisito.versionActual.n)}` : ''}`}
                      />
                      {marca && (
                        <Stack spacing={1.5} sx={{ mt: 1, pl: 1 }}>
                          <SelectorMotivos
                            etiqueta={t('expedientes.veredicto.motivos')}
                            ayuda={t('expedientes.veredicto.motivosAyuda')}
                            motivos={disponibles}
                            elegidos={marca.motivos}
                            onCambiar={(codigos) => cambiarMarca(requisito.id, { motivos: codigos })}
                            error={sinMotivos ? t('expedientes.veredicto.motivosRequeridos') : null}
                            deshabilitado={!aplica}
                          />
                          <TextField
                            size="small"
                            label={t('expedientes.decisionArea.nota')}
                            value={marca.observacion}
                            onChange={(e) => cambiarMarca(requisito.id, { observacion: e.target.value.slice(0, MAXIMO_TEXTO_EXPEDIENTE) })}
                            multiline
                            minRows={2}
                            disabled={!aplica}
                          />
                        </Stack>
                      )}
                    </Box>
                  );
                })}
              </>
            ) : (
              <>
                <Typography>{t('expedientes.decisionArea.ayudaAprobar', { count: dialogo.documentos.length })}</Typography>
                <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
                  {dialogo.documentos.map((d) => (
                    <Typography component="li" key={d.requisitoId}>
                      {nombreDe(d.requisitoId)} · {t('expedientes.requisitos.sobreVersion', { n: String(d.versionVigente) })}
                    </Typography>
                  ))}
                </Box>
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {aplica ? t('comun.cancelar') : t('comun.cerrar')}
          </Button>
          <Button type="submit" variant="contained" color={observando ? 'warning' : 'primary'} disabled={ocupado || !aplica} startIcon={ocupado ? <IconoCarga /> : undefined}>
            {observando ? t('expedientes.decisionArea.confirmarObservar', { count: marcas.size }) : t('expedientes.decisionArea.aprobar', { count: dialogo.documentos.length })}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
