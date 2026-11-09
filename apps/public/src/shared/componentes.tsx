import { Box, Skeleton, Stack, Typography } from '@mui/material';
import { ErrorEnLinea } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from './errores';

/** Título de página del tercero (20/26 en móvil, 20/28 en escritorio) con su explicación y, a la derecha en escritorio, la acción principal. */
export function CabeceraPagina({ titulo, sub, accion }: { titulo: string; sub?: ReactNode; accion?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ sm: 'flex-end' }} justifyContent="space-between" sx={{ mb: 2 }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="h2" component="h1">
          {titulo}
        </Typography>
        {sub && (
          <Typography color="text.secondary" sx={{ mt: 0.5, fontSize: { xs: 15, sm: 14 }, lineHeight: { xs: '22px', sm: '20px' } }}>
            {sub}
          </Typography>
        )}
      </Box>
      {accion && <Box sx={{ flex: 'none' }}>{accion}</Box>}
    </Stack>
  );
}

/** Esqueleto de tarjetas mientras carga. */
export function EsqueletoTarjetas({ cantidad = 2 }: { cantidad?: number }) {
  const { t } = useTranslation();
  return (
    <Stack spacing={1.5} aria-busy aria-label={t('comun.cargando')}>
      {Array.from({ length: cantidad }, (_, i) => (
        <Skeleton key={i} variant="rounded" height={96} />
      ))}
    </Stack>
  );
}

export function ErrorRecuperable({ error, onReintentar }: { error: unknown; onReintentar: () => void }) {
  const { t } = useTranslation();
  return <ErrorEnLinea mensaje={mensajeDeError(error, t)} onReintentar={onReintentar} textoReintentar={t('comun.reintentar')} />;
}

/**
 * Fila «Empresa · estado · detalle · acción» de una lista con divisores (TarjetaDocumentoBoveda y Mis empresas). En móvil
 * el detalle y la acción van debajo; en escritorio, en la misma línea (empresa 220 px, estado, detalle, acción).
 */
export function FilaEmpresa({ nombre, estado, detalle, accion }: { nombre: string; estado?: ReactNode; detalle?: ReactNode; accion?: ReactNode }) {
  return (
    <Box
      component="li"
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: 'minmax(0, 1fr) auto', sm: '220px max-content minmax(0, 1fr) auto' },
        alignItems: 'center',
        columnGap: { xs: 1, sm: 2 },
        rowGap: 0.25,
        py: { xs: 1, sm: 0.75 },
        px: { xs: 1.5, sm: 2 },
        minHeight: { xs: 44, sm: 40 },
        borderTop: 1,
        borderColor: 'divider',
        listStyle: 'none',
      }}
    >
      <Typography component="span" sx={{ fontWeight: 500, fontSize: 14, lineHeight: '20px', minWidth: 0 }}>
        {nombre}
      </Typography>
      <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, justifySelf: { xs: 'end', sm: 'start' } }}>{estado}</Box>
      {detalle ? (
        <Typography color="text.secondary" sx={{ fontSize: 13, lineHeight: '18px', gridColumn: { xs: '1 / -1', sm: 3 } }}>
          {detalle}
        </Typography>
      ) : (
        <Box sx={{ display: { xs: 'none', sm: 'block' } }} />
      )}
      {accion && <Box sx={{ gridColumn: { xs: '1 / -1', sm: 4 } }}>{accion}</Box>}
    </Box>
  );
}

/** Logo de la empresa o su inicial en un cuadro de 36 px. */
export function LogoEmpresa({ nombre, logoUrl, tamano = 36 }: { nombre: string; logoUrl?: string | null; tamano?: number }) {
  if (logoUrl) return <Box component="img" src={logoUrl} alt="" sx={{ width: tamano, height: tamano, borderRadius: 1, objectFit: 'contain', flex: 'none' }} />;
  return (
    <Box
      aria-hidden
      sx={{
        width: tamano,
        height: tamano,
        borderRadius: 1,
        flex: 'none',
        display: 'grid',
        placeItems: 'center',
        bgcolor: 'superficie.nivel2',
        color: 'text.secondary',
        fontWeight: 600,
        fontSize: 15,
      }}
    >
      {nombre.trim().charAt(0).toUpperCase()}
    </Box>
  );
}
