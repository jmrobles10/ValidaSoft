import { Box, ButtonBase, Chip, Skeleton, Stack, Typography } from '@mui/material';
import type { FlujoVersionDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';

/** Versiones del flujo, de la más nueva a la más vieja; elegir una la muestra en solo lectura. */
export function VersionesFlujo({ versiones, actual, onVer }: { versiones: readonly FlujoVersionDto[] | undefined; actual: number | null; onVer: (version: number | null) => void }) {
  const { t } = useTranslation();
  const ordenadas = [...(versiones ?? [])].sort((a, b) => b.version - a.version);

  return (
    <Tarjeta titulo={t('flujos.editor.versiones')}>
      {!versiones && <Skeleton variant="rounded" height={80} />}
      <Stack component="ul" spacing={0.5} sx={{ m: 0, p: 0, listStyle: 'none' }}>
        {ordenadas.map((v) => {
          const fecha =
            v.estado === 'Publicada' && v.publicadaEn
              ? t('flujos.editor.publicadaEl', { fecha: formatearFecha(v.publicadaEn) })
              : v.estado === 'Retirada' && v.retiradaEn
                ? t('flujos.editor.retiradaEl', { fecha: formatearFecha(v.retiradaEn) })
                : t('flujos.editor.creadaEl', { fecha: formatearFecha(v.creadaEn) });
          const elegida = actual === v.version;
          return (
            <li key={v.id}>
              <ButtonBase
                onClick={() => onVer(elegida ? null : v.version)}
                aria-pressed={elegida}
                sx={{
                  width: '100%',
                  justifyContent: 'flex-start',
                  textAlign: 'left',
                  gap: 1,
                  px: 1,
                  py: 0.75,
                  borderRadius: (th) => `${String(th.radio.sm)}px`,
                  bgcolor: elegida ? 'acento.tinte' : 'transparent',
                  '&:hover': { bgcolor: elegida ? 'acento.tinte' : 'superficie.nivel2' },
                }}
              >
                <Typography component="span" variant="monoTabla" sx={{ fontWeight: 600, minWidth: 28 }}>
                  v{v.version}
                </Typography>
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 12 }}>
                    {fecha}
                  </Typography>
                </Box>
                <Chip
                  size="small"
                  variant="outlined"
                  label={t(`flujos.editor.estadosVersion.${v.estado}`)}
                  sx={{ color: v.estado === 'Publicada' ? 'estado.exito.texto' : v.estado === 'Borrador' ? 'estado.encurso.texto' : 'text.secondary' }}
                />
              </ButtonBase>
            </li>
          );
        })}
      </Stack>
    </Tarjeta>
  );
}
