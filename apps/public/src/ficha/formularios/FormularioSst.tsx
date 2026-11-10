import { Divider, Stack } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CampoSiNo } from '../campos';
import { PREGUNTAS_SST, type DatosSst, type PropsFormulario } from '../tipos';

/** Seguridad y salud en el trabajo (`FICHA_SST`): las ocho preguntas sí/no del Portal; un «No» también es respuesta. */
export function FormularioSst({ datos, onCambiar, mensaje, deshabilitado }: PropsFormulario<DatosSst>) {
  const { t } = useTranslation();
  return (
    <Stack spacing={1.5} divider={<Divider flexItem />}>
      {PREGUNTAS_SST.map((pregunta) => (
        <CampoSiNo
          key={pregunta}
          etiqueta={t(`ficha.sst.preguntas.${pregunta}`)}
          valor={datos[pregunta]}
          onCambiar={(valor) => onCambiar({ [pregunta]: valor })}
          mensaje={mensaje(pregunta)}
          deshabilitado={deshabilitado}
        />
      ))}
    </Stack>
  );
}
