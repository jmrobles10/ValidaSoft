import { Alert, Box, Button } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { EstadoVacio, TablaConCursor, usePaginacionCursor } from '@validasoft/ui';
import { Download, FileText } from 'lucide-react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { EncabezadoPagina, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { guardarArchivo } from '../vencimientos/api';
import { useDescarga, useInformeEstado } from './api';
import { useColumnasEstado } from './columnas';
import { FiltrosEstadoExpedientes } from './FiltrosEstado';
import { leerFiltrosEstado, parametrosEstado, rangoInvalido, RUTAS } from './modelo';
import { PestanasInformes } from './PestanasInformes';
import { ResumenEstado } from './ResumenEstado';

const TAMANO_PAGINA = 50;

/**
 * Informe de estado de expedientes (IF-1, IF-2): totales, tiempos por área y cada expediente con sus días en carga, con
 * el tercero y en revisión. Filtros en la URL; Excel y PDF con los mismos filtros.
 */
export function PaginaEstadoExpedientes() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.expedientesLeer);
  const mostrar = useAvisos((s) => s.mostrar);
  const [parametros] = useSearchParams();
  const filtros = leerFiltrosEstado(parametros);
  const errorRango = rangoInvalido(filtros);
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  const columnas = useColumnasEstado();
  const descarga = useDescarga(empresa.id);
  const clave = parametros.toString();

  useEffect(() => {
    reiniciar();
  }, [clave, empresa.id, reiniciar]);

  const params = parametrosEstado(filtros);
  const consulta = useInformeEstado(empresa.id, { ...params, limite: TAMANO_PAGINA, cursor: paginacion.cursor ?? undefined }, puede && !errorRango);

  if (!puede) return <SinPermiso seccion={t('nav.informes')} />;

  const descargar = (url: string, nombre: string) =>
    descarga.mutate(
      { url, params },
      {
        onSuccess: (archivo) => guardarArchivo(archivo, nombre),
        onError: (error) => mostrar(`${t('informes.descargaError')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );
  const sinFilas = consulta.data?.resumen.total === 0;

  return (
    <>
      <EncabezadoPagina
        titulo={t('informes.titulo')}
        subtitulo={t('informes.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button
              variant="outlined"
              startIcon={<Download size={16} aria-hidden />}
              disabled={descarga.isPending || errorRango || sinFilas}
              onClick={() => descargar(RUTAS.estadoExcel, t('informes.estado.excelNombre'))}
            >
              {t('informes.excel')}
            </Button>
            <Button
              variant="outlined"
              startIcon={<FileText size={16} aria-hidden />}
              disabled={descarga.isPending || errorRango || sinFilas}
              onClick={() => descargar(RUTAS.estadoPdf, t('informes.estado.pdfNombre'))}
            >
              {t('informes.pdf')}
            </Button>
          </Box>
        }
      />
      <PestanasInformes empresaId={empresa.id} actual="estado-expedientes" />
      <FiltrosEstadoExpedientes empresaId={empresa.id} filtros={filtros} />

      {errorRango ? (
        <Alert severity="warning">{t('informes.estado.filtros.rangoInvalido')}</Alert>
      ) : (
        <>
          {consulta.data && <ResumenEstado resumen={consulta.data.resumen} />}
          <TablaConCursor
            etiqueta={t('informes.pestanas.estado')}
            columnas={columnas}
            filas={consulta.data?.datos}
            claveFila={(f) => f.id}
            cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
            error={consulta.isError ? `${t('informes.estado.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
            onReintentar={() => void consulta.refetch()}
            paginacion={paginacion}
            siguienteCursor={consulta.data?.siguienteCursor}
            onFila={(f) => void navegar(`/e/${empresa.id}/expedientes/${f.id}`)}
            etiquetaFila={(f) => t('informes.estado.abrir', { folio: f.folio, tercero: f.tercero.nombre })}
            vacio={<EstadoVacio compacto titulo={t('informes.estado.vacio')} />}
          />
        </>
      )}
    </>
  );
}
