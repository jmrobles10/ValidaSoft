import { Box, Skeleton, Stack, Typography } from '@mui/material';
import type { EmpresaDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useTextosLegales } from './api';
import { FilaTextoLegal } from './FilaTextoLegal';
import { TIPOS_TEXTO, vigenteDe } from './modelo';

/** Autorización de tratamiento de datos y términos de la empresa: versión vigente de cada uno y publicar una nueva. */
export function TextosLegalesEmpresa({ empresa, puedeLeer, puedeEscribir }: { empresa: EmpresaDto; puedeLeer: boolean; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const consulta = useTextosLegales(empresa.id, puedeLeer);
  if (!puedeLeer) return null;

  return (
    <Box component="section" aria-labelledby={`textos-${empresa.id}`}>
      <Typography id={`textos-${empresa.id}`} component="h3" sx={{ fontSize: 14, fontWeight: 600 }}>
        {t('empresasGrupo.textos.titulo')}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1, maxWidth: 720 }}>
        {t('empresasGrupo.textos.ayuda', { nombre: empresa.nombre })}
      </Typography>
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending ? (
        <Stack spacing={1} aria-busy>
          <Skeleton variant="rounded" height={32} />
          <Skeleton variant="rounded" height={32} />
        </Stack>
      ) : (
        consulta.data && (
          <Box component="ul" aria-label={t('empresasGrupo.textos.titulo')} sx={{ m: 0, p: 0, listStyle: 'none' }}>
            {TIPOS_TEXTO.map(({ ruta }) => (
              <FilaTextoLegal key={ruta} empresaId={empresa.id} empresaNombre={empresa.nombre} ruta={ruta} vigente={vigenteDe(consulta.data, ruta)} puedeEscribir={puedeEscribir} />
            ))}
          </Box>
        )
      )}
    </Box>
  );
}
