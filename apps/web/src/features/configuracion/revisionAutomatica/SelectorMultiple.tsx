import { Box, Checkbox, Chip, ListItemText, MenuItem, TextField, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** Chips a la vista en el campo; el resto va en un chip «+N» (el campo no crece hacia abajo). */
const CHIPS_VISIBLES = 2;
const TODOS = '__todos__';

export interface OpcionMultiple {
  valor: string;
  etiqueta: string;
}

export interface SelectorMultipleProps {
  etiqueta: string;
  placeholder: string;
  opciones: readonly OpcionMultiple[];
  elegidos: readonly string[];
  onCambiar: (valores: string[]) => void;
  error?: string | null;
  ayuda?: ReactNode;
  deshabilitado?: boolean;
}

/**
 * Selector de varias opciones con su flecha: el menú lleva casillas y un «Seleccionar todos»; el campo muestra las
 * primeras elegidas como chips (y «+N» con el resto), «Todos (n)» si están todas, o el texto que invita a elegir.
 */
export function SelectorMultiple({ etiqueta, placeholder, opciones, elegidos, onCambiar, error, ayuda, deshabilitado = false }: SelectorMultipleProps) {
  const { t } = useTranslation();
  const nombre = (valor: string) => opciones.find((o) => o.valor === valor)?.etiqueta ?? valor;
  const todos = opciones.length > 0 && opciones.every((o) => elegidos.includes(o.valor));

  const mostrar = (seleccion: unknown): ReactNode => {
    const valores = seleccion as string[];
    if (valores.length === 0) return <Typography component="span" sx={{ color: 'text.secondary' }}>{placeholder}</Typography>;
    if (todos && valores.length > CHIPS_VISIBLES) return t('revisionAutomatica.config.alcance.todosElegidos', { count: valores.length });
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, minWidth: 0, overflow: 'hidden' }}>
        {valores.slice(0, CHIPS_VISIBLES).map((v) => (
          <Chip key={v} size="small" label={nombre(v)} title={nombre(v)} sx={{ minWidth: 0, maxWidth: 200, flexShrink: 1 }} />
        ))}
        {valores.length > CHIPS_VISIBLES && (
          <Chip
            size="small"
            variant="outlined"
            label={t('revisionAutomatica.config.alcance.masElegidos', { count: valores.length - CHIPS_VISIBLES })}
            title={valores.slice(CHIPS_VISIBLES).map(nombre).join(', ')}
            sx={{ flex: 'none' }}
          />
        )}
      </Box>
    );
  };

  return (
    <TextField
      select
      fullWidth
      size="small"
      label={etiqueta}
      value={[...elegidos]}
      disabled={deshabilitado}
      error={Boolean(error)}
      helperText={error ?? ayuda}
      onChange={(evento) => {
        const valor: unknown = evento.target.value;
        const lista = typeof valor === 'string' ? valor.split(',') : (valor as string[]);
        // «Seleccionar todos» alterna: todos si faltaba alguno, ninguno si ya estaban todos.
        onCambiar(lista.includes(TODOS) ? (todos ? [] : opciones.map((o) => o.valor)) : lista);
      }}
      slotProps={{
        select: { multiple: true, displayEmpty: true, renderValue: mostrar, MenuProps: { slotProps: { paper: { sx: { maxHeight: 340 } } } } },
        inputLabel: { shrink: true },
      }}
    >
      <MenuItem value={TODOS} divider>
        <Checkbox size="small" checked={todos} indeterminate={!todos && elegidos.length > 0} tabIndex={-1} disableRipple />
        <ListItemText primary={t('revisionAutomatica.config.alcance.todos')} />
      </MenuItem>
      {opciones.map((opcion) => (
        <MenuItem key={opcion.valor} value={opcion.valor}>
          <Checkbox size="small" checked={elegidos.includes(opcion.valor)} tabIndex={-1} disableRipple />
          <ListItemText primary={opcion.etiqueta} />
        </MenuItem>
      ))}
    </TextField>
  );
}
