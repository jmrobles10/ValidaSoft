import { Box, Button, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { getListarOperadoresPlataformaQueryOptions } from '@validasoft/api-client';
import { BarraFiltros, CampoFecha, EstadoVacio, FilasEsqueleto, FiltroMenu, MarcoTabla } from '@validasoft/ui';
import { CircleAlert, CircleCheck, History } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable, IconoCarga } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { formatearMomento } from '../auditoria/modelo';
import { useIngresosOperadores } from './apiIdentidad';
import {
  dispositivoLegible,
  EVENTOS_DE_INGRESO,
  hayFiltrosDeIngresos,
  leerFiltrosDeIngresos,
  parametrosDeIngresos,
  rangoDeIngresosInvalido,
  type FiltrosDeIngresos,
} from './modeloIngresos';

const COLUMNAS = 7;

type Clave = keyof FiltrosDeIngresos;

/**
 * Ingresos de los operadores de Plataforma (PI-16): la auditoría de quién entró, quién falló, quién registró o revocó una passkey y quién confirmó una acción
 * sensible, de lo más reciente a lo más antiguo, con filtros por operador, evento, resultado y fechas (todo en la URL).
 */
export function PaginaIngresosOperadores() {
  const { t } = useTranslation();
  const [parametros, setParametros] = useSearchParams();
  const filtros = useMemo(() => leerFiltrosDeIngresos(parametros), [parametros]);
  const consulta = useIngresosOperadores(useMemo(() => parametrosDeIngresos(filtros), [filtros]));
  const operadores = useQuery(getListarOperadoresPlataformaQueryOptions({ request: { empresa: null } }));
  const filas = useMemo(() => (consulta.data?.pages ?? []).flatMap((p) => p.datos), [consulta.data]);
  const errorRango = rangoDeIngresosInvalido(filtros);

  const cambiar = (clave: Clave, valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );
  const campoFecha = (clave: 'desde' | 'hasta') => <CampoFecha etiqueta={t(`consola.ingresos.filtros.${clave}`)} valor={filtros[clave]} onCambiar={(iso) => cambiar(clave, iso)} error={errorRango} sx={{ width: 170 }} />;

  return (
    <>
      <EncabezadoPagina titulo={t('consola.ingresos.titulo')} subtitulo={t('consola.ingresos.subtitulo')} />
      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('consola.ingresos.filtros.operador')}
          textoTodos={t('comun.todos')}
          valor={filtros.operador}
          onCambiar={(v) => cambiar('operador', v)}
          opciones={(operadores.data ?? []).map((o) => ({ valor: o.id, etiqueta: o.nombre }))}
        />
        <FiltroMenu
          etiqueta={t('consola.ingresos.filtros.evento')}
          textoTodos={t('comun.todos')}
          valor={filtros.evento}
          onCambiar={(v) => cambiar('evento', v)}
          opciones={EVENTOS_DE_INGRESO.map((e) => ({ valor: e, etiqueta: t(`consola.ingresos.eventos.${e}`) }))}
        />
        <FiltroMenu
          etiqueta={t('consola.ingresos.filtros.resultado')}
          textoTodos={t('comun.todos')}
          valor={filtros.resultado}
          onCambiar={(v) => cambiar('resultado', v)}
          opciones={[
            { valor: 'exitoso', etiqueta: t('consola.ingresos.resultado.exitoso') },
            { valor: 'fallido', etiqueta: t('consola.ingresos.resultado.fallido') },
          ]}
        />
        {campoFecha('desde')}
        {campoFecha('hasta')}
        {hayFiltrosDeIngresos(filtros) && (
          <Button variant="text" onClick={() => setParametros(new URLSearchParams(), { replace: true })}>
            {t('consola.ingresos.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>
      {errorRango && (
        <Typography variant="body2" role="alert" sx={{ color: 'error.main', mb: 1.5 }}>
          {t('consola.ingresos.rangoInvalido')}
        </Typography>
      )}
      {consulta.isError && !consulta.data && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('consola.ingresos.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell sx={{ width: 190 }}>{t('consola.ingresos.columnas.fecha')}</TableCell>
                <TableCell>{t('consola.ingresos.columnas.operador')}</TableCell>
                <TableCell sx={{ width: 230 }}>{t('consola.ingresos.columnas.evento')}</TableCell>
                <TableCell sx={{ width: 130 }}>{t('consola.ingresos.columnas.resultado')}</TableCell>
                <TableCell sx={{ width: 150 }}>{t('consola.ingresos.columnas.motivo')}</TableCell>
                <TableCell sx={{ width: 140 }}>{t('consola.ingresos.columnas.ip')}</TableCell>
                <TableCell sx={{ width: 170 }}>{t('consola.ingresos.columnas.dispositivo')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={COLUMNAS} alto={44} />}
              {consulta.isSuccess && filas.length === 0 && (
                <TableRow>
                  <TableCell colSpan={COLUMNAS} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={History} titulo={hayFiltrosDeIngresos(filtros) ? t('consola.ingresos.vacioFiltros') : t('consola.ingresos.vacio')} />
                  </TableCell>
                </TableRow>
              )}
              {filas.map((f) => (
                <TableRow key={f.id}>
                  <TableCell sx={{ height: 44 }}>
                    <Typography variant="monoTabla" sx={{ whiteSpace: 'nowrap' }}>
                      {formatearMomento(f.fecha)}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography noWrap sx={{ fontSize: 14 }}>
                      {f.correo ?? '—'}
                    </Typography>
                  </TableCell>
                  <TableCell>{t(`consola.ingresos.eventos.${f.evento as (typeof EVENTOS_DE_INGRESO)[number]}`, { defaultValue: f.evento })}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      variant="outlined"
                      color={f.exitoso ? 'success' : 'error'}
                      icon={f.exitoso ? <CircleCheck size={14} aria-hidden /> : <CircleAlert size={14} aria-hidden />}
                      label={f.exitoso ? t('consola.ingresos.resultado.exitoso') : t('consola.ingresos.resultado.fallido')}
                    />
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{f.motivo ?? '—'}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{f.ip ?? '—'}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography noWrap title={f.dispositivo ?? undefined} sx={{ fontSize: 13 }}>
                      {dispositivoLegible(f.dispositivo)}
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>
      {consulta.hasNextPage && (
        <Box sx={{ display: 'flex', justifyContent: 'center', pt: 2 }}>
          <Button variant="outlined" onClick={() => void consulta.fetchNextPage()} disabled={consulta.isFetchingNextPage} startIcon={consulta.isFetchingNextPage ? <IconoCarga /> : undefined}>
            {t('consola.ingresos.verMas')}
          </Button>
        </Box>
      )}
    </>
  );
}
