import { Alert, Box, Button, Skeleton, Stack } from '@mui/material';
import { esErrorApi, SCOPES, type FlujoDto } from '@validasoft/api-client';
import { EsqueletoTarjeta, PaginaEstado } from '@validasoft/ui';
import { Pencil, Rocket, SearchX, Star } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import { DialogoConfirmacion, EncabezadoPagina, EnlaceVolver, ErrorRecuperable, IconoCarga, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useAreas } from '../../configuracion/areas/api';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useTiposVinculo } from '../../vinculos/api';
import { useCrearFlujo, useTiposDocumento, useDefinirPorDefecto, useEditarFlujo, useFlujo, usePublicarFlujo, useVersionesFlujo } from '../api';
import { FormularioFlujo } from '../components/FormularioFlujo';
import { VersionesFlujo } from '../components/VersionesFlujo';
import { errorDeApiEnRequisitos } from '../requisitosDeSeccion';
import {
  borradorDesdeVersion,
  borradorVacio,
  erroresBloqueantes,
  etapasADto,
  firmaBorrador,
  normalizarCodigo,
  plazoADto,
  requisitosADto,
  validarFlujo,
  verificacionesADto,
  type BorradorFlujo,
} from '../editor';
import { vigenciaADto } from '../vigencia';

/**
 * Crear (`flujos/nuevo`) o editar un flujo. Con borrador, se edita el borrador; con solo versión publicada, "Editar"
 * prepara el borrador n+1 (FL-2). Las versiones anteriores se ven en solo lectura.
 */
