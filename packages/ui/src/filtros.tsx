import { Box, ButtonBase, InputBase, ListItemText, Menu, MenuItem } from '@mui/material';
import { Check, ChevronDown, Search } from 'lucide-react';
import { useId, useState, type FormEvent, type ReactNode } from 'react';

const ALTO_CONTROL = 32;

export interface CampoBusquedaProps {
  valor: string;
  onCambiar: (valor: string) => void;
  /** Enter o el envío del formulario. */
  onBuscar?: (valor: string) => void;
  placeholder: string;
  ancho?: number | string;
}

/** Búsqueda de la barra de filtros: ícono, borde de control y foco visible. */
export function CampoBusqueda({ valor, onCambiar, onBuscar, placeholder, ancho = 320 }: CampoBusquedaProps) {
  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    onBuscar?.(valor.trim());
  };
  return (
    <Box
      component="form"
      role="search"
      onSubmit={enviar}
      sx={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 1,
        width: { xs: '100%', sm: ancho },
        height: ALTO_CONTROL,
        px: 1.25,
        border: 1,
        borderColor: 'borde.control',
        borderRadius: (t) => `${String(t.radio.sm)}px`,
        bgcolor: 'superficie.nivel1',
        color: 'text.secondary',
        '&:focus-within': { borderColor: (t) => t.palette.acento.main, boxShadow: (t) => `0 0 0 1px ${t.palette.acento.main}` },
      }}
    >
      <Search size={16} strokeWidth={1.75} aria-hidden />
      <InputBase
        type="search"
        value={valor}
        onChange={(e) => onCambiar(e.target.value)}
        placeholder={placeholder}
        inputProps={{ 'aria-label': placeholder }}
        sx={{ flex: 1, fontSize: 14, '& input': { p: 0, '&::placeholder': { color: 'text.secondary', opacity: 1 } } }}
      />
    </Box>
  );
}

export interface OpcionFiltro {
  valor: string;
  etiqueta: string;
  /** Contenido de la opción en el menú (p. ej. un ChipEstado); por defecto la etiqueta. */
  contenido?: ReactNode;
}

export interface FiltroMenuProps {
  etiqueta: string;
  /** Valor activo; `''` = sin filtro. */
  valor: string;
  opciones: readonly OpcionFiltro[];
  onCambiar: (valor: string) => void;
  /** Texto cuando no hay filtro ("Todos"). */
  textoTodos: string;
}

/**
 * Filtro de la barra: botón "Estado · Todos ▾" que abre un menú. Activo, se tiñe con el acento y el valor va en
 * negrita, así se ve qué filtra la tabla sin abrirlo.
 */
export function FiltroMenu({ etiqueta, valor, opciones, onCambiar, textoTodos }: FiltroMenuProps) {
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const idMenu = useId();
  const activo = valor !== '';
  const elegida = opciones.find((o) => o.valor === valor);
  const elegir = (nuevo: string) => {
    setAncla(null);
    if (nuevo !== valor) onCambiar(nuevo);
  };

  return (
    <>
      <ButtonBase
        aria-haspopup="menu"
        aria-expanded={ancla ? 'true' : undefined}
        aria-controls={ancla ? idMenu : undefined}
        onClick={(e) => setAncla(e.currentTarget)}
        sx={{
          gap: 0.75,
          height: ALTO_CONTROL,
          pl: 1.25,
          pr: 1,
          border: 1,
          borderColor: activo ? 'acento.main' : 'borde.control',
          borderRadius: (t) => `${String(t.radio.sm)}px`,
          bgcolor: activo ? 'acento.tinte' : 'superficie.nivel1',
          color: 'text.primary',
          fontSize: 14,
          whiteSpace: 'nowrap',
        }}
      >
        <Box component="span" sx={{ color: activo ? 'text.primary' : 'text.secondary' }}>
          {etiqueta}
        </Box>
        <Box component="span" sx={{ fontWeight: 600, color: activo ? 'acento.main' : 'text.primary', maxWidth: 160, overflow: 'hidden', textOverflow: 'ellipsis' }}>
          {elegida?.etiqueta ?? textoTodos}
        </Box>
        <ChevronDown size={14} strokeWidth={1.75} aria-hidden />
      </ButtonBase>
      <Menu id={idMenu} anchorEl={ancla} open={Boolean(ancla)} onClose={() => setAncla(null)} slotProps={{ paper: { sx: { minWidth: 200 } } }}>
        {[{ valor: '', etiqueta: textoTodos } as OpcionFiltro, ...opciones].map((o) => (
          <MenuItem key={o.valor || '__todos'} selected={o.valor === valor} onClick={() => elegir(o.valor)}>
            <ListItemText primary={o.contenido ?? o.etiqueta} />
            {o.valor === valor && <Check size={16} strokeWidth={1.75} aria-hidden />}
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}

/** Barra de filtros: búsqueda + filtros a la izquierda, acciones a la derecha. */
export function BarraFiltros({ children, derecha }: { children: ReactNode; derecha?: ReactNode }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mb: 1.5 }}>
      {children}
      <Box sx={{ flex: 1 }} />
      {derecha}
    </Box>
  );
}
