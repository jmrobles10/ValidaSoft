import { Autocomplete, Box, Chip, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';

/** Chips de áreas a la vista en «Lo revisan»; el resto va en un chip «+N». */
const CHIPS_VISIBLES = 2;

export interface SelectorAreasRequisitoProps {
  areasDelFlujo: readonly string[];
  elegidas: readonly string[];
  nombreArea: (id: string) => string;
  onCambiar: (areas: string[]) => void;
  deshabilitado: boolean;
}

/**
 * «Lo revisan» de un requisito (documento o sección): las áreas del flujo que lo revisan; ninguna elegida = todas.
 * Chips en una sola línea (los que no caben se cortan con puntos suspensivos; desde la tercera área, «+N»): el campo
 * mide lo mismo que los demás de la fila en vez de crecer hacia abajo.
 */
export function SelectorAreasRequisito({ areasDelFlujo, elegidas, nombreArea, onCambiar, deshabilitado }: SelectorAreasRequisitoProps) {
  const { t } = useTranslation();
  return (
    <Autocomplete
      multiple
      size="small"
      disableCloseOnSelect
      // Cada chip tiene su ✕: el botón de limpiar todo solo restaba espacio a los chips.
      disableClearable
      options={[...areasDelFlujo]}
      value={elegidas.filter((a) => areasDelFlujo.includes(a))}
      onChange={(_, valor) => onCambiar(valor)}
      getOptionLabel={nombreArea}
      disabled={deshabilitado}
      renderValue={(valor, getItemProps) => {
        const ocultas = valor.slice(CHIPS_VISIBLES);
        return (
          <Box className="chips-areas" sx={{ display: 'flex', alignItems: 'center', flex: '0 1 auto', minWidth: 0, overflow: 'hidden', pl: 0.5 }}>
            {valor.slice(0, CHIPS_VISIBLES).map((id, index) => {
              const { key, ...props } = getItemProps({ index });
              return <Chip key={key} {...props} size="small" label={nombreArea(id)} title={nombreArea(id)} sx={{ minWidth: 0, flexShrink: 1 }} />;
            })}
            {ocultas.length > 0 && (
              <Chip size="small" variant="outlined" label={`+${String(ocultas.length)}`} title={ocultas.map(nombreArea).join(', ')} sx={{ flex: 'none', m: '2px' }} />
            )}
          </Box>
        );
      }}
      renderInput={(params) => (
        <TextField {...params} label={t('flujos.editor.requisitos.areas')} placeholder={elegidas.length === 0 ? t('flujos.editor.requisitos.todas') : undefined} />
      )}
      // MUI limita cada chip a `calc(100% - 6px)` de su contenedor, que aquí es la fila de chips (se encogían sin
      // necesidad): tope fijo, y solo se cortan con puntos suspensivos cuando de verdad no caben.
      // Con chips, el hueco para escribir se achica (se puede seguir escribiendo) para dejarles el espacio.
      sx={{
        minWidth: 0,
        '& .MuiOutlinedInput-root': { flexWrap: 'nowrap' },
        '& .MuiAutocomplete-tag': { maxWidth: 160 },
        '& .chips-areas + .MuiAutocomplete-input': { minWidth: 8, px: 0.5 },
      }}
    />
  );
}
