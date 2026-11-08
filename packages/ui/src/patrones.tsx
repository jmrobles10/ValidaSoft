import { Alert, Box, Button, Stack, Typography } from '@mui/material';
import { Inbox, RotateCcw, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

function CirculoIcono({ icono: Icono, grande = false }: { icono: LucideIcon; grande?: boolean }) {
  const lado = grande ? 64 : 48;
  return (
    <Box
      aria-hidden
      sx={{ display: 'grid', placeItems: 'center', width: lado, height: lado, mb: 0.75, borderRadius: '50%', bgcolor: 'superficie.nivel2', color: 'text.secondary' }}
    >
      <Icono size={grande ? 28 : 24} strokeWidth={1.75} />
    </Box>
  );
}

export interface EstadoVacioProps {
  titulo: string;
  texto?: ReactNode;
  icono?: LucideIcon;
  accion?: ReactNode;
  /** Dentro de una tabla o tarjeta (menos aire). */
  compacto?: boolean;
}

/** Vacío: ícono en círculo, frase que dice qué falta y dónde aparecerá, y una acción. Dentro del área del contenido. */
export function EstadoVacio({ titulo, texto, icono = Inbox, accion, compacto = false }: EstadoVacioProps) {
  return (
    <Stack alignItems="center" spacing={0.75} sx={{ py: compacto ? 3 : 6, px: 3, textAlign: 'center' }}>
      <CirculoIcono icono={icono} />
      <Typography sx={{ fontSize: 15, lineHeight: '22px', fontWeight: 600 }}>{titulo}</Typography>
      {texto && (
        <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 380 }}>
          {texto}
        </Typography>
      )}
      {accion && <Box sx={{ pt: 1 }}>{accion}</Box>}
    </Stack>
  );
}

export interface PaginaEstadoProps {
  icono: LucideIcon;
  titulo: string;
  texto: ReactNode;
  acciones?: ReactNode;
  /** `role` del contenedor: `status` para esperas y cierres, `alert` para lo que interrumpe. */
  rol?: 'status' | 'alert';
}

/** Página de estado (sesión vencida o cerrada, 403, 404): ícono grande, título, explicación y siguiente paso. */
export function PaginaEstado({ icono, titulo, texto, acciones, rol }: PaginaEstadoProps) {
  return (
    <Stack component="section" role={rol} alignItems="center" spacing={1} sx={{ maxWidth: 520, mx: 'auto', px: 3, py: 8, textAlign: 'center' }}>
      <CirculoIcono icono={icono} grande />
      <Typography variant="h2" component="h1">
        {titulo}
      </Typography>
      <Typography variant="body1" color="text.secondary">
        {texto}
      </Typography>
      {acciones && (
        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" justifyContent="center" sx={{ pt: 1.5 }}>
          {acciones}
        </Stack>
      )}
    </Stack>
  );
}

/** Error recuperable: inline en el bloque que falló, con "Reintentar"; el resto de la pantalla sigue usable. */
export function ErrorEnLinea({ mensaje, onReintentar, textoReintentar = 'Reintentar' }: { mensaje: ReactNode; onReintentar?: () => void; textoReintentar?: string }) {
  return (
    <Alert
      severity="error"
      action={
        onReintentar ? (
          <Button variant="outlined" startIcon={<RotateCcw size={16} aria-hidden />} onClick={onReintentar}>
            {textoReintentar}
          </Button>
        ) : undefined
      }
      sx={{ alignItems: 'center', '& .MuiAlert-action': { pt: 0 } }}
    >
      {mensaje}
    </Alert>
  );
}
