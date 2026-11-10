import { Autocomplete, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { camposSenalables, etiquetaDeRuta } from '../../terceros/ficha/secciones';

export interface SelectorCamposSeccionProps {
  /** Código de la sección observada (`FICHA_GENERAL`…). */
  seccion: string;
  elegidos: readonly string[];
  onCambiar: (campos: string[]) => void;
  deshabilitado?: boolean;
}

/**
 * Al observar una sección (FM-28): los campos que el tercero debe corregir, con su etiqueta legible. Opcional; viajan
 * en `campos` (rutas snake_case) y el tercero los ve señalados en «Mi información».
 */
export function SelectorCamposSeccion({ seccion, elegidos, onCambiar, deshabilitado = false }: SelectorCamposSeccionProps) {
  const { t } = useTranslation();
  return (
    <Autocomplete
      multiple
      size="small"
      disableCloseOnSelect
      options={camposSenalables(seccion)}
      value={[...elegidos]}
      onChange={(_, valor) => onCambiar(valor)}
      getOptionLabel={(campo) => etiquetaDeRuta(campo, t)}
      disabled={deshabilitado}
      renderInput={(params) => <TextField {...params} label={t('fichaEmpresa.observar.campos')} helperText={t('fichaEmpresa.observar.camposAyuda')} />}
    />
  );
}
