import { Chip, Typography } from '@mui/material';
import type { RegistroAuditoriaDto } from '@validasoft/api-client';
import type { ColumnaTabla } from '@validasoft/ui';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { formatearMomento, textoAccion, textoActor, textoEntidad } from './modelo';

/** Columnas de la auditoría (una línea por fila): cuándo, quién, sobre qué, qué hizo y a quién alcanza. */
export function useColumnasAuditoria(): ColumnaTabla<RegistroAuditoriaDto>[] {
  const { t } = useTranslation();
  return useMemo<ColumnaTabla<RegistroAuditoriaDto>[]>(
    () => [
      {
        id: 'fecha',
        encabezado: t('auditoria.columnas.fecha'),
        ancho: 190,
        celda: (r) => <Typography variant="monoTabla" sx={{ whiteSpace: 'nowrap' }}>{formatearMomento(r.fecha)}</Typography>,
      },
      {
        id: 'actor',
        encabezado: t('auditoria.columnas.actor'),
        celda: (r) => (
          <>
            {textoActor(r.actor, t)}
            {r.actor.sistema && (
              <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                {` · ${t('auditoria.actores.desdeSistema', { sistema: r.actor.sistema })}`}
              </Typography>
            )}
          </>
        ),
      },
      {
        id: 'entidad',
        encabezado: t('auditoria.columnas.entidad'),
        celda: (r) => (
          <>
            {textoEntidad(r.entidad.tipo, t)}
            {r.entidad.nombre && (
              <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                {` · ${r.entidad.nombre}`}
              </Typography>
            )}
          </>
        ),
      },
      {
        id: 'accion',
        encabezado: t('auditoria.columnas.accion'),
        ancho: 130,
        celda: (r) => <Chip size="small" variant="outlined" label={textoAccion(r.accion, t)} />,
      },
      {
        id: 'alcance',
        encabezado: t('auditoria.columnas.alcance'),
        ancho: 130,
        celda: (r) => (r.empresaId === null ? t('auditoria.alcance.grupo') : t('auditoria.alcance.empresa')),
      },
    ],
    [t],
  );
}
