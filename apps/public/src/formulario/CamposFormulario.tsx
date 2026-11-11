import { Box, FormControl, FormControlLabel, FormHelperText, FormLabel, MenuItem, Radio, RadioGroup, TextField } from '@mui/material';
import type { CampoFormularioDto } from '@validasoft/api-client';
import { CampoFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { MAXIMO_TEXTO, tipoDeCampo } from './formulario';
import type { EstadoFormulario } from './useFormulario';

/**
 * Los campos que configuró la empresa, en su orden, con su tipo, si son obligatorios (*) y su ayuda. Las fechas y los números
 * son campos cortos: van de a dos por fila (UI-8); el texto, las listas y los sí/no ocupan la fila.
 */
export function CamposFormulario({ campos, formulario, deshabilitado = false }: { campos: readonly CampoFormularioDto[]; formulario: EstadoFormulario; deshabilitado?: boolean }) {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 2 }}>
      {campos.map((campo) => (
        <CampoDelFormulario
          key={campo.clave}
          campo={campo}
          valor={formulario.valores[campo.clave] ?? ''}
          error={formulario.mensajeDe(campo.clave)}
          onCambiar={(valor) => formulario.cambiar(campo.clave, valor)}
          onTocar={() => formulario.tocar(campo.clave)}
          deshabilitado={deshabilitado}
        />
      ))}
    </Box>
  );
}

interface PropsCampo {
  campo: CampoFormularioDto;
  valor: string;
  error: string | null;
  onCambiar: (valor: string) => void;
  onTocar: () => void;
  deshabilitado: boolean;
}

/** Un campo: texto, número, fecha (`aaaa-mm-dd`), lista de opciones o sí/no. */
function CampoDelFormulario({ campo, valor, error, onCambiar, onTocar, deshabilitado }: PropsCampo) {
  const { t } = useTranslation();
  const tipo = tipoDeCampo(campo);
  const id = `campo-${campo.clave}`;
  const ayuda = error ?? campo.ayuda ?? (tipo === 'Numero' ? t('formulario.numeroAyuda') : null);
  const fila = tipo === 'Fecha' || tipo === 'Numero' ? undefined : '1 / -1';

  if (tipo === 'SiNo') {
    return (
      <FormControl error={Boolean(error)} required={campo.obligatorio} disabled={deshabilitado} sx={{ gridColumn: fila }}>
        <FormLabel id={`${id}-etiqueta`}>{campo.etiqueta}</FormLabel>
        <RadioGroup
          row
          name={id}
          aria-labelledby={`${id}-etiqueta`}
          aria-describedby={ayuda ? `${id}-ayuda` : undefined}
          value={valor}
          onChange={(e) => {
            onCambiar(e.target.value);
            onTocar();
          }}
        >
          <FormControlLabel value="true" control={<Radio />} label={t('formulario.si')} />
          <FormControlLabel value="false" control={<Radio />} label={t('formulario.no')} />
        </RadioGroup>
        {ayuda && <FormHelperText id={`${id}-ayuda`}>{ayuda}</FormHelperText>}
      </FormControl>
    );
  }

  if (tipo === 'Fecha') {
    return (
      <CampoFecha
        id={id}
        etiqueta={campo.etiqueta}
        requerido={campo.obligatorio}
        valor={valor}
        onCambiar={onCambiar}
        onBlur={onTocar}
        error={Boolean(error)}
        ayuda={ayuda}
        deshabilitado={deshabilitado}
        fullWidth
      />
    );
  }

  return (
    <TextField
      id={id}
      label={campo.etiqueta}
      required={campo.obligatorio}
      value={valor}
      onChange={(e) => onCambiar(e.target.value)}
      onBlur={tipo === 'Lista' ? undefined : onTocar}
      error={Boolean(error)}
      helperText={ayuda}
      disabled={deshabilitado}
      fullWidth
      select={tipo === 'Lista'}
      sx={{ gridColumn: fila }}
      slotProps={{
        htmlInput: tipo === 'Texto' ? { maxLength: MAXIMO_TEXTO } : tipo === 'Numero' ? { inputMode: 'decimal' } : undefined,
        // En una lista, «tocado» es cerrar el menú (al abrirlo el campo pierde el foco antes de elegir).
        select: tipo === 'Lista' ? { onClose: onTocar } : undefined,
      }}
    >
      {tipo === 'Lista' &&
        (campo.opciones ?? []).map((opcion) => (
          <MenuItem key={opcion} value={opcion}>
            {opcion}
          </MenuItem>
        ))}
    </TextField>
  );
}
