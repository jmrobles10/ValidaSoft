import { Box, Button } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { BarraFiltros, EstadoVacio, FiltroMenu, TablaConCursor, usePaginacionCursor } from '@validasoft/ui';
import { Download } from 'lucide-react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { EncabezadoPagina, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { Cifra, GrupoCifras } from '../inicio/Cifras';
import { guardarArchivo } from '../vencimientos/api';
import { useTiposVinculo } from '../vinculos/api';
import { useDescarga, useInformeVinculos } from './api';
import { useColumnasVinculos } from './columnas';
import { ESTADOS_VINCULO, leerFiltrosVinculos, parametrosVinculos, RUTAS } from './modelo';
import { PestanasInformes } from './PestanasInformes';

const TAMANO_PAGINA = 50;

/**
 * Informe de terceros y vínculos (IF-1): estado de cada vínculo, certificado, expediente en curso, vencimientos de sus
 * documentos y secciones de la ficha por validar. Se descarga en Excel con los mismos filtros.
 */
export function PaginaInformeVinculos() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.vinculosLeer);
  const mostrar = useAvisos((s) => s.mostrar);
  const [parametros, setParametros] = useSearchParams();
  const filtros = leerFiltrosVinculos(parametros);
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  const columnas = useColumnasVinculos();
  const descarga = useDescarga(empresa.id);
  const tipos = useTiposVinculo(empresa.id, permisos.puede(SCOPES.configuracionLeer));

  useEffect(() => {
    reiniciar();
  }, [filtros.estado, filtros.tipoVinculoId, empresa.id, reiniciar]);

  const params = parametrosVinculos(filtros);
  const consulta = useInformeVinculos(empresa.id, { ...params, limite: TAMANO_PAGINA, cursor: paginacion.cursor ?? undefined }, puede);

  if (!puede) return <SinPermiso seccion={t('nav.informes')} />;

  const cambiar = (clave: 'estado' | 'tipoVinculoId', valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );
  const descargar = () =>
    descarga.mutate(
      { url: RUTAS.vinculosExcel, params },
      {
        onSuccess: (archivo) => guardarArchivo(archivo, t('informes.vinculos.excelNombre')),
        onError: (error) => mostrar(`${t('informes.descargaError')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );
  const resumen = consulta.data?.resumen;

  return (
    <>
      <EncabezadoPagina
        titulo={t('informes.titulo')}
        subtitulo={t('informes.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={descarga.isPending || resumen?.total === 0} onClick={descargar}>
            {t('informes.excel')}
          </Button>
        }
      />
      <PestanasInformes empresaId={empresa.id} actual="vinculos" />
      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('informes.vinculos.filtros.estado')}
          textoTodos={t('comun.todos')}
          valor={filtros.estado}
          onCambiar={(v) => cambiar('estado', v)}
          opciones={ESTADOS_VINCULO.map((e) => ({ valor: e, etiqueta: t(`informes.vinculos.estados.${e}`) }))}
        />
        {tipos.data && (
          <FiltroMenu
            etiqueta={t('informes.vinculos.filtros.tipoVinculo')}
            textoTodos={t('comun.todos')}
            valor={filtros.tipoVinculoId}
            onCambiar={(v) => cambiar('tipoVinculoId', v)}
            opciones={tipos.data.map((tv) => ({ valor: tv.id, etiqueta: tv.nombre }))}
          />
        )}
      </BarraFiltros>

      {resumen && (
        <Box sx={{ mb: 2 }}>
          <GrupoCifras titulo={t('informes.estado.resumen.titulo')}>
            <Cifra etiqueta={t('informes.vinculos.resumen.total')} valor={String(resumen.total)} />
            <Cifra etiqueta={t('informes.vinculos.resumen.certificado')} valor={String(resumen.conCertificadoVigente)} />
            <Cifra etiqueta={t('informes.vinculos.resumen.porVencer')} valor={String(resumen.conDocumentosPorVencer)} />
            <Cifra etiqueta={t('informes.vinculos.resumen.vencidos')} valor={String(resumen.conDocumentosVencidos)} />
            <Cifra etiqueta={t('informes.vinculos.resumen.secciones')} valor={String(resumen.conSeccionesPendientes)} />
          </GrupoCifras>
        </Box>
      )}

      <TablaConCursor
        etiqueta={t('informes.pestanas.vinculos')}
        columnas={columnas}
        filas={consulta.data?.datos}
        claveFila={(f) => f.vinculoId}
        cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
        error={consulta.isError ? `${t('informes.vinculos.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={consulta.data?.siguienteCursor}
        onFila={(f) => void navegar(`/e/${empresa.id}/vinculos/${f.vinculoId}`)}
        etiquetaFila={(f) => t('informes.vinculos.abrir', { tercero: f.tercero.nombre })}
        vacio={<EstadoVacio compacto titulo={t('informes.vinculos.vacio')} />}
      />
    </>
  );
}
