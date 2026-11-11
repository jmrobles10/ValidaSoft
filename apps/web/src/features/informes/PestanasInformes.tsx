import { Box, Tab, Tabs } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { usePermisos } from '../../shared/usePermisos';

export type PestanaInforme = 'estado-expedientes' | 'vinculos' | 'sla';

/** Pestañas de `/informes`: cada informe es una ruta; solo se muestran los que la persona puede ver. */
export function PestanasInformes({ empresaId, actual }: { empresaId: string; actual: PestanaInforme }) {
  const { t } = useTranslation();
  const permisos = usePermisos(empresaId);
  const pestanas: { valor: PestanaInforme; etiqueta: string }[] = [];
  if (permisos.puede(SCOPES.expedientesLeer)) pestanas.push({ valor: 'estado-expedientes', etiqueta: t('informes.pestanas.estado') });
  if (permisos.puede(SCOPES.vinculosLeer)) pestanas.push({ valor: 'vinculos', etiqueta: t('informes.pestanas.vinculos') });
  if (permisos.puede(SCOPES.expedientesLeer)) pestanas.push({ valor: 'sla', etiqueta: t('informes.pestanas.sla') });

  return (
    <Box sx={{ mb: 1.5, borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={actual} aria-label={t('informes.pestanas.etiqueta')}>
        {pestanas.map((p) => (
          <Tab key={p.valor} value={p.valor} label={p.etiqueta} component={RouterLink} to={`/e/${empresaId}/informes/${p.valor}`} />
        ))}
      </Tabs>
    </Box>
  );
}
