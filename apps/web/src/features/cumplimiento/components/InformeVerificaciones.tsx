import { Box, Button, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { Dato, EstadoVacio, formatearFecha, RejillaDatos } from '@validasoft/ui';
import { Download, FileSpreadsheet } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, IconoCarga, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { CampoFecha } from '../../../shared/camposTransitorios';
import { useAvisos } from '../../../shared/useAvisos';
import { guardarArchivo } from '../../vencimientos/api';
import { useDescargarInformeCumplimiento, useInformeCumplimiento } from '../api';
import { claveRiesgo } from '../modelo';

/** `yyyy-MM-dd` de hoy menos `dias` en la zona del navegador (solo para el filtro inicial; la API usa la zona del negocio). */
function fechaISO(dias: number): string {
  const fecha = new Date(Date.now() - dias * 86_400_000);
  return `${String(fecha.getFullYear())}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
}

/**
 * Informe de verificaciones (VE-14): totales por verificación y proveedor, riesgo, estudios vencidos y listas por reconsultar en
 * el periodo, con el detalle por expediente y la exportación a Excel (los mismos filtros). Es la segunda vista del informe.
 */
export function InformeVerificaciones({ empresaId }: { empresaId: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [desde, setDesde] = useState(() => fechaISO(90));
  const [hasta, setHasta] = useState(() => fechaISO(0));
  const params = { desde, hasta };
  const consulta = useInformeCumplimiento(empresaId, params, Boolean(desde && hasta));
  const excel = useDescargarInformeCumplimiento(empresaId);
  const informe = consulta.data;

  const exportar = () =>
    excel.mutate(params, {
      onSuccess: (archivo) => guardarArchivo(archivo, t('cumplimiento.informe.nombreArchivo')),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    });

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <CampoFecha etiqueta={t('cumplimiento.informe.desde')} valor={desde} onCambiar={setDesde} />
        <CampoFecha etiqueta={t('cumplimiento.informe.hasta')} valor={hasta} onCambiar={setHasta} />
        <Box sx={{ flex: 1 }} />
        <Button variant="outlined" startIcon={excel.isPending ? <IconoCarga /> : <Download size={16} aria-hidden />} onClick={exportar} disabled={excel.isPending}>
          {excel.isPending ? t('cumplimiento.informe.exportando') : t('cumplimiento.informe.exportar')}
        </Button>
      </Stack>

      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {informe && (
        <>
          <Tarjeta titulo={t('cumplimiento.informe.resumen')}>
            <RejillaDatos columnas={3}>
              {informe.verificaciones.map((v) => (
                <Dato key={`${v.tipo}-${v.proveedor ?? ''}`} etiqueta={`${t(`cumplimiento.tipos.${v.tipo as 'ListasRestrictivas'}`)}${v.proveedor ? ` · ${v.proveedor}` : ''}`}>
                  {`${t('cumplimiento.informe.total')}: ${String(v.total)} · ${t('cumplimiento.informe.conCoincidencias')}: ${String(v.conCoincidencias)} · ${t('cumplimiento.informe.confirmadas')}: ${String(v.confirmadas)} · ${t('cumplimiento.informe.pendientes')}: ${String(v.pendientes)}`}
                </Dato>
              ))}
              <Dato etiqueta={t('cumplimiento.informe.riesgo')}>
                {informe.riesgo.length === 0
                  ? t('cumplimiento.informe.sinRiesgo')
                  : informe.riesgo.map((r) => `${t(`cumplimiento.riesgos.${claveRiesgo(r.nivel)}`)}: ${String(r.cantidad)}`).join(' · ')}
              </Dato>
              <Dato etiqueta={t('cumplimiento.informe.estudiosVencidos')}>{String(informe.estudiosVencidos)}</Dato>
              <Dato etiqueta={t('cumplimiento.informe.pendientesReconsulta')}>{String(informe.pendientesReconsulta)}</Dato>
            </RejillaDatos>
          </Tarjeta>
          <Tarjeta>
            {informe.filas.length === 0 ? (
              <EstadoVacio icono={FileSpreadsheet} titulo={t('cumplimiento.informe.vacio')} compacto />
            ) : (
              <Box sx={{ overflowX: 'auto' }}>
                <Table size="small" aria-label={t('cumplimiento.pestanas.informe')}>
                  <TableHead>
                    <TableRow>
                      {(['folio', 'tercero', 'verificacion', 'proveedor', 'estado', 'fecha', 'coincidencias', 'riesgo', 'concepto', 'vigenteHasta'] as const).map((c) => (
                        <TableCell key={c}>{t(`cumplimiento.informe.columnas.${c}`)}</TableCell>
                      ))}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {informe.filas.map((f) => (
                      <TableRow key={`${f.expedienteId}-${f.verificacion}`}>
                        <TableCell>
                          <Typography component="span" variant="mono">
                            {f.folio}
                          </Typography>
                        </TableCell>
                        <TableCell>{f.tercero}</TableCell>
                        <TableCell>{t(`cumplimiento.tipos.${f.verificacion as 'ListasRestrictivas'}`)}</TableCell>
                        <TableCell>{f.proveedor ?? '—'}</TableCell>
                        <TableCell>{t(`cumplimiento.estados.${f.estado as 'Pendiente'}`)}</TableCell>
                        <TableCell>{f.fecha ? formatearFecha(f.fecha) : '—'}</TableCell>
                        <TableCell align="right">{`${String(f.coincidencias)} (${String(f.confirmadas)}/${String(f.descartadas)})`}</TableCell>
                        <TableCell>{f.nivelRiesgo ?? '—'}</TableCell>
                        <TableCell>{f.concepto ? t(`cumplimiento.conceptos.${f.concepto as 'Favorable'}`) : '—'}</TableCell>
                        <TableCell>{f.vigenteHasta ? formatearFecha(f.vigenteHasta) : '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Tarjeta>
        </>
      )}
    </Stack>
  );
}
