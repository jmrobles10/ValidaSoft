import { Skeleton, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useTercero } from '../api';

/**
 * Nombre e identificación del tercero. `VinculoDto` solo trae `terceroId`, así que cada fila lo consulta (con caché
 * de 5 minutos); pendiente pedir al backend un resumen del tercero en el listado.
 */
export function NombreTercero({
  terceroId,
  conIdentificacion = true,
  variante = 'body2',
}: {
  terceroId: string;
  conIdentificacion?: boolean;
  variante?: 'body2' | 'h2';
}) {
  const { t } = useTranslation();
  const { data, isPending, isError } = useTercero(terceroId);

  if (isPending) return <Skeleton variant="text" width={180} />;
  if (isError) {
    return (
      <Typography component="span" variant={variante} color="text.secondary">
        {t('vinculos.terceroDesconocido')}
      </Typography>
    );
  }

  const { numero, digitoVerificacion, tipo } = data.identificacion;
  return (
    <Stack component="span" spacing={0} sx={{ minWidth: 0 }}>
      <Typography component="span" variant={variante} noWrap sx={{ display: 'block' }}>
        {data.nombre}
      </Typography>
      {conIdentificacion && (
        <Typography variant="mono" color="text.secondary" sx={{ fontSize: 12, display: 'block' }}>
          {tipo} {numero}
          {digitoVerificacion ? `-${digitoVerificacion}` : ''}
        </Typography>
      )}
    </Stack>
  );
}
