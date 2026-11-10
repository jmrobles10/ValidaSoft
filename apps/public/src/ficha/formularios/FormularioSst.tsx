import { Box, Paper } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CampoSiNo } from '../campos';
import { PREGUNTAS_SST, type DatosSst, type PropsFormulario } from '../tipos';

/**
 * Seguridad y salud en el trabajo (`FICHA_SST`): las ocho preguntas sí/no del Portal; un «No» también es respuesta. Van
 * en una lista compacta, cada pregunta con su Sí/No en la misma fila.
 */
export function FormularioSst({ datos, onCambiar, mensaje, deshabilitado }: PropsFormulario<DatosSst>) {
  const { t } = useTranslation();
  return (
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      {PREGUNTAS_SST.map((pregunta) => (
        <Box key={pregunta} sx={{ px: 2, py: 0.75, borderTop: 1, borderColor: 'divider', '&:first-of-type': { borderTop: 0 } }}>
          <CampoSiNo
            enLinea
            etiqueta={t(`ficha.sst.preguntas.${pregunta}`)}
            valor={datos[pregunta]}
            onCambiar={(valor) => onCambiar({ [pregunta]: valor })}
            mensaje={mensaje(pregunta)}
            deshabilitado={deshabilitado}
          />
        </Box>
      ))}
    </Paper>
  );
}
