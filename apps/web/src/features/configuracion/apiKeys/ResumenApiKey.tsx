import { Box, Tooltip, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { claveDeScope, resumirScopes } from './scopes';

/** «Todos los permisos» o los grupos que toca la llave; el detalle (cada permiso) va en el tooltip. */
export function ResumenPermisos({ scopes }: { scopes: readonly string[] }) {
  const { t } = useTranslation();
  const { todos, grupos, desconocidos } = resumirScopes(scopes);
  const texto = todos
    ? t('apiKeys.todosLosPermisos')
    : [...grupos.map((g) => (g.cuantos === g.total ? t(`apiKeys.grupos.${g.grupo}`) : `${t(`apiKeys.grupos.${g.grupo}`)} (${String(g.cuantos)}/${String(g.total)})`)), ...desconocidos].join(' · ');
  const detalle = scopes.map((s) => {
    const clave = claveDeScope(s);
    return clave ? t(`apiKeys.permisos.${clave}`) : s;
  });
  return (
    <Tooltip title={<Box component="span" sx={{ whiteSpace: 'pre-line' }}>{detalle.join('\n')}</Box>}>
      <Typography component="span" variant="body2" sx={{ display: 'block' }}>
        {texto || t('apiKeys.sinPermisos')}
      </Typography>
    </Tooltip>
  );
}
