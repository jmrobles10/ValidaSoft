import { Box, Button, Typography } from '@mui/material';
import { ChevronLeft, ChevronRight } from 'lucide-react';

export interface PaginacionCursorProps {
  pagina: number;
  hayAnterior: boolean;
  haySiguiente: boolean;
  onAnterior: () => void;
  onSiguiente: () => void;
  cargando?: boolean;
}

/** Paginación por cursor: solo "Anterior", "Siguiente" y "Página n"; nunca total de páginas ni saltos. */
export function PaginacionCursor({ pagina, hayAnterior, haySiguiente, onAnterior, onSiguiente, cargando = false }: PaginacionCursorProps) {
  return (
    <Box component="nav" aria-label="Paginación" sx={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 0.5, py: 1 }}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mr: 1 }}>
        Página {pagina}
      </Typography>
      <Button variant="text" startIcon={<ChevronLeft size={16} aria-hidden />} disabled={!hayAnterior || cargando} onClick={onAnterior}>
        Anterior
      </Button>
      <Button variant="text" endIcon={<ChevronRight size={16} aria-hidden />} disabled={!haySiguiente || cargando} onClick={onSiguiente}>
        Siguiente
      </Button>
    </Box>
  );
}
