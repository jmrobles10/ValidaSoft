import { Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Stack, TextField, Typography } from '@mui/material';
import type { ExpedienteDto, MotivoTipificadoDto } from '@validasoft/api-client';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionExpediente } from '../api';
import { conDiscrepanciaCritica, justificacionSuficiente } from '../cruce';
import { documentosDelArea, falloDeDecision, requisitosParaObservar, validarObservacion, versionesParaAprobar } from '../decisionArea';
import { MAXIMO_TEXTO_EXPEDIENTE, motivosPara, type DialogoDeArea } from '../reglas';
import { SelectorCamposSeccion } from './SelectorCamposSeccion';
import { SelectorMotivos } from './SelectorMotivos';

interface Marca {
  motivos: string[];
  observacion: string;
  campos?: string[];
}
type Marcas = Map<string, Marca>;

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
  // Documentos cuyos motivos la persona ya tocó: solo a ellos se les marca el error de «sin motivos».
  const [motivosTocados, setMotivosTocados] = useState<ReadonlySet<string>>(new Set());
  const [justificacion, setJustificacion] = useState('');
  const area = dialogo.area;
  const observando = dialogo.tipo === 'observarArea';
  const documentos = documentosDelArea(expediente, area.areaId);
  const nombreDe = (requisitoId: string) => documentos.find((d) => d.requisito.id === requisitoId)?.requisito.tipoDocumento.nombre ?? '—';
  const aplica = aplicaAlExpediente || mutacion.isPending || mutacion.isSuccess;
  const errores = observando ? validarObservacion(marcas) : [];
  const sinMotivos = errores.flatMap((e) => (e.tipo === 'sin-motivos' ? [e.requisitoId] : []));
  // Aprobar documentos o secciones cuyo cruce difiere en campos críticos exige dejar escrito por qué (F5, FM-13).
  const conDiscrepancia = observando ? [] : conDiscrepanciaCritica(expediente, dialogo.documentos);
  const faltaJustificacion = conDiscrepancia.length > 0 && !justificacionSuficiente(justificacion);
  // Sin nada completo no se puede enviar: Observar pide al menos un documento y cada uno con motivo; Aprobar, algo por aprobar.
  const incompleto = observando ? errores.length > 0 : dialogo.documentos.length === 0 || faltaJustificacion;
  const pendiente = !aplica
    ? null
    : faltaJustificacion
      ? t('cruce.justificacion.falta')
      : !observando
        ? null
        : marcas.size === 0
          ? t('expedientes.decisionArea.marcaUno')
          : sinMotivos.length > 0
            ? t('expedientes.decisionArea.faltanMotivos', { count: sinMotivos.length })
            : null;
  const fallo = mutacion.isError ? falloDeDecision(mutacion.error) : null;
  const disponibles = motivosPara(motivos, 'Observacion');
  const ocupado = mutacion.isPending;

  const cambiarMarca = (requisitoId: string, cambio: Partial<Marca> | null) =>
    setMarcas((previas) => {
      const siguientes = new Map(previas);
      if (cambio === null) siguientes.delete(requisitoId);
      else siguientes.set(requisitoId, { motivos: [], observacion: '', ...previas.get(requisitoId), ...cambio });
      return siguientes;
    });

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!aplica || incompleto) return;
    const decision = observando
      ? { veredicto: 'Observado' as const, requisitos: requisitosParaObservar(marcas, expediente) }
      : {
          veredicto: 'Aprobado' as const,
          versiones: versionesParaAprobar(dialogo.documentos),
          ...(conDiscrepancia.length > 0 ? { justificacion: justificacion.trim() } : {}),
        };
    mutacion.mutate(
      { tipo: 'veredictoArea', areaId: area.areaId, decision },
      {
        onSuccess: (resultado) => {
          // Que la decisión de esta área cierre el expediente como aprobado (y emita el certificado) es el momento que lleva la animación de marca.
          if (resultado.estado === 'Aprobado' && expediente.estado !== 'Aprobado') {
            mostrar(t('expedientes.exitoExpedienteAprobado', { folio: resultado.folio }), 'success', { celebrar: true });
          } else {
            mostrar(observando ? t('expedientes.decisionArea.exitoObservar', { count: marcas.size }) : t('expedientes.decisionArea.exitoAprobar', { area: area.nombre }));
          }
          onCerrar();
        },
      },
    );
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 600, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-decision-area">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-decision-area">
          {observando ? t('expedientes.decisionArea.tituloObservar', { area: area.nombre }) : t('expedientes.decisionArea.tituloAprobar', { area: area.nombre })}
          <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
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
                {documentos.map(({ requisito }) => {
                  const marca = marcas.get(requisito.id);
                  const faltanSusMotivos = motivosTocados.has(requisito.id) && sinMotivos.includes(requisito.id);
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
                            onCambiar={(codigos) => {
                              cambiarMarca(requisito.id, { motivos: codigos });
                              setMotivosTocados((previos) => new Set(previos).add(requisito.id));
                            }}
                            error={faltanSusMotivos ? t('expedientes.veredicto.motivosRequeridos') : null}
                            deshabilitado={!aplica}
                          />
                          {requisito.seccion && (
                            <SelectorCamposSeccion
                              seccion={requisito.seccion.seccion}
                              elegidos={marca.campos ?? []}
                              onCambiar={(campos) => cambiarMarca(requisito.id, { campos })}
                              deshabilitado={!aplica}
                            />
                          )}
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
                {conDiscrepancia.length > 0 && (
                  <>
                    <Alert severity="error">{t('cruce.justificacion.ayuda', { requisitos: conDiscrepancia.map((r) => r.tipoDocumento.nombre).join(', ') })}</Alert>
                    <TextField
                      required
                      label={t('cruce.justificacion.etiqueta')}
                      value={justificacion}
                      onChange={(e) => setJustificacion(e.target.value.slice(0, MAXIMO_TEXTO_EXPEDIENTE))}
                      multiline
                      minRows={2}
                      disabled={!aplica}
                    />
                  </>
                )}
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          {pendiente && <FaltaParaEnviar>{pendiente}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {aplica ? t('comun.cancelar') : t('comun.cerrar')}
          </Button>
          <Button
            type="submit"
            variant="contained"
            color={observando ? 'warning' : 'primary'}
            disabled={ocupado || !aplica || incompleto}
            startIcon={ocupado ? <IconoCarga /> : undefined}
          >
            {observando
              ? marcas.size === 0
                ? t('expedientes.decisionArea.observar')
                : t('expedientes.decisionArea.confirmarObservar', { count: marcas.size })
              : t('expedientes.decisionArea.aprobar', { count: dialogo.documentos.length })}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
