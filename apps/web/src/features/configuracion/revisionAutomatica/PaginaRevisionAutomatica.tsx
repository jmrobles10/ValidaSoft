import { Alert, AlertTitle, Button, Stack, Typography } from '@mui/material';
import { SCOPES, type RevisionAutomaticaConfigDto } from '@validasoft/api-client';
import { EsqueletoTarjeta } from '@validasoft/ui';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, IconoCarga, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useActualizarRevisionAutomatica, useRevisionAutomatica } from './api';
import { aDto, borradorDe, efectivo, hayCambios, nivelDe, pideConfirmacion, umbralDelPlan, validar, type BorradorRevision } from './modelo';
import { TarjetaAlcance } from './TarjetaAlcance';
import { TarjetaConfianza } from './TarjetaConfianza';
import { TarjetasDeNivel } from './TarjetasDeNivel';
import { TarjetaUso } from './TarjetaUso';

/**
 * «Revisión automática» de la empresa activa (ADR-0012): el nivel (Manual, Asistido o Automático), la confianza mínima, lo que
 * el sistema aprueba solo, el uso del mes y los avisos de lo que pasa al guardar. Solo lectura sin `configuracion:escribir`.
 */
export function PaginaRevisionAutomatica() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const consulta = useRevisionAutomatica(empresa.id, puedeLeer);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.revision-automatica')} />;
  if (consulta.data) return <Contenido empresaId={empresa.id} empresa={empresa.nombre} datos={consulta.data} puedeEscribir={permisos.puede(SCOPES.configuracionEscribir)} />;

  return (
    <>
      <EncabezadoPagina titulo={t('revisionAutomatica.config.titulo')} subtitulo={t('revisionAutomatica.config.subtitulo', { empresa: empresa.nombre })} />
      {consulta.isError ? (
        <ErrorRecuperable mensaje={`${t('revisionAutomatica.config.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
      ) : (
        <Stack spacing={1.5} aria-busy>
          <EsqueletoTarjeta alto={160} />
          <EsqueletoTarjeta alto={110} />
          <EsqueletoTarjeta alto={200} />
        </Stack>
      )}
    </>
  );
}

function Contenido({ empresaId, empresa, datos, puedeEscribir }: { empresaId: string; empresa: string; datos: RevisionAutomaticaConfigDto; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useActualizarRevisionAutomatica(empresaId);
  const original = useMemo(() => borradorDe(datos), [datos]);
  const [borrador, setBorrador] = useState<BorradorRevision>(original);
  const [confirmando, setConfirmando] = useState(false);

  // Lo que se guardaría (cada nivel usa solo lo que muestra), sus errores y si hay algo distinto de lo guardado.
  const actual = efectivo(borrador, original);
  const errores = validar(actual);
  const cambios = hayCambios(original, borrador);
  const bloqueado = Object.keys(errores).length > 0;
  const cambiar = (cambio: Partial<BorradorRevision>) => setBorrador((b) => ({ ...b, ...cambio }));
  const nivelGuardado = nivelDe(datos);

  const enviar = () =>
    guardar.mutate(aDto(actual), {
      onSuccess: (respuesta) => {
        setBorrador(borradorDe(respuesta));
        mostrar(t('revisionAutomatica.config.guardado', { nivel: t(`revisionAutomatica.niveles.${nivelDe(respuesta)}`) }));
      },
      onSettled: () => setConfirmando(false),
    });
  const alGuardar = () => {
    if (pideConfirmacion(original, borrador)) setConfirmando(true);
    else enviar();
  };

  return (
    <>
      <EncabezadoPagina
        titulo={t('revisionAutomatica.config.titulo')}
        subtitulo={t('revisionAutomatica.config.subtitulo', { empresa })}
        acciones={
          puedeEscribir ? (
            <Stack direction="row" spacing={1}>
              {cambios && (
                <Button variant="text" onClick={() => setBorrador(original)} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
                  {t('revisionAutomatica.config.descartar')}
                </Button>
              )}
              <Button variant="contained" onClick={alGuardar} disabled={!cambios || bloqueado || guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
                {t('revisionAutomatica.config.guardar')}
              </Button>
            </Stack>
          ) : undefined
        }
      />
      <Stack spacing={1.5}>
        {!puedeEscribir && <Alert severity="info">{t('revisionAutomatica.config.soloLectura')}</Alert>}
        {!datos.iaDisponible && (
          <Alert
            severity="warning"
            action={
              <Button component={RouterLink} to={`/e/${empresaId}/uso-ia`} size="small" color="inherit">
                {t('revisionAutomatica.config.sinIa.enlace')}
              </Button>
            }
          >
            <AlertTitle>{t('revisionAutomatica.config.sinIa.titulo')}</AlertTitle>
            {nivelGuardado === 'Automatico' ? t('revisionAutomatica.config.sinIa.textoAutomatico') : t('revisionAutomatica.config.sinIa.texto')}
          </Alert>
        )}
        {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}

        <Tarjeta titulo={t('revisionAutomatica.config.nivel.titulo')}>
          <Stack spacing={1.5}>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('revisionAutomatica.config.nivel.ayuda')}
            </Typography>
            <TarjetasDeNivel valor={borrador.nivel} onCambiar={(nivel) => cambiar({ nivel })} automaticoDisponible={datos.iaDisponible} deshabilitado={!puedeEscribir} />
            {borrador.nivel === 'Automatico' && (
              <Alert severity="warning">
                <AlertTitle>{t('revisionAutomatica.config.avisos.automatico')}</AlertTitle>
                {t('revisionAutomatica.config.avisos.automaticoAyuda')}
              </Alert>
            )}
          </Stack>
        </Tarjeta>

        {borrador.nivel !== 'Manual' && (
          <TarjetaConfianza
            valor={borrador.umbral}
            onCambiar={(umbral) => cambiar({ umbral })}
            error={Boolean(errores.umbral)}
            umbralDelPlan={umbralDelPlan(datos)}
            deshabilitado={!puedeEscribir}
          />
        )}
        {borrador.nivel === 'Automatico' && <TarjetaAlcance borrador={borrador} onCambiar={cambiar} config={datos} errores={errores} deshabilitado={!puedeEscribir} />}
        {(nivelGuardado !== 'Manual' || datos.uso.aprobadas + datos.uso.excepciones + datos.uso.sugerencias > 0) && <TarjetaUso uso={datos.uso} />}
      </Stack>

      {confirmando && (
        <DialogoConfirmacion
          titulo={t('revisionAutomatica.config.confirmar.titulo')}
          mensaje={t('revisionAutomatica.config.confirmar.mensaje', {
            tipos: t('revisionAutomatica.config.confirmar.tipos', { count: actual.tipos.length }),
            areas: t('revisionAutomatica.config.confirmar.areas', { count: actual.areas.length }),
            umbral: actual.umbral,
            muestreo: actual.muestreo,
          })}
          confirmar={t('revisionAutomatica.config.confirmar.boton')}
          tono="primary"
          ocupado={guardar.isPending}
          onConfirmar={enviar}
          onCerrar={() => setConfirmando(false)}
        />
      )}
    </>
  );
}
