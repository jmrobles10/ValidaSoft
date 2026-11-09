import { Box, Checkbox, FormControlLabel, Radio, RadioGroup, Stack, Typography } from '@mui/material';
import type { EmpresaDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { TODO_EL_GRUPO } from './modelo';

/** Empresas de alcance de la llave: todo el grupo (`*`, vacío para la API) o solo algunas. */
export function SelectorAlcance({ valor, empresas, onCambiar }: { valor: readonly string[]; empresas: readonly EmpresaDto[]; onCambiar: (empresas: string[]) => void }) {
  const { t } = useTranslation();
  const todas = valor.includes(TODO_EL_GRUPO);
  return (
    <Box component="fieldset" sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
      <Typography component="legend" sx={{ fontSize: 14, fontWeight: 600 }}>
        {t('apiKeys.campos.alcance')}
      </Typography>
      <RadioGroup value={todas ? 'todas' : 'algunas'} onChange={(e) => onCambiar(e.target.value === 'todas' ? [TODO_EL_GRUPO] : [])}>
        <FormControlLabel value="todas" control={<Radio size="small" />} label={t('apiKeys.campos.todoElGrupo')} />
        <FormControlLabel value="algunas" control={<Radio size="small" />} label={t('apiKeys.campos.algunas')} />
      </RadioGroup>
      {!todas && (
        <Stack sx={{ pl: 3.5 }}>
          {empresas.map((e) => (
            <FormControlLabel
              key={e.id}
              control={<Checkbox size="small" checked={valor.includes(e.id)} onChange={() => onCambiar(valor.includes(e.id) ? valor.filter((x) => x !== e.id) : [...valor, e.id])} />}
              label={e.nombre}
            />
          ))}
        </Stack>
      )}
    </Box>
  );
}
