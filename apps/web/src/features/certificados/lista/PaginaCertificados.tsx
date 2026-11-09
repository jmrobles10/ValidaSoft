import { Button } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { BarraFiltros, CampoBusqueda, ChipEstado, EstadoVacio, FiltroMenu, TablaConCursor, usePaginacionCursor } from '@validasoft/ui';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { EncabezadoPagina, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useBusquedaEnUrl } from '../../../shared/useBusquedaEnUrl';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useCertificados } from '../api';
import { useColumnasCertificados } from './columnas';
import { ESPERA_BUSQUEDA_MS, ESTADOS_FILTRO_CERTIFICADOS, leerFiltros, parametrosConsulta } from './modelo';

const TAMANO_PAGINA = 25;

/**
 * Certificados de la empresa (CE-11): la emisión actual de cada vínculo, con su estado y hasta cuándo es vigente.
 * Filtros en la URL; al abrir una fila se va al vínculo, donde están las emisiones y las descargas.
 */
export function PaginaCertificados() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.certificadosLeer);
  const [parametros, setParametros] = useSearchParams();
  const filtros = leerFiltros(parametros);
  const { estado, q } = filtros;
  const busqueda = useBusquedaEnUrl(ESPERA_BUSQUEDA_MS);
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  const columnas = useColumnasCertificados();

  useEffect(() => {
    reiniciar();
  }, [estado, q, empresa.id, reiniciar]);

  const consulta = useCertificados(empresa.id, parametrosConsulta(filtros, TAMANO_PAGINA, paginacion.cursor), puede);

  if (!puede) return <SinPermiso seccion={t('nav.certificados')} />;

  const cambiarEstado = (valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set('estado', valor);
        else nuevos.delete('estado');
        return nuevos;
      },
      { replace: true },
    );
  const hayFiltros = Boolean(estado || q);
  const limpiar = () => setParametros(new URLSearchParams(), { replace: true });

  return (
    <>
      <EncabezadoPagina titulo={t('certificados.lista.titulo')} subtitulo={t('certificados.lista.subtitulo', { empresa: empresa.nombre })} />

      <BarraFiltros>
        <CampoBusqueda valor={busqueda.texto} onCambiar={busqueda.setTexto} placeholder={t('certificados.lista.buscar')} />
        <FiltroMenu
          etiqueta={t('certificados.lista.filtros.estado')}
          textoTodos={t('comun.todos')}
          valor={estado}
          onCambiar={cambiarEstado}
          opciones={ESTADOS_FILTRO_CERTIFICADOS.map((e) => ({
            valor: e,
            etiqueta: t(`certificados.lista.estados.${e}`),
            contenido: e === 'Bloqueante' ? undefined : <ChipEstado estado={e} />,
          }))}
        />
        {hayFiltros && (
          <Button variant="text" onClick={limpiar}>
            {t('certificados.lista.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>

      <TablaConCursor
        etiqueta={t('certificados.lista.titulo')}
        columnas={columnas}
        filas={consulta.data?.datos}
        claveFila={(f) => f.id}
        cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
        error={consulta.isError ? `${t('certificados.lista.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={consulta.data?.siguienteCursor}
        onFila={(f) => void navegar(`/e/${empresa.id}/vinculos/${f.vinculoId}`)}
        etiquetaFila={(f) => t('certificados.lista.abrir', { tercero: f.tercero.nombre, folio: f.folio })}
        vacio={
          <EstadoVacio
            compacto
            titulo={hayFiltros ? t('certificados.lista.vacioFiltros') : t('certificados.lista.vacio', { empresa: empresa.nombre })}
            texto={hayFiltros ? undefined : t('certificados.lista.vacioAyuda')}
            accion={
              hayFiltros ? (
                <Button variant="outlined" onClick={limpiar}>
                  {t('certificados.lista.filtros.limpiar')}
                </Button>
              ) : undefined
            }
          />
        }
      />
    </>
  );
}
