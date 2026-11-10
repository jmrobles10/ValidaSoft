import { Box, Checkbox, Collapse, FormControlLabel, List, ListItemButton, ListItemText, Typography } from '@mui/material';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { hojas, type Nodo } from './arbol';

interface Props {
  nodos: readonly Nodo[];
  elegidas: readonly string[];
  /** Con búsqueda, todo lo que coincide se ve abierto. */
  abiertoTodo: boolean;
  onAlternar: (codigo: string, marcado: boolean) => void;
  deshabilitado: boolean;
}

/** Árbol grupo → categoría → especialidad: los dos primeros niveles se abren y cierran; el tercero se marca. */
export function ArbolEspecialidades({ nodos, elegidas, abiertoTodo, onAlternar, deshabilitado, nivel = 0 }: Props & { nivel?: number }) {
  return (
    <List dense disablePadding sx={{ pl: nivel === 0 ? 0 : 2 }}>
      {nodos.map((nodo) =>
        nodo.nivel === 3 || nodo.hijos.length === 0 ? (
          <Box component="li" key={nodo.codigo} sx={{ listStyle: 'none', pl: 2 }}>
            <FormControlLabel
              control={<Checkbox checked={elegidas.includes(nodo.codigo)} onChange={(e) => onAlternar(nodo.codigo, e.target.checked)} />}
              label={nodo.nombre}
              disabled={deshabilitado}
              sx={{ minHeight: 44, width: '100%', mr: 0 }}
            />
          </Box>
        ) : (
          <Rama key={nodo.codigo} nodo={nodo} elegidas={elegidas} abiertoTodo={abiertoTodo} onAlternar={onAlternar} deshabilitado={deshabilitado} nivel={nivel} />
        ),
      )}
    </List>
  );
}

function Rama({ nodo, elegidas, abiertoTodo, onAlternar, deshabilitado, nivel }: Omit<Props, 'nodos'> & { nodo: Nodo; nivel: number }) {
  const { t } = useTranslation();
  const [abierto, setAbierto] = useState(false);
  const visible = abiertoTodo || abierto;
  const marcadas = hojas(nodo).filter((h) => elegidas.includes(h.codigo)).length;
  return (
    <Box component="li" sx={{ listStyle: 'none' }}>
      <ListItemButton onClick={() => setAbierto(!visible)} aria-expanded={visible} sx={{ minHeight: 48 }}>
        <Box component="span" aria-hidden sx={{ display: 'inline-flex', mr: 1 }}>
          {visible ? <ChevronDown size={18} /> : <ChevronRight size={18} />}
        </Box>
        <ListItemText primary={nodo.nombre} slotProps={{ primary: { sx: { fontWeight: nivel === 0 ? 600 : 500 } } }} />
        {marcadas > 0 && (
          <Typography component="span" sx={{ color: 'primary.main', fontSize: 13, ml: 1 }}>
            {t('ficha.especialidades.marcadas', { count: marcadas })}
          </Typography>
        )}
      </ListItemButton>
      <Collapse in={visible} unmountOnExit>
        <ArbolEspecialidades nodos={nodo.hijos} elegidas={elegidas} abiertoTodo={abiertoTodo} onAlternar={onAlternar} deshabilitado={deshabilitado} nivel={nivel + 1} />
      </Collapse>
    </Box>
  );
}
