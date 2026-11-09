import { Box, Stack, Typography } from '@mui/material';
import type { ExpedientePublicoDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { enlaceDeSubida, requisitosPorGrupo, type GrupoRequisito } from './requisitos';
import { TarjetaRequisito } from './TarjetaRequisito';

const GRUPOS: readonly GrupoRequisito[] = ['te-toca', 'en-revision', 'listo'];

/**
 * Los documentos que pide la empresa, agrupados en «Te toca», «Lo revisa la empresa» y «Listo»: cada grupo es una lista
 * con divisores dentro de un solo marco (nunca una tarjeta por documento).
 */
export function RequisitosDelExpediente({ expediente, vinculoId, soloLectura = false }: { expediente: ExpedientePublicoDto; vinculoId: string | null; soloLectura?: boolean }) {
  const { t } = useTranslation();
  const grupos = requisitosPorGrupo(expediente);
  const empresa = expediente.empresa.nombre;

  return (
    <Box component="section" aria-labelledby="titulo-documentos" sx={{ mb: 2 }}>
      <Typography id="titulo-documentos" component="h2" variant="h4" sx={{ mb: 1 }}>
        {t('expediente.documentos.titulo', { empresa })}
      </Typography>
      <Stack spacing={1.5}>
        {GRUPOS.filter((g) => grupos[g].length > 0).map((grupo) => (
          <Box key={grupo}>
            <Typography component="h3" sx={{ fontWeight: 600, fontSize: 11, lineHeight: '16px', letterSpacing: '0.06em', textTransform: 'uppercase', color: 'text.secondary', mb: 0.75 }}>
              {t(`expediente.documentos.grupos.${grupo}`, { empresa })}
            </Typography>
            <Box
              component="ul"
              sx={(th) => ({
                p: 0,
                m: 0,
                listStyle: 'none',
                overflow: 'hidden',
                border: 1,
                borderColor: 'divider',
                borderRadius: `${String(th.radio.md)}px`,
                bgcolor: 'background.paper',
                '& > li + li': { borderTop: 1, borderColor: 'divider' },
              })}
            >
              {grupos[grupo].map((r) => (
                <TarjetaRequisito key={r.id} requisito={r} expediente={expediente} enlace={soloLectura ? null : enlaceDeSubida(r, vinculoId, expediente.id)} />
              ))}
            </Box>
          </Box>
        ))}
      </Stack>
    </Box>
  );
}
