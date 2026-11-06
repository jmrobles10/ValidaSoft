import { IconButton, ListItemIcon, ListItemText, Menu, MenuItem, Tooltip } from '@mui/material';
import { Ellipsis, type LucideIcon } from 'lucide-react';
import { useState } from 'react';

export interface AccionFila {
  etiqueta: string;
  icono: LucideIcon;
  onClick: () => void;
  destructiva?: boolean;
}

/** Acciones de una fila tras "…": el `aria-label` nombra la fila ("Acciones de Olga Oficial"). */
export function MenuFila({ etiqueta, acciones }: { etiqueta: string; acciones: readonly AccionFila[] }) {
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  if (acciones.length === 0) return null;
  return (
    <>
      <Tooltip title={etiqueta}>
        <IconButton
          aria-label={etiqueta}
          aria-haspopup="menu"
          onClick={(e) => {
            e.stopPropagation();
            setAncla(e.currentTarget);
          }}
        >
          <Ellipsis size={16} strokeWidth={1.75} />
        </IconButton>
      </Tooltip>
      <Menu
        anchorEl={ancla}
        open={Boolean(ancla)}
        onClose={() => setAncla(null)}
        onClick={(e) => e.stopPropagation()}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {acciones.map(({ etiqueta: texto, icono: Icono, onClick, destructiva }) => (
          <MenuItem
            key={texto}
            onClick={() => {
              setAncla(null);
              onClick();
            }}
            sx={destructiva ? { color: 'estado.error.texto', '& .MuiListItemIcon-root': { color: 'estado.error.texto' } } : undefined}
          >
            <ListItemIcon>
              <Icono size={16} strokeWidth={1.75} aria-hidden />
            </ListItemIcon>
            <ListItemText primary={texto} />
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
