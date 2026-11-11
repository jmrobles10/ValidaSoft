import { Alert, AlertTitle, Box } from '@mui/material';
import type { SinSoporteDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';

/**
 * «Cambio sin soporte» (FM-15, FD-4): el tercero cambió datos de la sección sin un documento nuevo que los respalde. Lista los campos
 * que cambiaron; si alguno es crítico (identidad o representante legal), aprobarlo exige una justificación. Es la misma marca en la
 * revisión del expediente y en la ficha aceptada: la calcula la API, aquí solo se muestra.
 */
export function SinSoporte({ sinSoporte }: { sinSoporte: SinSoporteDto }) {
  const { t } = useTranslation();
  const titulo = t('fichaEmpresa.sinSoporte.titulo');
  return (
    <Alert severity={sinSoporte.critico ? 'error' : 'warning'} aria-label={titulo}>
      <AlertTitle>{titulo}</AlertTitle>
      {t(sinSoporte.critico ? 'fichaEmpresa.sinSoporte.critico' : 'fichaEmpresa.sinSoporte.texto')}
      <Box component="ul" sx={{ m: 0, mt: 0.5, pl: 2.5 }}>
        {sinSoporte.cambios.map((c) => (
          <li key={c.campo}>{c.etiqueta}</li>
        ))}
      </Box>
    </Alert>
  );
}
