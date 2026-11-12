import { TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CampoTexto, Rejilla } from '../campos';
import { nombrePais, PAISES } from '../paises';
import { SelectorUnico } from '../selectores';
import { SeccionDeclaracion, type PropsSeccion } from './CamposPep';
import { MAXIMO } from './declaracion';

/** Sección 1: lo que sale de la información ya diligenciada (solo lectura) y lo que falta (nacionalidad y, en una persona, dirección). */
export function SeccionBasicos({ d, cambiar, mensaje, basicos }: PropsSeccion) {
  const { t } = useTranslation();
  const lectura = { input: { readOnly: true } } as const;
  const documento = [basicos.tipoIdentificacion, basicos.numero].filter(Boolean).join(' ');
  const relacion = t(`ficha.pep.basicos.relaciones.${basicos.relacion}`) + (basicos.relacionOtra ? `: ${basicos.relacionOtra}` : '');
  return (
    <SeccionDeclaracion titulo={t('ficha.pep.secciones.basicos')} ayuda={t('ficha.pep.basicos.ayuda')}>
      <Rejilla>
        <TextField label={t('ficha.pep.basicos.nombre')} value={basicos.nombre} fullWidth slotProps={lectura} />
        <TextField label={t('ficha.pep.basicos.documento')} value={documento} fullWidth slotProps={lectura} />
        <TextField label={t('ficha.pep.basicos.relacion')} value={relacion} fullWidth slotProps={lectura} />
        {basicos.nacionalidadFija ? (
          <TextField label={t('ficha.pep.basicos.nacionalidad')} value={nombrePais(basicos.nacionalidadFija)} fullWidth slotProps={lectura} />
        ) : (
          <SelectorUnico etiqueta={t('ficha.pep.basicos.nacionalidad')} opciones={PAISES} valor={d.nacionalidad} onCambiar={(v) => cambiar({ nacionalidad: v })} mensaje={mensaje('nacionalidad')} />
        )}
      </Rejilla>
      {basicos.direccionFija ? (
        <TextField label={t('ficha.pep.basicos.direccion')} value={basicos.direccionFija} fullWidth slotProps={lectura} />
      ) : (
        <CampoTexto etiqueta={t('ficha.pep.basicos.direccion')} valor={d.direccion} onCambiar={(v) => cambiar({ direccion: v })} mensaje={mensaje('direccion')} maxLength={MAXIMO.direccion} autoComplete="street-address" />
      )}
    </SeccionDeclaracion>
  );
}
