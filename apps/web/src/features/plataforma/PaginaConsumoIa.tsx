import { Box, Button, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material';
import { BarraFiltros, CampoBusqueda, Dato, EstadoVacio, RejillaDatos, TablaEsqueleto } from '@validasoft/ui';
import { Download, SearchX, Sparkles } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable, IconoCarga, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useBusquedaEnUrl } from '../../shared/useBusquedaEnUrl';
import { guardarArchivo } from '../vencimientos/api';
import { useConsumoIa, useExportarConsumoIa, useTarifaIa } from './apiIdentidad';
import { ESPERA_BUSQUEDA_GRUPOS_MS } from './modelo';
import { aNumero, etiquetaDePeriodo, formatearEntero, formatearMoneda, formatearTokens, formatearUsd, periodoDe, periodosRecientes } from './modeloConsumo';
import { TablaConsumo } from './TablaConsumo';
import { TarjetaTarifaIa } from './TarjetaTarifaIa';

function Esqueleto() {
  const { t } = useTranslation();
  return (
    <Stack spacing={2} role="status" aria-busy aria-label={t('comun.cargando')}>
      <Skeleton variant="rounded" height={88} />
      <TablaEsqueleto columnas={6} filas={5} />
    </Stack>
  );
}

/**
 * Uso de IA por cliente (PI-18): para cada grupo y empresa, en el mes elegido, las validaciones incluidas y adicionales, los tokens, el costo del proveedor y lo
 * que valen las adicionales con el precio vigente. Exportable a Excel o CSV con los mismos filtros.
 */
export function PaginaConsumoIa() {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [parametros, setParametros] = useSearchParams();
  const meses = useMemo(() => periodosRecientes(new Date()), []);
  const periodoUrl = parametros.get('periodo') ?? '';
  const periodo = meses.includes(periodoUrl) ? periodoUrl : periodoDe(new Date());
  const q = (parametros.get('q') ?? '').trim();
  const busqueda = useBusquedaEnUrl(ESPERA_BUSQUEDA_GRUPOS_MS);
  const consulta = useConsumoIa({ periodo, q });
  const tarifa = useTarifaIa();
  const exportar = useExportarConsumoIa();

  const grupos = useMemo(() => (consulta.data?.pages ?? []).flatMap((p) => p.grupos), [consulta.data]);
  const resumen = consulta.data?.pages[0];
  const moneda = tarifa.data?.moneda ?? resumen?.moneda ?? 'COP';

  const cambiarPeriodo = (valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        nuevos.set('periodo', valor);
        return nuevos;
      },
      { replace: true },
    );
  const descargar = (formato: 'xlsx' | 'csv') =>
    exportar.mutate(
      { periodo, q, formato },
      {
        onSuccess: (archivo) => {
          guardarArchivo(archivo, t(`consola.consumo.archivo.${formato}`, { periodo }));
          mostrar(t('consola.consumo.exportado', { periodo: etiquetaDePeriodo(periodo) }));
        },
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      },
    );

  return (
    <>
      <EncabezadoPagina
        titulo={t('consola.consumo.titulo')}
        subtitulo={t('consola.consumo.subtitulo')}
        acciones={
          <Stack direction="row" spacing={1}>
            {(['xlsx', 'csv'] as const).map((formato) => (
              <Button key={formato} variant="outlined" disabled={exportar.isPending || !resumen} startIcon={exportar.isPending && exportar.variables.formato === formato ? <IconoCarga /> : <Download size={16} aria-hidden />} onClick={() => descargar(formato)}>
                {t(`consola.consumo.exportar.${formato}`)}
              </Button>
            ))}
          </Stack>
        }
      />
      <BarraFiltros>
        <TextField select size="small" label={t('consola.consumo.periodo')} value={periodo} onChange={(e) => cambiarPeriodo(e.target.value)} sx={{ minWidth: 200 }}>
          {meses.map((m) => (
            <MenuItem key={m} value={m}>
              {etiquetaDePeriodo(m)}
            </MenuItem>
          ))}
        </TextField>
        <CampoBusqueda valor={busqueda.texto} onCambiar={busqueda.setTexto} placeholder={t('consola.consumo.buscar')} ancho={300} />
      </BarraFiltros>
      {consulta.isError && !consulta.data && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && <Esqueleto />}
      {resumen && (
        <Stack spacing={2}>
          <Tarjeta titulo={t('consola.consumo.totales.titulo', { periodo: etiquetaDePeriodo(periodo) })}>
            <RejillaDatos columnas={3}>
              <Dato etiqueta={t('consola.consumo.totales.clientes')}>{t('consola.consumo.totales.clientesValor', { grupos: formatearEntero(resumen.totales.grupos), empresas: formatearEntero(resumen.totales.empresas) })}</Dato>
              <Dato etiqueta={t('consola.consumo.totales.incluidas')}>{formatearEntero(resumen.totales.incluidasConsumidas)}</Dato>
              <Dato etiqueta={t('consola.consumo.totales.adicionales')}>{formatearEntero(resumen.totales.adicionalesConsumidas)}</Dato>
              <Dato etiqueta={t('consola.consumo.totales.tokens')}>{`${formatearTokens(resumen.totales.tokensEntrada)} · ${formatearTokens(resumen.totales.tokensSalida)}`}</Dato>
              <Dato etiqueta={t('consola.consumo.totales.costo')}>{formatearUsd(resumen.totales.costoUsd)}</Dato>
              <Dato etiqueta={t('consola.consumo.totales.valor')}>{aNumero(resumen.totales.valorAdicional) > 0 ? formatearMoneda(resumen.totales.valorAdicional, moneda) : '—'}</Dato>
            </RejillaDatos>
          </Tarjeta>
          <TarjetaTarifaIa />
          {grupos.length === 0 ? (
            <EstadoVacio
              icono={q ? SearchX : Sparkles}
              titulo={q ? t('consola.consumo.vacioBusqueda', { q }) : t('consola.consumo.vacio', { periodo: etiquetaDePeriodo(periodo) })}
              texto={q ? t('consola.consumo.vacioBusquedaAyuda') : t('consola.consumo.vacioAyuda')}
            />
          ) : (
            <Box aria-busy={consulta.isPlaceholderData} sx={{ opacity: consulta.isPlaceholderData ? 0.6 : 1, transition: 'opacity 120ms' }}>
              <TablaConsumo grupos={grupos} moneda={moneda} />
              {consulta.hasNextPage ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', pt: 2 }}>
                  <Button variant="outlined" onClick={() => void consulta.fetchNextPage()} disabled={consulta.isFetchingNextPage} startIcon={consulta.isFetchingNextPage ? <IconoCarga /> : undefined}>
                    {t('consola.consumo.verMas')}
                  </Button>
                </Box>
              ) : (
                <Typography variant="caption" role="status" sx={{ display: 'block', textAlign: 'center', color: 'text.secondary', pt: 2 }}>
                  {t('consola.consumo.fin', { count: grupos.length })}
                </Typography>
              )}
            </Box>
          )}
        </Stack>
      )}
    </>
  );
}
