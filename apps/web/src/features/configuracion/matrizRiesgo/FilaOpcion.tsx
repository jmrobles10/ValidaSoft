import { Box, IconButton, MenuItem, TextField } from '@mui/material';
import { visualmenteOculto } from '@validasoft/ui';
import { Trash2 } from 'lucide-react';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { COLUMNAS_OPCION } from './estilos';
import type { BorradorOpcion, CambioOpcion, ErrorMatriz, Piso } from './modelo';

/** Entero de una opción (puntaje) sin etiqueta propia: la etiqueta va en la cabecera de la columna y en `aria-label`. */
export function CampoDigitos({ valor, onCambiar, etiqueta, error, deshabilitado, maxDigitos = 3, ancho }: { valor: string; onCambiar: (texto: string) => void; etiqueta: string; error?: boolean; deshabilitado?: boolean; maxDigitos?: number; ancho?: number }) {
  return (
    <TextField
      value={valor}
      onChange={(e) => onCambiar(e.target.value.replace(/\D/g, '').slice(0, maxDigitos))}
      error={error}
      disabled={deshabilitado}
      sx={ancho ? { width: ancho } : undefined}
      slotProps={{ htmlInput: { inputMode: 'numeric', pattern: '[0-9]*', autoComplete: 'off', maxLength: maxDigitos, 'aria-label': etiqueta } }}
    />
  );
}

export interface FilaOpcionProps {
  opcion: BorradorOpcion;
  errores: readonly ErrorMatriz[];
  /** Los factores automáticos conservan sus opciones: no se quitan. */
  puedeQuitar: boolean;
  deshabilitado: boolean;
  onCambiar: (cambio: CambioOpcion) => void;
  onQuitar: () => void;
}

/** Una opción de un factor: su nombre, su puntaje (0–100) y el piso de nivel que fija al elegirla. */
export function FilaOpcion({ opcion: o, errores, puedeQuitar, deshabilitado, onCambiar, onQuitar }: FilaOpcionProps) {
  const { t } = useTranslation();
  const idPiso = useId();
  const nombre = o.nombre.trim() || t('matrizRiesgo.factores.sinOpcion');
  const mensajes = errores.map((e) => t(`matrizRiesgo.validacion.${e.codigo}`));
  const conError = (codigos: readonly string[]) => errores.some((e) => codigos.includes(e.codigo));

  return (
    <Box>
      <Box sx={{ display: 'grid', gridTemplateColumns: COLUMNAS_OPCION, gap: 1, alignItems: 'start' }}>
        <TextField
          value={o.nombre}
          onChange={(e) => onCambiar({ nombre: e.target.value })}
          disabled={deshabilitado}
          error={conError(['nombre-opcion', 'codigo-opcion'])}
          sx={{ gridColumn: { xs: '1 / -1', sm: 'auto' } }}
          slotProps={{ htmlInput: { maxLength: 160, 'aria-label': t('matrizRiesgo.factores.opcionNombre') } }}
        />
        <CampoDigitos valor={o.puntaje} onCambiar={(puntaje) => onCambiar({ puntaje })} etiqueta={t('matrizRiesgo.factores.opcionPuntaje', { opcion: nombre })} error={conError(['puntaje'])} deshabilitado={deshabilitado} />
        <Box component="span" id={idPiso} sx={visualmenteOculto}>
          {t('matrizRiesgo.factores.opcionPiso', { opcion: nombre })}
        </Box>
        <TextField
          select
          value={o.piso}
          onChange={(e) => onCambiar({ piso: e.target.value as Piso })}
          disabled={deshabilitado}
          slotProps={{ select: { displayEmpty: true, labelId: idPiso } }}
        >
          <MenuItem value="">{t('matrizRiesgo.factores.sinPiso')}</MenuItem>
          <MenuItem value="2">{t('matrizRiesgo.factores.pisos.2')}</MenuItem>
          <MenuItem value="3">{t('matrizRiesgo.factores.pisos.3')}</MenuItem>
        </TextField>
        {puedeQuitar && !deshabilitado ? (
          <IconButton size="small" aria-label={t('matrizRiesgo.factores.quitarOpcion', { nombre })} onClick={onQuitar} sx={{ mt: '2px' }}>
            <Trash2 size={16} aria-hidden />
          </IconButton>
        ) : (
          <Box />
        )}
      </Box>
      {mensajes.length > 0 && (
        <Box role="alert" sx={{ color: 'error.main', fontSize: 12, mt: 0.25 }}>
          {[...new Set(mensajes)].join(' ')}
        </Box>
      )}
    </Box>
  );
}
