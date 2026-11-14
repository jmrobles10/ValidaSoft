import { Typography } from '@mui/material';
import type { ExpedienteDto } from '@validasoft/api-client';
import { Dato, formatearFecha, formatearFechaHora, LineaDeTiempo, RejillaDatos } from '@validasoft/ui';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useSesion } from '../../../sesion/sesionStore';
import { ErrorRecuperable, EsqueletoHistorial, Tarjeta } from '../../../shared/componentes';
import { identificacionResumida } from '../../vinculos/tercero';
import { useHistorialExpediente } from '../api';
import { actoresDeHistorial, useNombresUsuarios } from '../../configuracion/usuarios/api';
import { eventoExpedienteATimeline, nombresDeAreas, nombresDeRequisitos } from '../historial';
import { OrigenDelFlujo } from './OrigenDelFlujo';

const Mono = ({ children }: { children: string }) => (
  <Typography component="span" variant="mono">
    {children}
  </Typography>
);

export function DatosExpediente({ expediente }: { expediente: ExpedienteDto }) {
  const { t } = useTranslation();
  const { flujo } = expediente;
  return (
    <Tarjeta titulo={t('expedientes.datos.titulo')}>
      <RejillaDatos columnas={2}>
        <Dato etiqueta={t('expedientes.datos.tercero')}>{expediente.tercero.nombre}</Dato>
        <Dato etiqueta={t('expedientes.datos.identificacion')}>
          <Mono>{identificacionResumida(expediente.tercero.identificacion)}</Mono>
        </Dato>
        <Dato etiqueta={t('expedientes.datos.tipoVinculo')}>{expediente.tipoVinculo.nombre}</Dato>
        <Dato etiqueta={t('expedientes.datos.tipo')}>{t(`expedientes.tipo.${expediente.tipo}`)}</Dato>
        <Dato etiqueta={t('expedientes.datos.flujo')} ancho>
          {flujo ? (
            <>
              {t('expedientes.datos.flujoValor', { nombre: flujo.nombre, version: String(flujo.version) })}{' '}
              <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
                ({t(`expedientes.datos.origenFlujo.${flujo.origen}`)})
              </Typography>
              <OrigenDelFlujo flujo={flujo} />
            </>
          ) : (
            '—'
          )}
        </Dato>
        <Dato etiqueta={t('expedientes.datos.politica')}>{t(`expedientes.datos.politicas.${expediente.politicaDevolucion}`)}</Dato>
        <Dato etiqueta={t('expedientes.datos.separacion')}>{expediente.separacionDeFunciones ? t('comun.si') : t('comun.no')}</Dato>
        <Dato etiqueta={t('expedientes.datos.abierto')}>
          <Mono>{formatearFechaHora(expediente.abiertoEn)}</Mono>
        </Dato>
        {expediente.cerradoEn ? (
          <Dato etiqueta={t('expedientes.datos.cerrado')}>
            <Mono>{formatearFechaHora(expediente.cerradoEn)}</Mono>
          </Dato>
        ) : (
          <Dato etiqueta={t('expedientes.datos.reenvios')}>{String(expediente.reenvios)}</Dato>
        )}
      </RejillaDatos>
    </Tarjeta>
  );
}

/** Última respuesta del tercero (o del integrador en su nombre) a una observación. */
export function UltimaRespuesta({ expediente }: { expediente: ExpedienteDto }) {
  const { t } = useTranslation();
  if (expediente.reenvios === 0) return null;
  return (
    <Tarjeta titulo={t('expedientes.respuesta.titulo')}>
      <Typography sx={{ whiteSpace: 'pre-wrap' }}>{expediente.ultimaRespuesta ?? t('expedientes.respuesta.sinTexto')}</Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5 }}>
        {t('expedientes.datos.reenvios')}: {String(expediente.reenvios)} · {formatearFecha(expediente.actualizadoEn)}
      </Typography>
    </Tarjeta>
  );
}

export function HistorialExpediente({ empresaId, expediente }: { empresaId: string; expediente: ExpedienteDto }) {
  const { t } = useTranslation();
  const usuarioActual = useSesion((s) => s.sesion?.usuarioId ?? null);
  const consulta = useHistorialExpediente(empresaId, expediente.id);
  const nombres = useNombresUsuarios(empresaId, useMemo(() => actoresDeHistorial(consulta.data), [consulta.data]));
  const { refetch } = consulta;
  const areas = useMemo(() => nombresDeAreas(expediente), [expediente]);
  const requisitos = useMemo(() => nombresDeRequisitos(expediente), [expediente]);
  const eventos = useMemo(
    () => [...(consulta.data ?? [])].reverse().map((e) => eventoExpedienteATimeline(e, { usuarioActual, nombres, areas, requisitos }, t)),
    [consulta.data, usuarioActual, nombres, areas, requisitos, t],
  );

  return (
    <Tarjeta titulo={t('expedientes.historial.titulo')}>
      {consulta.isPending && (
        <EsqueletoHistorial />
      )}
      {consulta.isError && <ErrorRecuperable mensaje={t('expedientes.historial.error')} onReintentar={() => void refetch()} />}
      {consulta.data && <LineaDeTiempo eventos={eventos} vacio={t('expedientes.historial.vacio')} />}
    </Tarjeta>
  );
}
