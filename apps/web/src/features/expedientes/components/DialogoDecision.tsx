import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Radio, RadioGroup, Stack, TextField, Typography } from '@mui/material';
import type { ExpedienteDto, MotivoTipificadoDto, Veredicto } from '@validasoft/api-client';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { ResumenVersion } from '../../documentos/ResumenVersion';
import { useAccionExpediente, type EntradaExpediente } from '../api';
import { justificacionSuficiente, tieneDiscrepanciaCritica } from '../cruce';
import { areasQueVuelvenARevisar, MAXIMO_TEXTO_EXPEDIENTE, motivosPara, veredictosVigentes, type DialogoDeArea, type DialogoExpediente } from '../reglas';
import { documentoEnVisor, versionVigente } from '../requisitos';
import { SelectorCamposSeccion } from './SelectorCamposSeccion';
import { SelectorMotivos } from './SelectorMotivos';

export interface DialogoDecisionProps {
  dialogo: Exclude<DialogoExpediente, DialogoDeArea>;
  expediente: ExpedienteDto;
  empresaId: string;
  motivos: readonly MotivoTipificadoDto[];
  /**
   * La decisión aplica al expediente vigente. Si otra persona lo cambió antes de enviar, deja de aplicar y el diálogo
   * lo explica; el cambio que produce la propia decisión no cuenta (el diálogo se cierra con la respuesta).
   */
  aplica: boolean;
  onCerrar: () => void;
}

/**
 * Toda decisión sobre el expediente: veredicto (Aprobado u Observado con motivos), rechazo, reenvío como integrador,
 * anulación y las acciones de administrador. Ante 409/403 explica el caso con el mensaje del código y el detalle se
 * recarga detrás; lo escrito se conserva.
 */
