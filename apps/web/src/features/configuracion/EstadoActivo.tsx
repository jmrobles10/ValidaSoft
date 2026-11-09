import { Box } from '@mui/material';
import { Ban, CircleCheck, type LucideIcon } from 'lucide-react';

/**
 * Estado de un registro de configuración (activo, inactivo, vencido…): texto con ícono. No es un estado canónico del
 * dominio, así que no usa ChipEstado; el tono va solo en el ícono.
 */
export function EstadoActivo({ activo, texto, icono }: { activo: boolean; texto: string; icono?: LucideIcon }) {
  const Icono = icono ?? (activo ? CircleCheck : Ban);
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 13, whiteSpace: 'nowrap', '& svg': { color: activo ? 'estado.exito.texto' : 'text.secondary' } }}>
      <Icono size={14} strokeWidth={1.75} aria-hidden />
      {texto}
    </Box>
  );
}