export function EditorFlujo() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { flujoId = '' } = useParams();
  const nuevo = flujoId === 'nuevo';
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeEscribir = permisos.puede(SCOPES.flujosEscribir);
  const mostrar = useAvisos((s) => s.mostrar);
  const flujo = useFlujo(empresa.id, nuevo ? '' : flujoId);
  const versiones = useVersionesFlujo(empresa.id, nuevo ? '' : flujoId);
  const areas = useAreas(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const tipos = useTiposVinculo(empresa.id, nuevo && permisos.puede(SCOPES.configuracionLeer));
  const tiposDocumento = useTiposDocumento(permisos.puede(SCOPES.configuracionLeer));
  const crear = useCrearFlujo(empresa.id);
  const editar = useEditarFlujo(empresa.id, flujoId);
  const publicar = usePublicarFlujo(empresa.id, flujoId);
  const porDefecto = useDefinirPorDefecto(empresa.id);
  const [borrador, setBorrador] = useState<BorradorFlujo | null>(() => (nuevo ? borradorVacio() : null));
  const [verVersion, setVerVersion] = useState<number | null>(null);
  const [intentado, setIntentado] = useState(false);
  const [confirmarPublicar, setConfirmarPublicar] = useState(false);
  // Lo que se envió la última vez: un 422 de un requisito se muestra en su campo mientras el borrador siga igual.
  const [firmaEnviada, setFirmaEnviada] = useState<string | null>(null);

  const datos = flujo.data;
  const idBorradorServidor = datos?.borrador?.id;
  // Al cargar (o tras guardar por primera vez) el borrador del servidor pasa al editor.
  useEffect(() => {
    if (datos?.borrador && borrador === null) setBorrador(borradorDesdeVersion(base(datos), datos.borrador));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- solo cuando cambia el borrador del servidor
  }, [idBorradorServidor]);

  if (!permisos.puede(SCOPES.flujosLeer)) return <SinPermiso seccion={t('nav.flujos')} />;
  const volver = <EnlaceVolver a={`/e/${empresa.id}/flujos`}>{t('nav.flujos')}</EnlaceVolver>;

  if (!nuevo && flujo.isPending) {
    return (
      <Box aria-busy>
        {volver}
        <Skeleton variant="rounded" width={320} height={28} sx={{ mb: 3 }} />
        <EsqueletoTarjeta alto={360} />
      </Box>
    );
  }
  if (!nuevo && flujo.isError) {
    if (esErrorApi(flujo.error) && flujo.error.esNoEncontrado) return <PaginaEstado icono={SearchX} titulo={t('flujos.noEncontrado')} texto={t('flujos.noEncontradoAyuda')} acciones={volver} />;
    return (
      <>
        {volver}
        <ErrorRecuperable mensaje={mensajeDeError(flujo.error, t)} onReintentar={() => void flujo.refetch()} />
      </>
    );
  }

  const listaAreas = areas.data ?? [];
  const publicada = datos?.publicada ?? null;
  const versionVista = verVersion !== null ? versiones.data?.find((v) => v.version === verVersion) : undefined;
  const mostrado: BorradorFlujo | null =
    versionVista && datos ? borradorDesdeVersion(base(datos), versionVista) : (borrador ?? (datos?.publicada ? borradorDesdeVersion(base(datos), datos.publicada) : null));
  const editando = !versionVista && borrador !== null && puedeEscribir;
  const errores = borrador ? validarFlujo(borrador, listaAreas, nuevo, tiposDocumento.data ?? []) : [];
  const bloqueantes = erroresBloqueantes(errores);
  const errorMutacion = crear.error ?? editar.error ?? publicar.error;
  const errorEnRequisito = borrador ? errorDeApiEnRequisitos(errorMutacion, borrador.requisitos) : null;
  const errorEnCampo = errorEnRequisito && borrador && firmaEnviada === firmaBorrador(borrador) ? [errorEnRequisito] : [];
  const visibles = editando ? [...(intentado ? errores : errores.filter((e) => e.aviso)), ...errorEnCampo] : [];
  const sinGuardar = Boolean(borrador && (nuevo || !datos?.borrador || firmaBorrador(borrador) !== firmaBorrador(borradorDesdeVersion(base(datos), datos.borrador))));
  const ocupado = crear.isPending || editar.isPending || publicar.isPending;

  const guardar = () => {
    setIntentado(true);
    if (!borrador || bloqueantes.length > 0) return;
    setFirmaEnviada(firmaBorrador(borrador));
    const comun = {
      nombre: borrador.nombre.trim(),
      etapas: etapasADto(borrador.etapas),
      politicaDevolucion: borrador.politicaDevolucion,
      separacionDeFunciones: borrador.separacionDeFunciones,
      plazoCargue: plazoADto(borrador),
      vigenciaVinculo: vigenciaADto(borrador),
      requisitos: requisitosADto(borrador.requisitos),
      verificaciones: verificacionesADto(borrador.verificaciones),
    };
    if (nuevo) {
      crear.mutate(
        { ...comun, codigo: normalizarCodigo(borrador.codigo), tipoVinculo: borrador.tipoVinculo },
        {
          onSuccess: (creado) => {
            mostrar(t('flujos.editor.creado'));
            setIntentado(false);
            void navegar(`/e/${empresa.id}/flujos/${creado.id}`, { replace: true });
          },
        },
      );
    } else {
      editar.mutate(comun, { onSuccess: () => mostrar(t('flujos.editor.guardado')) });
    }
  };

  const confirmarPublicacion = () => {
    if (borrador) setFirmaEnviada(firmaBorrador(borrador));
    publicar.mutate(undefined, {
      onSuccess: (f) => {
        mostrar(t('flujos.editor.publicado', { version: String(f.publicada?.version ?? '') }));
        setConfirmarPublicar(false);
        setBorrador(null);
        setIntentado(false);
      },
      onError: () => setConfirmarPublicar(false),
    });
  };

  const titulo = nuevo ? t('flujos.editor.nuevoTitulo') : (datos?.nombre ?? '');

  return (
    <>
      <EncabezadoPagina
        antes={volver}
        titulo={titulo}
        tituloCompleto={titulo}
        subtitulo={datos ? `${datos.codigo} · ${datos.tipoVinculo.nombre}` : t('flujos.ayuda')}
        acciones={
          puedeEscribir && !versionVista ? (
            <>
              {datos?.publicada && !datos.esPorDefecto && (
                <Button
                  variant="outlined"
                  startIcon={<Star size={16} aria-hidden />}
                  disabled={porDefecto.isPending}
                  onClick={() =>
                    porDefecto.mutate(
                      { tipoVinculoId: datos.tipoVinculo.id, flujoId: datos.id },
                      { onSuccess: () => mostrar(t('flujos.porDefectoGuardado', { tipo: datos.tipoVinculo.nombre })), onError: (e) => mostrar(mensajeDeError(e, t), 'error') },
                    )
                  }
                >
                  {t('flujos.editor.marcarPorDefecto', { tipo: datos.tipoVinculo.nombre })}
                </Button>
              )}
              {!borrador && datos && publicada && (
                <Button variant="contained" startIcon={<Pencil size={16} aria-hidden />} onClick={() => setBorrador(borradorDesdeVersion(base(datos), publicada))}>
                  {t('flujos.editor.editar')}
                </Button>
              )}
              {borrador && sinGuardar && !nuevo && datos?.borrador && (
                <Button variant="text" onClick={() => setBorrador(datos.borrador ? borradorDesdeVersion(base(datos), datos.borrador) : null)} disabled={ocupado} sx={{ color: 'text.primary' }}>
                  {t('flujos.editor.descartar')}
                </Button>
              )}
              {borrador && (sinGuardar || nuevo) && (
                <Button variant={datos?.borrador ? 'outlined' : 'contained'} onClick={guardar} disabled={ocupado} startIcon={crear.isPending || editar.isPending ? <IconoCarga /> : undefined}>
                  {nuevo ? t('flujos.editor.crear') : t('flujos.editor.guardar')}
                </Button>
              )}
              {datos?.borrador && !sinGuardar && (
                <Button variant="contained" startIcon={<Rocket size={16} aria-hidden />} onClick={() => setConfirmarPublicar(true)} disabled={ocupado || bloqueantes.length > 0}>
                  {t('flujos.editor.publicar', { version: String(datos.borrador.version) })}
                </Button>
              )}
            </>
          ) : undefined
        }
      />

      {errorMutacion && !errorEnRequisito && <Alert severity="error" sx={{ mb: 2 }}>{mensajeDeError(errorMutacion, t)}</Alert>}
      {intentado && bloqueantes.length > 0 && <Alert severity="warning" sx={{ mb: 2 }}>{t('flujos.editor.revisa')}</Alert>}
      {areas.isSuccess && listaAreas.every((a) => !a.activa) && <Alert severity="warning" sx={{ mb: 2 }}>{t('flujos.editor.sinAreas')}</Alert>}
      {!borrador && datos?.publicada && !versionVista && puedeEscribir && (
        <Alert severity="info" sx={{ mb: 2 }}>{t('flujos.editor.editarAyuda', { version: String(datos.publicada.version + 1) })}</Alert>
      )}
      {versionVista && (
        <Alert severity="info" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={() => setVerVersion(null)}>{t('flujos.editor.verActual')}</Button>}>
          {t('flujos.editor.soloLectura', { version: String(versionVista.version), estado: t(`flujos.editor.estadosVersion.${versionVista.estado}`).toLowerCase() })}
        </Alert>
      )}

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', xl: 'minmax(0,1fr) 280px' }, gap: 2, alignItems: 'start' }}>
        {mostrado && (
          <FormularioFlujo
            borrador={mostrado}
            onCambiar={(b) => setBorrador(b)}
            areas={listaAreas}
            tipos={nuevo ? (tipos.data ?? []) : null}
            tiposDocumento={tiposDocumento.data ?? []}
            errores={visibles}
            soloLectura={!editando}
          />
        )}
        {!nuevo && <Stack spacing={2}><VersionesFlujo versiones={versiones.data} actual={verVersion} onVer={setVerVersion} /></Stack>}
      </Box>

      {confirmarPublicar && datos?.borrador && (
        <DialogoConfirmacion
          titulo={t('flujos.editor.publicarTitulo', { version: String(datos.borrador.version), nombre: datos.nombre })}
          mensaje={t('flujos.editor.publicarAyuda')}
          confirmar={t('flujos.editor.publicar', { version: String(datos.borrador.version) })}
          icono={<Rocket size={16} aria-hidden />}
          tono="primary"
          ocupado={publicar.isPending}
          onConfirmar={confirmarPublicacion}
          onCerrar={() => setConfirmarPublicar(false)}
        />
      )}
    </>
  );
}

function base(flujo: FlujoDto) {
  return { codigo: flujo.codigo, nombre: flujo.nombre, tipoVinculo: flujo.tipoVinculo.codigo };
}
