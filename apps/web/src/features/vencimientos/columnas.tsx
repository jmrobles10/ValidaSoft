import { Box, Checkbox, Tooltip, Typography } from '@mui/material';
import type { VencimientoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha, type ColumnaTabla } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { RefreshCw } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { formatearFechaDeclarada } from '../documentos/formatoDatos';
import { CHIP_VIGENCIA } from '../documentos/vigencia';
import { TerceroEnLinea } from '../expedientes/componentes';
import { baseVencimiento, claveVencimiento, COLOR_TONO, estadoVencimiento, textoDias, tonoVencimiento } from './modelo';

interface Opciones {
  /** Con `vinculos:escribir` se muestra la casilla para recordar. */
  conSeleccion: boolean;
  seleccion: ReadonlySet<string>;
  alternar: (fila: VencimientoDto) => void;
}

/** Celda del estado: el chip canónico de la vigencia; en gracia, el tooltip dice hasta cuándo se acepta. */
function celdaEstado(fila: VencimientoDto, t: TFunction) {
  const estado = estadoVencimiento(fila);
  if (!estado) return <Typography variant="body2">{fila.estado}</Typography>;
  const chip = <ChipEstado estado={CHIP_VIGENCIA[estado]} etiqueta={t(`vencimientos.estados.${estado}`)} />;
  if (estado !== 'EnGracia' || !fila.finDeGracia) return chip;
  return (
    <Tooltip title={t('vencimientos.enGraciaHasta', { fecha: formatearFechaDeclarada(fila.finDeGracia) })}>
      <Box component="span" sx={{ display: 'inline-flex' }}>
        {chip}
      </Box>
    </Tooltip>
  );
}

/** Columnas de la tabla de vencimientos (densas: una línea por fila). */
export function useColumnasVencimientos({ conSeleccion, seleccion, alternar }: Opciones): ColumnaTabla<VencimientoDto>[] {
  const { t } = useTranslation();
  return useMemo<ColumnaTabla<VencimientoDto>[]>(
    () => [
      ...(conSeleccion
        ? [
            {
              id: 'seleccion',
              encabezado: t('vencimientos.columnas.seleccion'),
              encabezadoOculto: true,
              ancho: 44,
              celda: (f: VencimientoDto) => (
                <Checkbox
                  size="small"
                  checked={seleccion.has(claveVencimiento(f))}
                  onClick={(e) => e.stopPropagation()}
                  onChange={() => alternar(f)}
                  slotProps={{ input: { 'aria-label': t('vencimientos.seleccionar', { documento: f.documento, tercero: f.tercero.nombre }) } }}
                  sx={{ p: 0.5 }}
                />
              ),
            },
          ]
        : []),
      { id: 'tercero', encabezado: t('vencimientos.columnas.tercero'), celda: (f) => <TerceroEnLinea tercero={f.tercero} /> },
      {
        id: 'documento',
        encabezado: t('vencimientos.columnas.documento'),
        ancho: 220,
        // Con muchas columnas la tabla encoge la que puede partirse: el nombre del documento no baja de dos líneas.
        celda: (f) => <Box component="span" sx={{ display: 'inline-block', minWidth: 200 }}>{f.documento}</Box>,
      },
      {
        id: 'version',
        encabezado: t('vencimientos.columnas.version'),
        ancho: 72,
        celda: (f) => <Typography variant="monoTabla">{t('vencimientos.version', { numero: String(f.version) })}</Typography>,
      },
      {
        id: 'vigenteHasta',
        encabezado: t('vencimientos.columnas.vigenteHasta'),
        ancho: 110,
        celda: (f) => <Typography variant="monoTabla">{formatearFechaDeclarada(f.vigenteHasta)}</Typography>,
      },
      { id: 'estado', encabezado: t('vencimientos.columnas.estado'), ancho: 130, celda: (f) => celdaEstado(f, t) },
      {
        id: 'dias',
        encabezado: t('vencimientos.columnas.dias'),
        ancho: 150,
        celda: (f) => (
          <Typography component="span" variant="body2" color={COLOR_TONO[tonoVencimiento(f)]} sx={{ whiteSpace: 'nowrap' }}>
            {textoDias(f.diasRestantes, t)}
          </Typography>
        ),
      },
      {
        id: 'base',
        encabezado: t('vencimientos.columnas.base'),
        ancho: 160,
        celda: (f) => {
          const base = baseVencimiento(f);
          return (
            <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
              {base ? t(`vencimientos.bases.${base}`) : f.base}
            </Typography>
          );
        },
      },
      {
        id: 'ratificable',
        encabezado: t('vencimientos.columnas.ratificable'),
        ancho: 100,
        celda: (f) =>
          f.permiteRatificar ? (
            <Tooltip title={t('vencimientos.ratificableAyuda')}>
              <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, color: 'text.secondary' }}>
                <RefreshCw size={14} strokeWidth={1.75} aria-hidden />
                <Typography component="span" variant="body2" sx={{ color: 'text.primary' }}>
                  {t('vencimientos.ratificable')}
                </Typography>
              </Box>
            </Tooltip>
          ) : null,
      },
      {
        id: 'ultimoRecordatorio',
        encabezado: t('vencimientos.columnas.ultimoRecordatorio'),
        ancho: 150,
        celda: (f) => <Typography variant="monoTabla">{f.ultimoRecordatorio ? formatearFecha(f.ultimoRecordatorio) : t('vencimientos.nunca')}</Typography>,
      },
    ],
    [t, conSeleccion, seleccion, alternar],
  );
}
