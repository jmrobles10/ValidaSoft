import { Alert, Button, Stack } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { EsqueletoTabla, EsqueletoTarjeta } from '@validasoft/ui';
import { Download } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { Cifra, GrupoCifras } from '../inicio/Cifras';
import { guardarArchivo } from '../vencimientos/api';
import { useDescarga, useInformeSla } from './api';
import { FiltrosInformeSla } from './FiltrosInformeSla';
import { rangoInvalido, RUTAS } from './modelo';
import { formatearCumplimiento, leerFiltrosSla, parametrosSla } from './modeloSla';
import { PestanasInformes } from './PestanasInformes';
import { CumplimientoPorArea, VencidosAhora } from './TablasInformeSla';

/**
 * Cumplimiento del plazo de revisión (OP-14): cuántas revisiones de cada área se resolvieron dentro de su plazo, el promedio de
 * días y las que hoy están vencidas, con enlace al expediente. Filtros en la URL; el Excel lleva los mismos.
 */
export function PaginaInformeSla() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.expedientesLeer);
  const mostrar = useAvisos((s) => s.mostrar);
  const [parametros] = useSearchParams();
  const filtros = leerFiltrosSla(parametros);
  const errorRango = rangoInvalido(filtros);
  const params = parametrosSla(filtros);
  const consulta = useInformeSla(empresa.id, params, puede && !errorRango);
  const descarga = useDescarga(empresa.id);

  if (!puede) return <SinPermiso seccion={t('nav.informes')} />;

  const descargar = () =>
    descarga.mutate(
      { url: RUTAS.slaExcel, params },
      {
        onSuccess: (archivo) => guardarArchivo(archivo, t('informes.sla.excelNombre')),
        onError: (error) => mostrar(`${t('informes.descargaError')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );
  const informe = consulta.data;
  const sinDatos = informe?.porArea.length === 0 && informe.vencidos.length === 0;
  const sinDato = t('informes.sinDato');

  return (
    <>
      <EncabezadoPagina
        titulo={t('informes.titulo')}
        subtitulo={t('informes.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={descarga.isPending || errorRango || sinDatos} onClick={descargar}>
            {t('informes.excel')}
          </Button>
        }
      />
      <PestanasInformes empresaId={empresa.id} actual="sla" />
      <FiltrosInformeSla empresaId={empresa.id} filtros={filtros} />

      {errorRango && <Alert severity="warning">{t('informes.sla.filtros.rangoInvalido')}</Alert>}
      {!errorRango && consulta.isError && <ErrorRecuperable mensaje={`${t('informes.sla.errorCarga')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      {!errorRango && consulta.isPending && (
        <Stack spacing={1.5} role="status" aria-busy="true" aria-label={t('informes.sla.titulo')}>
          <EsqueletoTarjeta lineas={2} />
          <EsqueletoTabla columnas={6} filas={3} />
        </Stack>
      )}
      {!errorRango && informe && (
        <Stack spacing={1.5} sx={{ mb: 2 }}>
          <GrupoCifras titulo={t('informes.sla.titulo')} nota={t('informes.sla.ayuda')}>
            <Cifra etiqueta={t('informes.sla.resumen.expedientes')} valor={String(informe.resumen.expedientes)} />
            <Cifra etiqueta={t('informes.sla.resumen.rondas')} valor={String(informe.resumen.rondas)} />
            <Cifra etiqueta={t('informes.sla.resumen.aTiempo')} valor={String(informe.resumen.aDentroDelPlazo)} />
            <Cifra etiqueta={t('informes.sla.resumen.fuera')} valor={String(informe.resumen.fueraDelPlazo)} />
            <Cifra etiqueta={t('informes.sla.resumen.cumplimiento')} valor={formatearCumplimiento(informe.resumen.cumplimiento, sinDato)} />
            <Cifra etiqueta={t('informes.sla.resumen.vencidos')} valor={String(informe.resumen.vencidosAhora)} />
          </GrupoCifras>
          <CumplimientoPorArea areas={informe.porArea} />
          <VencidosAhora empresaId={empresa.id} vencidos={informe.vencidos} />
        </Stack>
      )}
    </>
  );
}
