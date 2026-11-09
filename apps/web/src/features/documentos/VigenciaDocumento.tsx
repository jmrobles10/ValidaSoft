import { Alert, Box, Stack, Tooltip, Typography } from '@mui/material';
import type { VigenciaDto } from '@validasoft/api-client';
import { ChipEstado, visualmenteOculto } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { CHIP_VIGENCIA, estadoVigencia, etiquetaVigencia, explicacionVigencia, severidadAviso, textoAvisoVigencia, textoVencimiento } from './vigencia';

/**
 * Chip compacto de la vigencia («Por vencer · 30/11/2026»): ícono y tono del estado canónico, y en el tooltip de dónde
 * sale la fecha. Para listas apretadas (decisión por área, documentos del vínculo).
 */
export function ChipVigencia({ vigencia }: { vigencia: VigenciaDto }) {
  const { t } = useTranslation();
  const estado = estadoVigencia(vigencia);
  if (!estado) return null;
  const detalle = [textoVencimiento(vigencia, t), explicacionVigencia(vigencia, t)].filter(Boolean).join('. ');
  return (
    <Tooltip title={detalle} describeChild>
      <Box component="span" sx={{ display: 'inline-flex' }}>
        <ChipEstado estado={CHIP_VIGENCIA[estado]} etiqueta={etiquetaVigencia(vigencia, t)} />
        <Box component="span" sx={visualmenteOculto}>
          {detalle}
        </Box>
      </Box>
    </Tooltip>
  );
}

/**
 * Vigencia completa de un documento: el estado, «Vence el …» y, como texto secundario, de dónde sale la fecha (desde la
 * expedición declarada, la carga o el vencimiento propio) y los días de gracia.
 */
export function VigenciaDocumento({ vigencia }: { vigencia: VigenciaDto }) {
  const { t } = useTranslation();
  const estado = estadoVigencia(vigencia);
  if (!estado) return null;
  const explicacion = explicacionVigencia(vigencia, t);
  return (
    <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
      <ChipEstado estado={CHIP_VIGENCIA[estado]} etiqueta={t(`expedientes.vigencia.estados.${estado}`)} />
      {estado !== 'SinVencimiento' && <Typography sx={{ fontSize: 13 }}>{textoVencimiento(vigencia, t)}</Typography>}
      {explicacion && (
        <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>
          {explicacion}
        </Typography>
      )}
    </Stack>
  );
}

/** Aviso visible si el documento está por vencer, en gracia (hasta cuándo) o vencido. */
export function AvisoVigencia({ vigencia }: { vigencia: VigenciaDto }) {
  const { t } = useTranslation();
  const texto = textoAvisoVigencia(vigencia, t);
  if (!texto) return null;
  return (
    <Alert severity={severidadAviso(vigencia)} sx={{ py: 0, fontSize: 13 }}>
      {texto}
    </Alert>
  );
}
