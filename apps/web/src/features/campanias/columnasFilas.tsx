import { Chip, Link, Typography } from '@mui/material';
import type { EstadoFilaCampania, FilaCampaniaDto } from '@validasoft/api-client';
import type { ColumnaTabla } from '@validasoft/ui';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { formatearDia, identificacionLegible, tieneExpediente } from './modelo';

/** Estados por los que filtra la API (una cancelada muestra sus pendientes como «Cancelada»: se ven con «Pendiente»). */
export const ESTADOS_FILA: readonly EstadoFilaCampania[] = ['Pendiente', 'Exitosa', 'Fallida', 'Omitida'];

const TONO_FILA: Record<EstadoFilaCampania, 'default' | 'success' | 'error' | 'warning' | 'info'> = {
  Pendiente: 'info',
  Exitosa: 'success',
  Fallida: 'error',
  Omitida: 'warning',
  Cancelada: 'default',
};

/** Columnas de los vínculos de una campaña: tercero, tipo, vencimiento, estado y qué se hizo (error, motivo o enlace al expediente). */
export function useColumnasFilasCampania(empresaId: string): ColumnaTabla<FilaCampaniaDto>[] {
  const { t } = useTranslation();
  return useMemo(() => {
    const accionDe = (f: FilaCampaniaDto) => (f.accion ? t(`campanias.detalle.acciones.${f.accion as 'RenovacionAbierta' | 'Recordada'}`, { defaultValue: f.accion }) : null);
    return [
      { id: 'n', encabezado: t('campanias.detalle.columnas.fila'), ancho: 64, celda: (f) => <Typography variant="monoTabla">{f.n}</Typography> },
      {
        id: 'tercero',
        encabezado: t('campanias.detalle.columnas.tercero'),
        celda: (f) => (
          <>
            <Link component={RouterLink} to={`/e/${empresaId}/vinculos/${f.vinculoId}`} onClick={(e) => e.stopPropagation()} sx={{ display: 'block' }}>
              {f.tercero}
            </Link>
            <Typography component="span" variant="caption" sx={{ color: 'text.secondary' }}>
              {identificacionLegible(f.identificacion)}
            </Typography>
          </>
        ),
      },
      { id: 'tipo', encabezado: t('campanias.detalle.columnas.tipo'), ancho: 150, celda: (f) => f.tipoVinculo },
      { id: 'vence', encabezado: t('campanias.detalle.columnas.vence'), ancho: 110, celda: (f) => <Typography variant="monoTabla">{formatearDia(f.venceEn)}</Typography> },
      {
        id: 'estado',
        encabezado: t('campanias.detalle.columnas.estado'),
        ancho: 120,
        celda: (f) => <Chip size="small" variant="outlined" color={TONO_FILA[f.estado]} label={t(`campanias.detalle.estadosFila.${f.estado}`)} />,
      },
      {
        id: 'detalle',
        encabezado: t('campanias.detalle.columnas.detalle'),
        celda: (f) =>
          f.errores.length > 0 ? (
            <Typography component="span" variant="body2" sx={{ color: 'estado.error.texto' }}>
              {f.errores.map((e) => e.mensaje).join(' ')}
            </Typography>
          ) : f.accion && tieneExpediente(f) ? (
            <Link component={RouterLink} to={`/e/${empresaId}/expedientes/${f.expedienteId ?? ''}`} onClick={(e) => e.stopPropagation()}>
              {`${accionDe(f) ?? ''} · ${f.folio ?? ''}`}
            </Link>
          ) : (
            (accionDe(f) ?? f.detalle ?? '—')
          ),
      },
    ];
  }, [t, empresaId]);
}
