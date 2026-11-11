import { Box, Button, Stack } from '@mui/material';
import { CampoFecha, EsqueletoTabla, EsqueletoTarjeta } from '@validasoft/ui';
import { Download } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { guardarArchivo } from '../../vencimientos/api';
import { useDescargarInformeSagrilaft, useInformeSagrilaft } from '../api';
import { fechaIso } from '../consultaManual';
import { TablaSagrilaft } from './TablaSagrilaft';
import { RiesgoSagrilaft, TarjetasSagrilaft, TiemposSagrilaft } from './TarjetasSagrilaft';
import { TendenciaSagrilaft } from './TendenciaSagrilaft';

/**
 * Informe SAGRILAFT completo (CU-19): cuántos terceros tienen estudio vigente, por vencer o vencido, el riesgo, la tendencia
 * de los últimos 12 meses, los tiempos del oficial y el detalle por tercero, con el Excel de tres hojas (terceros, personas y
 * verificaciones). La fecha es el último mes de la tendencia.
 */
export function InformeSagrilaft({ empresaId }: { empresaId: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [hoy] = useState(() => fechaIso(new Date()));
  const [hasta, setHasta] = useState(hoy);
  const params = hasta ? { hasta } : {};
  const consulta = useInformeSagrilaft(empresaId, params);
  const excel = useDescargarInformeSagrilaft(empresaId);
  const informe = consulta.data;

  const descargar = () =>
    excel.mutate(params, {
      onSuccess: (archivo) => guardarArchivo(archivo, t('cumplimiento.sagrilaft.nombreArchivo')),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    });

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <CampoFecha etiqueta={t('cumplimiento.sagrilaft.hasta')} valor={hasta} onCambiar={setHasta} maximo={hoy} />
        <Box sx={{ flex: 1 }} />
        <Button variant="outlined" startIcon={excel.isPending ? <IconoCarga /> : <Download size={16} aria-hidden />} onClick={descargar} disabled={excel.isPending || !informe}>
          {excel.isPending ? t('cumplimiento.sagrilaft.descargando') : t('cumplimiento.sagrilaft.descargar')}
        </Button>
      </Stack>

      {consulta.isError && !informe && <ErrorRecuperable mensaje={`${t('cumplimiento.sagrilaft.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && (
        <Stack spacing={2} aria-busy>
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(148px, 1fr))', gap: 1.5 }}>
            {Array.from({ length: 7 }, (_, i) => (
              <EsqueletoTarjeta key={i} lineas={1} />
            ))}
          </Box>
          <EsqueletoTarjeta lineas={4} alto={160} />
          <EsqueletoTabla columnas={6} filas={5} etiqueta={t('cumplimiento.sagrilaft.cargando')} />
        </Stack>
      )}
      {informe && (
        <Stack spacing={2} sx={{ opacity: consulta.isPlaceholderData ? 0.6 : 1, transition: 'opacity 120ms' }} aria-busy={consulta.isPlaceholderData || undefined}>
          <TarjetasSagrilaft informe={informe} />
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', lg: 'repeat(2, minmax(0, 1fr))' }, gap: 2, alignItems: 'start' }}>
            <RiesgoSagrilaft informe={informe} />
            <TiemposSagrilaft informe={informe} />
          </Box>
          <TendenciaSagrilaft meses={informe.tendencia} />
          <TablaSagrilaft filas={informe.filas} empresaId={empresaId} />
        </Stack>
      )}
    </Stack>
  );
}
