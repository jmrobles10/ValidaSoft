import { Box, Typography } from '@mui/material';
import type { CertificadoPublicoDto } from '@validasoft/api-client';
import { Dato, RejillaDatos } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';

/** Para quien quiera repetir la verificación por su cuenta: los dos hashes, el algoritmo y la clave usada. */
export function DetallesTecnicos({ certificado: c, hashCalculado }: { certificado: CertificadoPublicoDto; hashCalculado: string | null }) {
  const { t } = useTranslation();
  const mono = (texto: string) => (
    <Typography component="span" variant="mono" sx={{ wordBreak: 'break-all' }}>
      {texto}
    </Typography>
  );
  return (
    <Box component="details" sx={{ mt: 2, '& > summary': { cursor: 'pointer', color: 'text.secondary', fontSize: 14, py: 0.5 } }}>
      <summary>{t('verificacion.cripto.detalles')}</summary>
      <Box sx={{ pt: 1.5 }}>
        <RejillaDatos columnas={2}>
          <Dato etiqueta={t('verificacion.cripto.hashCalculado')} ancho>
            {mono(hashCalculado ?? '—')}
          </Dato>
          <Dato etiqueta={t('verificacion.cripto.hashDeclarado')} ancho>
            {mono(c.hashContenido)}
          </Dato>
          <Dato etiqueta={t('verificacion.cripto.algoritmo')}>{c.firma.algoritmo}</Dato>
          <Dato etiqueta={t('verificacion.cripto.kid')}>{mono(c.firma.kid)}</Dato>
        </RejillaDatos>
      </Box>
    </Box>
  );
}
