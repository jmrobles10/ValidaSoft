import { Alert, Stack, Typography } from '@mui/material';
import type { VigenciaDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { avisoDeVigencia, ESTADO_DEL_CHIP, estadoDeVigencia, textoVigencia } from './vigencia';

/** Estado de la vigencia con el ícono del design system y el texto del tercero («Por vencer», «No vence»). */
export function ChipVigencia({ vigencia, size = 'small' }: { vigencia: VigenciaDto; size?: 'small' | 'medium' }) {
  const { t } = useTranslation();
  const estado = estadoDeVigencia(vigencia);
  if (!estado) return null;
  return <ChipEstado estado={ESTADO_DEL_CHIP[estado]} etiqueta={t(`vigencia.estados.${estado}`)} size={size} />;
}

/** «Vence el 28/09/2026» junto a su estado. */
export function LineaVigencia({ vigencia }: { vigencia: VigenciaDto | null | undefined }) {
  const { t } = useTranslation();
  if (!vigencia || !estadoDeVigencia(vigencia)) return null;
  const texto = textoVigencia(vigencia, t);
  return (
    <Stack direction="row" spacing={1} alignItems="center" useFlexGap flexWrap="wrap" sx={{ mt: 0.75 }}>
      <ChipVigencia vigencia={vigencia} />
      {texto && (
        <Typography component="span" color="text.secondary" sx={{ fontSize: 14 }}>
          {texto}
        </Typography>
      )}
    </Stack>
  );
}

/** Qué hacer si está por vencer, en periodo de gracia o vencido; nada en los demás casos. */
export function AvisoVigencia({ vigencia }: { vigencia: VigenciaDto | null | undefined }) {
  const { t } = useTranslation();
  const aviso = vigencia ? avisoDeVigencia(vigencia, t) : null;
  if (!aviso) return null;
  return (
    <Alert severity={aviso.severidad} sx={{ mt: 1.5 }}>
      {aviso.texto}
    </Alert>
  );
}
