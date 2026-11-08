import { Box, Stack, Typography } from '@mui/material';
import type { ExpedientePublicoDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { enlaceDeSubida, requisitosPorGrupo, type GrupoRequisito } from './requisitos';
import { TarjetaRequisito } from './TarjetaRequisito';

const GRUPOS: readonly GrupoRequisito[] = ['te-toca', 'en-revision', 'listo'];

/** Los documentos que pide la empresa, agrupados en «Te toca», «Lo revisa la empresa» y «Listo». */
export function RequisitosDelExpediente({ expediente, vinculoId, soloLectura = false }: { expediente: ExpedientePublicoDto; vinculoId: string | null; soloLectura?: boolean }) {
  const { t } = useTranslation();
  const grupos = requisitosPorGrupo(expediente);
  const empresa = expediente.empresa.nombre;

  return (
    <Box component="section" aria-labelledby="titulo-documentos" sx={{ mb: 2 }}>
      <Typography id="titulo-documentos" component="h2" variant="h4" sx={{ mb: 1.5 }}>
        {t('expediente.documentos.titulo', { empresa })}
      </Typography>
      <Stack spacing={2}>
        {GRUPOS.filter((g) => grupos[g].length > 0).map((grupo) => (
          <Box key={grupo}>
            <Typography component="h3" sx={{ fontWeight: 600, fontSize: 13, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'text.secondary', mb: 1 }}>
              {t(`expediente.documentos.grupos.${grupo}`, { empresa })}
            </Typography>
            <Stack component="ul" spacing={1.25} sx={{ p: 0, m: 0, listStyle: 'none' }}>
              {grupos[grupo].map((r) => (
                <TarjetaRequisito key={r.id} requisito={r} expediente={expediente} enlace={soloLectura ? null : enlaceDeSubida(r, vinculoId, expediente.id)} />
              ))}
            </Stack>
          </Box>
        ))}
      </Stack>
    </Box>
  );
}
