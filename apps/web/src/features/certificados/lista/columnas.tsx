import { Typography } from '@mui/material';
import type { FilaCertificadoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha, type ColumnaTabla } from '@validasoft/ui';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { formatearFechaDeclarada } from '../../documentos/formatoDatos';
import { TerceroEnLinea } from '../../expedientes/componentes';
import { esEstadoCertificado } from '../api';
import { fechaDelEstado } from './modelo';

/** Celda del estado: el chip canónico (bloqueante en tono error) o, si la API trae uno desconocido, su texto. */
function celdaEstado(fila: FilaCertificadoDto) {
  if (!esEstadoCertificado(fila.estado)) return <Typography variant="body2">{fila.estado}</Typography>;
  return <ChipEstado estado={fila.estado} fecha={fechaDelEstado(fila)} bloqueante={fila.bloqueante} />;
}

/** Columnas de la lista de certificados (densas: una línea por fila). */
export function useColumnasCertificados(): ColumnaTabla<FilaCertificadoDto>[] {
  const { t } = useTranslation();
  return useMemo<ColumnaTabla<FilaCertificadoDto>[]>(
    () => [
      { id: 'tercero', encabezado: t('certificados.lista.columnas.tercero'), celda: (f) => <TerceroEnLinea tercero={f.tercero} /> },
      { id: 'tipo', encabezado: t('certificados.lista.columnas.tipo'), ancho: 140, celda: (f) => f.tipoVinculo.nombre },
      {
        id: 'folio',
        encabezado: t('certificados.lista.columnas.folio'),
        ancho: 200,
        celda: (f) => <Typography variant="monoTabla" sx={{ whiteSpace: 'nowrap' }}>{f.folio}</Typography>,
      },
      {
        id: 'emision',
        encabezado: t('certificados.lista.columnas.emision'),
        ancho: 80,
        celda: (f) => <Typography variant="monoTabla">{f.emision}</Typography>,
      },
      { id: 'estado', encabezado: t('certificados.lista.columnas.estado'), ancho: 160, celda: celdaEstado },
      {
        id: 'vigenteHasta',
        encabezado: t('certificados.lista.columnas.vigenteHasta'),
        ancho: 110,
        celda: (f) => <Typography variant="monoTabla">{f.vigenteHasta ? formatearFechaDeclarada(f.vigenteHasta) : '—'}</Typography>,
      },
      {
        id: 'emitido',
        encabezado: t('certificados.lista.columnas.emitido'),
        ancho: 110,
        celda: (f) => <Typography variant="monoTabla">{formatearFecha(f.emitidoEn)}</Typography>,
      },
    ],
    [t],
  );
}
