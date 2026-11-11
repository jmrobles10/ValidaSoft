import { Stack, Typography } from '@mui/material';
import type { VersionDocumentoDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { ChipVigencia } from '../vigencia/ChipVigencia';
import { estadoDeVigencia, requiereAtencion, textoVigencia } from '../vigencia/vigencia';

type VersionParaChip = Pick<VersionDocumentoDto, 'estado' | 'cargadaEn' | 'vigencia'>;

/** La versión que vale hoy: la actual, también cuando ya venció para todas las empresas (la API lo manda como `Vencida`). */
const ESTADOS_DE_LA_VIGENTE: readonly string[] = ['Actual', 'Vencida'];
const esLaVigente = (version: Pick<VersionDocumentoDto, 'estado'>) => ESTADOS_DE_LA_VIGENTE.includes(version.estado);

/**
 * Estado de una versión con la misma palabra en la cabecera de la tarjeta y en el listado (RT-5): la versión que vale hoy es la
 * «Vigente» (no «Actual»). Si está por vencer, en gracia o vencida, dice eso (el chip de vigencia); el resto de estados (Superada,
 * Devuelta, Retirada…) son los del design system.
 */
export function ChipDeVersion({ version }: { version: VersionParaChip }) {
  const { t } = useTranslation();
  if (!esLaVigente(version)) return <ChipEstado estado={version.estado} fecha={version.cargadaEn} />;
  if (version.vigencia && requiereAtencion(version.vigencia)) return <ChipVigencia vigencia={version.vigencia} />;
  return <ChipEstado estado="Vigente" etiqueta={t('boveda.vigente')} />;
}

/** Cabecera de la tarjeta: el chip de la versión vigente y, junto a él, cuándo vence («Vence el 09/11/2027» o «No vence»). */
export function LineaDeVersionVigente({ version }: { version: VersionParaChip }) {
  const { t } = useTranslation();
  const vigencia = version.vigencia;
  const texto = !vigencia ? null : estadoDeVigencia(vigencia) === 'SinVencimiento' ? t('vigencia.estados.SinVencimiento') : textoVigencia(vigencia, t);
  return (
    <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mt: 0.5 }}>
      <ChipDeVersion version={version} />
      {texto && (
        <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
          {texto}
        </Typography>
      )}
    </Stack>
  );
}