export function DialogoDecision({ dialogo, expediente, empresaId, motivos, aplica: aplicaAlExpediente, onCerrar }: DialogoDecisionProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const mutacion = useAccionExpediente(empresaId, expediente.id);
  const [veredicto, setVeredicto] = useState<Veredicto>('Aprobado');
  const [elegidos, setElegidos] = useState<string[]>([]);
  const [texto, setTexto] = useState('');
  const [campos, setCampos] = useState<string[]>([]);
  const [justificacionCruce, setJustificacionCruce] = useState('');
  // Al desaprobar un expediente aprobado: desde qué etapa se revisa de nuevo ('' = la última, la que lo cerró).
  const [etapaDestino, setEtapaDestino] = useState('');
  // El error de cada campo sale cuando la persona ya lo tocó; mientras falte algo, el botón de enviar está deshabilitado.
  const [motivosTocados, setMotivosTocados] = useState(false);
  const [textoTocado, setTextoTocado] = useState(false);

  const aplica = aplicaAlExpediente || mutacion.isPending || mutacion.isSuccess;
  const area = 'area' in dialogo ? dialogo.area : null;
  const nombreArea = dialogo.tipo === 'veredictoRequisito' ? dialogo.nombreArea : (area?.nombre ?? '');
  const esVeredicto = dialogo.tipo === 'veredicto' || dialogo.tipo === 'veredictoRequisito';
  const observando = esVeredicto && veredicto === 'Observado';
  const conMotivos = observando || dialogo.tipo === 'rechazar';
  const textoOpcional = dialogo.tipo === 'reenviar' || esVeredicto;
  const requisito = 'requisito' in dialogo ? dialogo.requisito : null;
  const documento = requisito?.tipoDocumento.nombre ?? '';
  const versionRevisada = requisito?.versionActual?.n ?? null;
  // Una sección (F2) no tiene archivo ni datos declarados: sus datos están en la tarjeta; al observarla se señalan campos.
  const seccion = requisito?.seccion ?? null;
  const version = requisito && !seccion ? documentoEnVisor(requisito) : null;
  // Si llegó otra versión del documento, el aviso dice cuál (en vez del genérico «el expediente cambió»).
  const versionNueva = dialogo.tipo === 'veredictoRequisito' && !aplica ? versionVigente(expediente, dialogo.requisito.id) : null;
  const faltanMotivos = conMotivos && elegidos.length === 0;
  const faltaTexto = !textoOpcional && !texto.trim();
  // Aprobar un documento o una sección cuyo cruce difiere en campos críticos exige dejar escrito por qué (F5, FM-13).
  const pideJustificacion = dialogo.tipo === 'veredictoRequisito' && !observando && requisito !== null && tieneDiscrepanciaCritica(requisito);
  const faltaJustificacion = pideJustificacion && !justificacionSuficiente(justificacionCruce);
  const disponibles = motivosPara(motivos, dialogo.tipo === 'rechazar' ? 'Rechazo' : 'Observacion');
  const incompleto = faltanMotivos || faltaTexto || faltaJustificacion;
  const ocupado = mutacion.isPending;

  const textos = (() => {
    switch (dialogo.tipo) {
      case 'veredicto':
        return {
          titulo: t('expedientes.veredicto.titulo', { area: nombreArea }),
          ayuda: null,
          campo: t('expedientes.veredicto.observacion'),
          confirmar: observando ? t('expedientes.veredicto.confirmarObservar') : t('expedientes.veredicto.confirmarAprobar'),
          exito: observando ? t('expedientes.veredicto.exitoObservar') : t('expedientes.veredicto.exitoAprobar', { area: nombreArea }),
        };
      case 'veredictoRequisito':
        return {
          titulo: t('expedientes.requisitos.veredicto.titulo', { documento, area: nombreArea }),
          ayuda: t('expedientes.requisitos.veredicto.ayuda', { n: String(versionRevisada ?? '—') }),
          campo: t('expedientes.veredicto.observacion'),
          confirmar: observando ? t('expedientes.veredicto.confirmarObservar') : t('expedientes.veredicto.confirmarAprobar'),
          exito: observando
            ? t('expedientes.requisitos.veredicto.exitoObservar', { documento })
            : t('expedientes.requisitos.veredicto.exitoAprobar', { documento, area: nombreArea }),
        };
      case 'eximir':
        return {
          titulo: t('expedientes.requisitos.eximir.titulo', { documento }),
          ayuda: t('expedientes.requisitos.eximir.ayuda'),
          campo: t('expedientes.requisitos.eximir.justificacion'),
          confirmar: t('expedientes.requisitos.eximir.confirmar'),
          exito: t('expedientes.requisitos.eximir.exito', { documento }),
        };
      case 'rechazar':
        return { titulo: t('expedientes.rechazo.titulo'), ayuda: t('expedientes.rechazo.ayuda'), campo: t('expedientes.rechazo.justificacion'), confirmar: t('expedientes.rechazo.confirmar'), exito: t('expedientes.rechazo.exito') };
      case 'aprobarComoAdministrador':
        return {
          titulo: t('expedientes.aprobacionAdmin.titulo', { area: nombreArea }),
          ayuda: t('expedientes.aprobacionAdmin.ayuda'),
          campo: t('expedientes.aprobacionAdmin.justificacion'),
          confirmar: t('expedientes.aprobacionAdmin.confirmar'),
          exito: t('expedientes.aprobacionAdmin.exito', { area: nombreArea }),
        };
      case 'anularVeredicto': {
        const ultimo = area ? veredictosVigentes(area).at(-1) : undefined;
        return {
          titulo: t('expedientes.anulacionVeredicto.titulo', { area: nombreArea }),
          ayuda: t('expedientes.anulacionVeredicto.ayuda', { veredicto: ultimo ? t(`expedientes.veredictos.${ultimo.veredicto}`).toLowerCase() : '—' }),
          campo: t('expedientes.anulacionVeredicto.justificacion'),
          confirmar: t('expedientes.anulacionVeredicto.confirmar'),
          exito: t('expedientes.anulacionVeredicto.exito'),
        };
      }
      case 'reenviar':
        return {
          titulo: t('expedientes.reenvio.titulo'),
          ayuda: t('expedientes.reenvio.ayuda', { areas: areasQueVuelvenARevisar(expediente).map((a) => a.nombre).join(', ') || '—' }),
          campo: t('expedientes.reenvio.respuesta'),
          confirmar: t('expedientes.reenvio.confirmar'),
          exito: t('expedientes.reenvio.exito'),
        };
      case 'anular':
        return { titulo: t('expedientes.anulacion.titulo'), ayuda: t('expedientes.anulacion.ayuda'), campo: t('expedientes.anulacion.motivo'), confirmar: t('expedientes.anulacion.confirmar'), exito: t('expedientes.anulacion.exito') };
      case 'reabrirEtapa':
        return {
          titulo: t('expedientes.reapertura.titulo', { orden: String(dialogo.etapa.orden) }),
          ayuda: t('expedientes.reapertura.ayuda', { orden: String(dialogo.etapa.orden) }),
          campo: t('expedientes.reapertura.justificacion'),
          confirmar: t('expedientes.reapertura.confirmar'),
          exito: t('expedientes.reapertura.exito', { orden: String(dialogo.etapa.orden) }),
        };
      case 'faseInicial':
        return {
          titulo: t('expedientes.faseInicial.titulo'),
          ayuda: expediente.estado === 'Aprobado' ? t('expedientes.faseInicial.ayudaAprobado') : t('expedientes.faseInicial.ayuda'),
          campo: t('expedientes.faseInicial.justificacion'),
          confirmar: t('expedientes.faseInicial.confirmar'),
          exito: t('expedientes.faseInicial.exito'),
        };
      case 'desaprobar':
        return {
          titulo: t('expedientes.desaprobacion.titulo'),
          ayuda: t('expedientes.desaprobacion.ayuda'),
          campo: t('expedientes.desaprobacion.justificacion'),
          confirmar: t('expedientes.desaprobacion.confirmar'),
          exito: t('expedientes.desaprobacion.exito'),
        };
    }
  })();

  const entrada = (): EntradaExpediente => {
    switch (dialogo.tipo) {
      case 'veredicto':
        return { tipo: 'veredicto', areaId: dialogo.area.areaId, veredicto, motivos: elegidos, observacion: observando ? texto : '' };
      case 'veredictoRequisito':
        return {
          tipo: 'veredictoRequisito',
          requisitoId: dialogo.requisito.id,
          areaId: dialogo.areaRequisito.areaId,
          veredicto,
          motivos: elegidos,
          observacion: observando ? texto : '',
          sobreVersion: versionRevisada ?? 0,
          campos: observando && seccion ? campos : [],
          ...(pideJustificacion ? { justificacion: justificacionCruce } : {}),
        };
      case 'eximir':
        return { tipo: 'eximir', requisitoId: dialogo.requisito.id, justificacion: texto };
      case 'rechazar':
        return { tipo: 'rechazar', areaId: dialogo.area.areaId, motivos: elegidos, justificacion: texto };
      case 'aprobarComoAdministrador':
      case 'anularVeredicto':
        return { tipo: dialogo.tipo, areaId: dialogo.area.areaId, justificacion: texto };
      case 'reenviar':
        return { tipo: 'reenviar', respuesta: texto };
      case 'anular':
        return { tipo: 'anular', motivo: texto };
      case 'reabrirEtapa':
        return { tipo: 'reabrirEtapa', orden: dialogo.etapa.orden, justificacion: texto };
      case 'faseInicial':
        return { tipo: 'faseInicial', justificacion: texto };
      case 'desaprobar':
        return { tipo: 'desaprobar', justificacion: texto, ...(etapaDestino ? { etapa: Number(etapaDestino) } : {}) };
    }
  };

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (incompleto || !aplica) return;
    mutacion.mutate(entrada(), {
      onSuccess: (resultado) => {
        mostrar(resultado.estado === 'Aprobado' && expediente.estado !== 'Aprobado' ? t('expedientes.exitoExpedienteAprobado', { folio: resultado.folio }) : textos.exito);
        onCerrar();
      },
    });
  };

  const destructiva =
    dialogo.tipo === 'rechazar' || dialogo.tipo === 'anular' || dialogo.tipo === 'anularVeredicto' || dialogo.tipo === 'desaprobar' || dialogo.tipo === 'faseInicial';
  const mostrarCampo = !esVeredicto || observando;
  const pendiente = !aplica
    ? null
    : faltanMotivos
      ? t('expedientes.veredicto.motivosRequeridos')
      : faltaTexto
        ? t('expedientes.faltaCampo', { campo: textos.campo })
        : faltaJustificacion
          ? t('cruce.justificacion.falta')
          : null;

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 520, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-decision">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-decision">
          {textos.titulo}
          <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
            {expediente.folio} · {expediente.tercero.nombre}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {!aplica && (
              <Alert severity="warning">
                {versionNueva !== null && versionNueva !== versionRevisada
                  ? t('expedientes.requisitos.versionCambio', { documento, n: String(versionNueva) })
                  : t('expedientes.yaNoAplica')}
              </Alert>
            )}
            {textos.ayuda && (destructiva ? <Alert severity="warning">{textos.ayuda}</Alert> : <Typography>{textos.ayuda}</Typography>)}
            {/* Lo que se decide de la versión además del archivo (y lo único, si es un formulario): vigencia y datos. */}
            {dialogo.tipo === 'veredictoRequisito' && version && <ResumenVersion datos={version.datosDeclarados ?? []} vigencia={version.vigencia} conTitulo />}
            {mutacion.isError && <Alert severity="error">{mensajeDeError(mutacion.error, t)}</Alert>}

            {esVeredicto && (
              <RadioGroup aria-label={t('expedientes.veredicto.decision')} value={veredicto} onChange={(e) => setVeredicto(e.target.value as Veredicto)}>
                {(['Aprobado', 'Observado'] as const).map((opcion) => (
                  <FormControlLabel
                    key={opcion}
                    value={opcion}
                    disabled={!aplica}
                    control={<Radio size="small" />}
                    sx={{ alignItems: 'flex-start', mx: 0, py: 0.5 }}
                    label={
                      <Box sx={{ pt: '5px' }}>
                        <Typography component="span" sx={{ display: 'block', fontWeight: 600, fontSize: 14 }}>
                          {opcion === 'Aprobado' ? t('expedientes.veredicto.aprobar') : t('expedientes.veredicto.observar')}
                        </Typography>
                        <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 13 }}>
                          {opcion === 'Aprobado'
                            ? t('expedientes.veredicto.aprobarAyuda')
                            : expediente.politicaDevolucion === 'AlCerrarArea'
                              ? t('expedientes.veredicto.observarAyudaAlCerrar')
                              : t('expedientes.veredicto.observarAyudaInmediata')}
                        </Typography>
                      </Box>
                    }
                  />
                ))}
              </RadioGroup>
            )}

            {conMotivos &&
              (disponibles.length === 0 ? (
                <Alert severity="warning">{t('expedientes.veredicto.sinMotivos')}</Alert>
              ) : (
                <SelectorMotivos
                  etiqueta={t('expedientes.veredicto.motivos')}
                  ayuda={t('expedientes.veredicto.motivosAyuda')}
                  motivos={disponibles}
                  elegidos={elegidos}
                  onCambiar={(codigos) => {
                    setElegidos(codigos);
                    setMotivosTocados(true);
                  }}
                  error={motivosTocados && faltanMotivos ? t('expedientes.veredicto.motivosRequeridos') : null}
                  deshabilitado={!aplica}
                />
              ))}

            {observando && seccion && <SelectorCamposSeccion seccion={seccion.seccion} elegidos={campos} onCambiar={setCampos} deshabilitado={!aplica} />}

            {pideJustificacion && (
              <>
                <Alert severity="error">{t('cruce.justificacion.ayuda', { requisitos: documento })}</Alert>
                <TextField
                  required
                  label={t('cruce.justificacion.etiqueta')}
                  value={justificacionCruce}
                  onChange={(e) => setJustificacionCruce(e.target.value.slice(0, MAXIMO_TEXTO_EXPEDIENTE))}
                  multiline
                  minRows={2}
                  disabled={!aplica}
                />
              </>
            )}

            {dialogo.tipo === 'desaprobar' && expediente.etapas.length > 1 && (
              <TextField
                select
                label={t('expedientes.desaprobacion.desde')}
                helperText={t('expedientes.desaprobacion.desdeAyuda')}
                value={etapaDestino}
                onChange={(e) => setEtapaDestino(e.target.value)}
                disabled={!aplica}
                slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
              >
                <MenuItem value="">{t('expedientes.desaprobacion.ultimaEtapa')}</MenuItem>
                {[...expediente.etapas]
                  .sort((a, b) => a.orden - b.orden)
                  .slice(0, -1)
                  .map((etapa) => (
                    <MenuItem key={etapa.orden} value={String(etapa.orden)}>
                      {etapa.orden === 1 ? t('expedientes.desaprobacion.primeraEtapa') : t('expedientes.desaprobacion.desdeEtapa', { orden: String(etapa.orden) })}
                    </MenuItem>
                  ))}
              </TextField>
            )}

            {mostrarCampo && (
              <TextField
                required={!textoOpcional}
                label={textos.campo}
                value={texto}
                onChange={(e) => setTexto(e.target.value.slice(0, MAXIMO_TEXTO_EXPEDIENTE))}
                multiline
                minRows={3}
                disabled={!aplica}
                onBlur={() => setTextoTocado(true)}
                error={textoTocado && faltaTexto}
                helperText={textoTocado && faltaTexto ? t('expedientes.textoRequerido') : undefined}
              />
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
            color={destructiva ? 'error' : 'primary'}
            disabled={ocupado || !aplica || incompleto}
            startIcon={ocupado ? <IconoCarga /> : undefined}
          >
            {textos.confirmar}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
