import { Alert, Button } from '@mui/material';
import { SCOPES, type RegistroAuditoriaDto } from '@validasoft/api-client';
import { EstadoVacio, TablaConCursor, usePaginacionCursor } from '@validasoft/ui';
import { Download } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { EncabezadoPagina, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useDescarga } from '../informes/api';
import { RUTAS, sinPaginacion } from '../informes/modelo';
import { guardarArchivo } from '../vencimientos/api';
import { useAuditoria } from './api';
import { useColumnasAuditoria } from './columnas';
import { DetalleAuditoria } from './DetalleAuditoria';
import { FiltrosAuditoria } from './FiltrosAuditoria';
import { formatearMomento, hayFiltros, leerFiltros, parametrosConsulta, rangoInvalido, textoAccion, textoEntidad, TIPO_POR_DEFECTO } from './modelo';

const TAMANO_PAGINA = 50;

/**
 * Auditoría de la empresa activa (docs/06 §`/auditoria`): cambios de configuración (de la empresa y del grupo) con su
 * diff campo por campo, y accesos a documentos y certificados. Tipo y filtros en la URL; paginación por cursor.
 */
export function PaginaAuditoria() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.auditoriaLeer);
  const puedeExportar = permisos.puede(SCOPES.auditoriaExportar);
  const mostrar = useAvisos((s) => s.mostrar);
  const descarga = useDescarga(empresa.id);
  const [parametros, setParametros] = useSearchParams();
  const filtros = leerFiltros(parametros);
  const { tipo, entidad, entidadId, actor, desde, hasta } = filtros;
  const errorRango = rangoInvalido(filtros);
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  const columnas = useColumnasAuditoria();
  const [abierto, setAbierto] = useState<RegistroAuditoriaDto | null>(null);

  useEffect(() => {
    reiniciar();
    setAbierto(null);
  }, [tipo, entidad, entidadId, actor, desde, hasta, empresa.id, reiniciar]);

  const consulta = useAuditoria(empresa.id, parametrosConsulta(filtros, TAMANO_PAGINA, paginacion.cursor), puede && !errorRango);

  if (!puede) return <SinPermiso seccion={t('nav.auditoria')} />;

  const conFiltros = hayFiltros(filtros);
  const limpiar = () => setParametros(tipo === TIPO_POR_DEFECTO ? new URLSearchParams() : new URLSearchParams({ tipo }), { replace: true });
  const ayudaVacio =
    tipo === 'Acceso'
      ? t('auditoria.vacioAyudaAcceso', { empresa: empresa.nombre })
      : tipo === 'Identidad'
        ? t('auditoria.vacioAyudaIdentidad')
        : t('auditoria.vacioAyudaConfiguracion', { empresa: empresa.nombre });
  // La exportación lleva los filtros de la pantalla, sin la paginación (IF-4).
  const exportar = () =>
    descarga.mutate(
      { url: RUTAS.auditoriaExcel, params: sinPaginacion(parametrosConsulta(filtros, TAMANO_PAGINA, null)) },
      {
        onSuccess: (archivo) => guardarArchivo(archivo, t('informes.auditoria.excelNombre')),
        onError: (error) => mostrar(`${t('informes.descargaError')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );

  return (
    <>
      <EncabezadoPagina
        titulo={t('auditoria.titulo')}
        subtitulo={t('auditoria.subtitulo', { empresa: empresa.nombre })}
        acciones={
          puedeExportar ? (
            <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={descarga.isPending || errorRango} onClick={exportar}>
              {t('informes.auditoria.excel')}
            </Button>
          ) : undefined
        }
      />

      <FiltrosAuditoria filtros={filtros} />

      {errorRango ? (
        <Alert severity="warning">{t('auditoria.filtros.rangoInvalido')}</Alert>
      ) : (
        <TablaConCursor
          etiqueta={t('auditoria.titulo')}
          columnas={columnas}
          filas={consulta.data?.datos}
          claveFila={(r) => r.id}
          cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
          error={consulta.isError ? `${t('auditoria.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
          onReintentar={() => void consulta.refetch()}
          paginacion={paginacion}
          siguienteCursor={consulta.data?.siguienteCursor}
          onFila={setAbierto}
          etiquetaFila={(r) => t('auditoria.abrir', { accion: textoAccion(r.accion, t), entidad: textoEntidad(r.entidad.tipo, t), fecha: formatearMomento(r.fecha) })}
          vacio={
            <EstadoVacio
              compacto
              titulo={conFiltros ? t('auditoria.vacioFiltros') : t('auditoria.vacio')}
              texto={conFiltros ? undefined : ayudaVacio}
              accion={
                conFiltros ? (
                  <Button variant="outlined" onClick={limpiar}>
                    {t('auditoria.filtros.limpiar')}
                  </Button>
                ) : undefined
              }
            />
          }
        />
      )}

      {abierto && <DetalleAuditoria registro={abierto} empresaId={empresa.id} onCerrar={() => setAbierto(null)} />}
    </>
  );
}
