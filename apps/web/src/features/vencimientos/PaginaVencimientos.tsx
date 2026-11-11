import { Alert, Box, Button } from '@mui/material';
import { esErrorApi, SCOPES, type VencimientoDto } from '@validasoft/api-client';
import { BarraFiltros, EstadoVacio, FiltroMenu, TablaConCursor, usePaginacionCursor } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { BellRing, Download, Megaphone } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { DialogoConfirmacion, EncabezadoPagina, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { guardarArchivo, useDescargarExcelVencimientos, useRecordarVencimientos, useVencimientos } from './api';
import { useColumnasVencimientos } from './columnas';
import { claveVencimiento, ESTADOS_VENCIMIENTO, HORIZONTE_POR_DEFECTO, HORIZONTES, leerFiltros, lote, MAXIMO_POR_RECORDATORIO, mensajeRecordatorio, tercerosDelLote } from './modelo';

/** Los códigos de lote son compartidos con otras acciones en lote: aquí se explican en términos de recordatorios. */
function errorAlRecordar(error: unknown, t: TFunction): string {
  if (esErrorApi(error) && error.codigo === 'lote-vacio') return t('vencimientos.errores.lote-vacio');
  if (esErrorApi(error) && error.codigo === 'lote-demasiado-grande') return t('vencimientos.errores.lote-demasiado-grande');
  return mensajeDeError(error, t);
}

/**
 * Vencimientos (VE-1..VE-3): por vínculo y documento compartido con la empresa, hasta cuándo está vigente para ella, en
 * qué estado y desde qué fecha se calculó. Se descarga en Excel con los mismos filtros y se le recuerda al tercero.
 */
export function PaginaVencimientos() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.documentosLeer);
  const puedeRecordar = permisos.puede(SCOPES.vinculosEscribir);
  const puedeVerCampanias = permisos.puede(SCOPES.expedientesLeer);
  const mostrar = useAvisos((s) => s.mostrar);
  const [parametros, setParametros] = useSearchParams();
  const { dias, estado } = leerFiltros(parametros);
  const paginacion = usePaginacionCursor();
  const [seleccion, setSeleccion] = useState<ReadonlySet<string>>(new Set());
  const [confirmando, setConfirmando] = useState(false);

  useEffect(() => {
    setSeleccion(new Set());
  }, [dias, estado, empresa.id]);

  const filtrosApi = { dias, estado: estado || undefined };
  const consulta = useVencimientos(empresa.id, filtrosApi, puede);
  const recordar = useRecordarVencimientos(empresa.id);
  const excel = useDescargarExcelVencimientos(empresa.id);
  const filas = useMemo(() => consulta.data ?? [], [consulta.data]);
  const porRecordar = lote(filas, seleccion);
  const excede = porRecordar.length > MAXIMO_POR_RECORDATORIO;

  const alternar = useCallback(
    (f: VencimientoDto) =>
      setSeleccion((previa) => {
        const nueva = new Set(previa);
        const clave = claveVencimiento(f);
        if (nueva.has(clave)) nueva.delete(clave);
        else nueva.add(clave);
        return nueva;
      }),
    [],
  );
  const columnas = useColumnasVencimientos({ conSeleccion: puedeRecordar && filas.length > 0, seleccion, alternar });

  if (!puede) return <SinPermiso seccion={t('nav.vencimientos')} />;

  const cambiarFiltro = (clave: 'dias' | 'estado', valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );
  // En el filtro de horizonte «Todos» es `dias=0`; el horizonte por defecto no se escribe en la URL.
  const cambiarHorizonte = (valor: string) => cambiarFiltro('dias', valor === '' ? '0' : valor === String(HORIZONTE_POR_DEFECTO) ? '' : valor);
  const hayFiltros = dias !== HORIZONTE_POR_DEFECTO || estado !== '';

  const descargar = () =>
    excel.mutate(filtrosApi, {
      onSuccess: (archivo) => guardarArchivo(archivo, t('vencimientos.excelNombre')),
      onError: (error) => mostrar(`${t('vencimientos.excelError')} ${mensajeDeError(error, t)}`, 'error'),
    });
  const confirmar = () =>
    recordar.mutate(
      { vencimientos: porRecordar },
      {
        onSuccess: (r) => {
          const { mensaje, severidad } = mensajeRecordatorio(r, t);
          mostrar(mensaje, severidad);
          setSeleccion(new Set());
        },
        onError: (error) => mostrar(errorAlRecordar(error, t), 'error'),
        onSettled: () => setConfirmando(false),
      },
    );

  const vacio = estado ? t('vencimientos.vacioFiltros') : dias === 0 ? t('vencimientos.vacioTodos') : t('vencimientos.vacio', { count: dias });

  return (
    <>
      <EncabezadoPagina
        titulo={t('vencimientos.titulo')}
        subtitulo={t('vencimientos.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {puedeVerCampanias && (
              <Button variant="outlined" startIcon={<Megaphone size={16} aria-hidden />} onClick={() => void navegar(`/e/${empresa.id}/vencimientos/campanias`)}>
                {t('campanias.irACampanias')}
              </Button>
            )}
            <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={excel.isPending || filas.length === 0} onClick={descargar}>
              {t('vencimientos.excel')}
            </Button>
            {puedeRecordar && filas.length > 0 && (
              <>
                <Button variant="outlined" onClick={() => setSeleccion(porRecordar.length > 0 ? new Set() : new Set(filas.map(claveVencimiento)))}>
                  {porRecordar.length > 0 ? t('vencimientos.quitarSeleccion') : t('vencimientos.seleccionarTodos')}
                </Button>
                <Button variant="contained" startIcon={<BellRing size={16} aria-hidden />} disabled={porRecordar.length === 0 || excede} onClick={() => setConfirmando(true)}>
                  {porRecordar.length > 0 ? t('vencimientos.recordar', { count: porRecordar.length }) : t('vencimientos.recordarSinSeleccion')}
                </Button>
              </>
            )}
          </Box>
        }
      />

      {excede && (
        <Alert severity="info" sx={{ mb: 1.5 }}>
          {t('vencimientos.maximo', { maximo: String(MAXIMO_POR_RECORDATORIO) })}
        </Alert>
      )}

      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('vencimientos.filtros.horizonte')}
          textoTodos={t('vencimientos.filtros.horizontes.0')}
          valor={dias === 0 ? '' : String(dias)}
          onCambiar={cambiarHorizonte}
          opciones={HORIZONTES.filter((h) => h !== 0).map((h) => ({ valor: String(h), etiqueta: t(`vencimientos.filtros.horizontes.${h}`) }))}
        />
        <FiltroMenu
          etiqueta={t('vencimientos.filtros.estado')}
          textoTodos={t('comun.todos')}
          valor={estado}
          onCambiar={(valor) => cambiarFiltro('estado', valor)}
          opciones={ESTADOS_VENCIMIENTO.map((e) => ({ valor: e, etiqueta: t(`vencimientos.estados.${e}`) }))}
        />
        {hayFiltros && (
          <Button variant="text" onClick={() => setParametros(new URLSearchParams(), { replace: true })}>
            {t('vencimientos.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>

      <TablaConCursor
        etiqueta={t('vencimientos.titulo')}
        columnas={columnas}
        filas={consulta.data}
        claveFila={claveVencimiento}
        cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
        error={consulta.isError ? `${t('vencimientos.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={null}
        onFila={(f) => void navegar(`/e/${empresa.id}/vinculos/${f.vinculoId}`)}
        etiquetaFila={(f) => t('vencimientos.abrir', { tercero: f.tercero.nombre, documento: f.documento })}
        vacio={<EstadoVacio compacto titulo={vacio} />}
      />

      {confirmando && (
        <DialogoConfirmacion
          tono="primary"
          titulo={t('vencimientos.confirmarTitulo', { count: porRecordar.length })}
          mensaje={t('vencimientos.confirmarTexto', { count: tercerosDelLote(porRecordar) })}
          confirmar={t('vencimientos.confirmar')}
          ocupado={recordar.isPending}
          onConfirmar={confirmar}
          onCerrar={() => setConfirmando(false)}
        />
      )}
    </>
  );
}
