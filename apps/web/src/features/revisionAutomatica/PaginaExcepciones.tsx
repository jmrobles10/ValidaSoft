import { Alert, Box, Button, Tab, Tabs } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { BarraFiltros, EsqueletoTarjetas, EstadoVacio, FiltroMenu } from '@validasoft/ui';
import { CircleCheck, ShieldCheck } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { useSesion } from '../../sesion/sesionStore';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePermisos } from '../../shared/usePermisos';
import { useAreas } from '../configuracion/areas/api';
import { useNombresUsuarios } from '../configuracion/usuarios/api';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useExcepciones } from './api';
import { FilaExcepcion } from './components/FilaExcepcion';
import { ListaExcepciones } from './components/ListaExcepciones';
import { filtrarPorTipo, MAXIMO_FILAS, tipoDeUrl, tipoParaUrl, type EstadoDeExcepciones, type TipoDeExcepcion } from './modelo';

const TIPOS: readonly TipoDeExcepcion[] = ['Excepcion', 'Muestreo'];

/**
 * Bandeja de excepciones de la revisión automática (ADR-0012): lo que el sistema no pudo aprobar solo (una persona lo decide en
 * el expediente) y lo que aprobó que una persona debe confirmar por muestreo. «Pendientes» y «Resueltas»; el estado y el
 * filtro por tipo van en la URL.
 */
