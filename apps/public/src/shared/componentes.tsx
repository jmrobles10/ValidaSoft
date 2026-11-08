import { Box, Skeleton, Stack, Typography } from '@mui/material';
import { ErrorEnLinea } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from './errores';

/** Título de página del tercero (22/28) con su explicación y, a la derecha en escritorio, la acción principal. */
export function CabeceraPagina({ titulo, sub, accion }: { titulo: string; sub?: ReactNode; accion?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ sm: 'flex-end' }} justifyContent="space-between" sx={{ mb: 2.5 }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="h2" component="h1">
          {titulo}
        </Typography>
        {sub && (
          <Typography color="text.secondary" sx={{ mt: 0.5, fontSize: 15, lineHeight: '22px' }}>
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
        <Skeleton key={i} variant="rounded" height={132} />
      ))}
    </Stack>
  );
}

export function ErrorRecuperable({ error, onReintentar }: { error: unknown; onReintentar: () => void }) {
  const { t } = useTranslation();
  return <ErrorEnLinea mensaje={mensajeDeError(error, t)} onReintentar={onReintentar} textoReintentar={t('comun.reintentar')} />;
}

/** Fila "Empresa · estado · detalle" dentro de una tarjeta (TarjetaDocumentoBoveda y Mis empresas). */
export function FilaEmpresa({ nombre, estado, detalle, accion }: { nombre: string; estado?: ReactNode; detalle?: ReactNode; accion?: ReactNode }) {
  return (
    <Box component="li" sx={{ py: 1.25, borderTop: 1, borderColor: 'divider', listStyle: 'none' }}>
      <Stack direction="row" spacing={1} alignItems="center" useFlexGap flexWrap="wrap">
        <Typography component="span" sx={{ fontWeight: 500, fontSize: 15, flex: '1 1 auto', minWidth: 0 }}>
          {nombre}
        </Typography>
        {estado}
      </Stack>
      {detalle && (
        <Typography color="text.secondary" sx={{ fontSize: 14, lineHeight: '20px', mt: 0.5 }}>
          {detalle}
        </Typography>
      )}
      {accion && <Box sx={{ mt: 1 }}>{accion}</Box>}
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