export function PaginaExcepciones() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.expedientesLeer);
  const puedeRevisar = permisos.puede(SCOPES.expedientesRevisar);
  const usuarioId = useSesion((s) => s.sesion?.usuarioId ?? null);
  const [parametros, setParametros] = useSearchParams();
  const estado: EstadoDeExcepciones = parametros.get('estado') === 'resueltas' ? 'Resueltas' : 'Pendientes';
  const tipo = tipoDeUrl(parametros.get('tipo'));

  const pendientes = useExcepciones(empresa.id, 'Pendientes', puedeLeer);
  const resueltas = useExcepciones(empresa.id, 'Resueltas', puedeLeer && estado === 'Resueltas');
  const consulta = estado === 'Pendientes' ? pendientes : resueltas;
  const areas = useAreas(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const personas = useMemo(() => (resueltas.data ?? []).flatMap((f) => (f.resueltaPor ? [f.resueltaPor] : [])), [resueltas.data]);
  const nombres = useNombresUsuarios(empresa.id, personas);
  const filas = useMemo(() => filtrarPorTipo(consulta.data ?? [], tipo), [consulta.data, tipo]);
  // Cuánto lleva esperando cada fila se cuenta desde el momento en que llegaron los datos.
  // eslint-disable-next-line react-hooks/exhaustive-deps -- `dataUpdatedAt` es lo que reinicia el reloj.
  const ahora = useMemo(() => Date.now(), [consulta.dataUpdatedAt]);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.excepciones')} />;

  const cambiar = (clave: 'estado' | 'tipo', valor: string | null) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );
  const miembro = (areaId: string): boolean | null => {
    const area = areas.data?.find((a) => a.id === areaId);
    return area && usuarioId ? area.activa && area.miembros.includes(usuarioId) : null;
  };
  const nombreDeArea = (areaId: string, codigo: string) => areas.data?.find((a) => a.id === areaId)?.nombre ?? codigo;
  const cuantasPendientes = pendientes.data?.length;
  const hayDatos = (consulta.data?.length ?? 0) > 0;
  const nombreDelEstado = t(estado === 'Pendientes' ? 'revisionAutomatica.excepciones.pendientes' : 'revisionAutomatica.excepciones.resueltas');

  return (
    <>
      <EncabezadoPagina titulo={t('revisionAutomatica.excepciones.titulo')} subtitulo={t('revisionAutomatica.excepciones.subtitulo')} />

      <Tabs
        value={estado}
        onChange={(_, valor: EstadoDeExcepciones) => cambiar('estado', valor === 'Resueltas' ? 'resueltas' : null)}
        aria-label={t('revisionAutomatica.excepciones.pestanas')}
        sx={{ mb: 1.5, borderBottom: 1, borderColor: 'divider' }}
      >
        <Tab
          value="Pendientes"
          id="pestana-excepciones-pendientes"
          aria-controls="panel-excepciones"
          label={
            cuantasPendientes === undefined
              ? t('revisionAutomatica.excepciones.pendientes')
              : t('nav.conConteo', { nombre: t('revisionAutomatica.excepciones.pendientes'), conteo: String(cuantasPendientes) })
          }
        />
        <Tab value="Resueltas" id="pestana-excepciones-resueltas" aria-controls="panel-excepciones" label={t('revisionAutomatica.excepciones.resueltas')} />
      </Tabs>

      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('revisionAutomatica.excepciones.filtros.tipo')}
          textoTodos={t('revisionAutomatica.excepciones.filtros.todos')}
          valor={tipo ? tipoParaUrl(tipo) : ''}
          onCambiar={(valor) => cambiar('tipo', valor || null)}
          opciones={TIPOS.map((x) => ({ valor: tipoParaUrl(x), etiqueta: t(`revisionAutomatica.excepciones.tipos.${x}`) }))}
        />
        {tipo && (
          <Button variant="text" onClick={() => cambiar('tipo', null)}>
            {t('revisionAutomatica.excepciones.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>

      <Box id="panel-excepciones" role="tabpanel" aria-labelledby={`pestana-excepciones-${estado.toLowerCase()}`}>
        {consulta.isError && !consulta.data ? (
          <ErrorRecuperable mensaje={`${t('revisionAutomatica.excepciones.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
        ) : consulta.isPending ? (
          <EsqueletoTarjetas cantidad={3} lineas={2} etiqueta={t('revisionAutomatica.excepciones.titulo')} />
        ) : filas.length === 0 ? (
          <Tarjeta>
            {hayDatos ? (
              <EstadoVacio
                compacto
                icono={ShieldCheck}
                titulo={t('revisionAutomatica.excepciones.vacioFiltro')}
                accion={
                  <Button variant="outlined" onClick={() => cambiar('tipo', null)}>
                    {t('revisionAutomatica.excepciones.filtros.limpiar')}
                  </Button>
                }
              />
            ) : (
              <EstadoVacio
                compacto
                icono={estado === 'Pendientes' ? CircleCheck : ShieldCheck}
                titulo={estado === 'Pendientes' ? t('revisionAutomatica.excepciones.vacioPendientes') : t('revisionAutomatica.excepciones.vacioResueltas')}
                texto={estado === 'Pendientes' ? t('revisionAutomatica.excepciones.vacioPendientesAyuda') : t('revisionAutomatica.excepciones.vacioResueltasAyuda')}
              />
            )}
          </Tarjeta>
        ) : (
          <>
            {consulta.data.length >= MAXIMO_FILAS && (
              <Alert severity="info" sx={{ mb: 1.5 }}>
                {t(estado === 'Pendientes' ? 'revisionAutomatica.excepciones.limitePendientes' : 'revisionAutomatica.excepciones.limiteResueltas', { cantidad: String(MAXIMO_FILAS) })}
              </Alert>
            )}
            <ListaExcepciones
              filas={filas}
              etiqueta={t('revisionAutomatica.excepciones.listaEtiqueta', { estado: nombreDelEstado.toLowerCase() })}
              fila={(f) => (
                <FilaExcepcion
                  fila={f}
                  empresaId={empresa.id}
                  ahora={ahora}
                  area={nombreDeArea(f.areaId, f.areaCodigo)}
                  puedeRevisar={puedeRevisar}
                  esMiembro={miembro(f.areaId)}
                  resolvio={f.resueltaPor ? (nombres.get(f.resueltaPor) ?? null) : null}
                />
              )}
            />
          </>
        )}
      </Box>
    </>
  );
}
