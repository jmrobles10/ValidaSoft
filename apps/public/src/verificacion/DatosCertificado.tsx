import { Typography } from '@mui/material';
import type { CertificadoPublicoDto } from '@validasoft/api-client';
import { Dato, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { formatearDia } from '../vigencia/vigencia';

/** Lo que expone la verificación pública (P-8-1): sin documentos, contactos, revisores ni motivo del bloqueo. */
export function DatosCertificado({ certificado: c }: { certificado: CertificadoPublicoDto }) {
  const { t } = useTranslation();
  return (
    <RejillaDatos columnas={2}>
      <Dato etiqueta={t('verificacion.datos.tercero')}>
        {c.titular.nombre}
        <Typography component="span" color="text.secondary" sx={{ display: 'block', fontSize: 14 }}>
          {c.titular.identificacionEnmascarada}
        </Typography>
      </Dato>
      <Dato etiqueta={t('verificacion.datos.empresa')}>{c.emisor.empresa}</Dato>
      <Dato etiqueta={t('verificacion.datos.proceso')}>{c.flujo.nombre}</Dato>
      <Dato etiqueta={t('verificacion.datos.vigencia')}>
        {c.vigenteHasta ? formatearDia(c.vigenteHasta) : t('verificacion.datos.sinVencimiento')}
      </Dato>
      <Dato etiqueta={t('verificacion.datos.folio')}>
        <Typography component="span" variant="mono">
          {c.folio}
        </Typography>
        <Typography component="span" color="text.secondary" sx={{ display: 'block', fontSize: 14 }}>
          {t('verificacion.datos.emision', { n: String(c.emision) })}
        </Typography>
      </Dato>
      <Dato etiqueta={t('verificacion.datos.emitido')}>{formatearFechaHora(c.emitidoEn)}</Dato>
      <Dato etiqueta={t('verificacion.datos.huella')}>
        <Typography component="span" variant="mono">
          {c.huella}
        </Typography>
      </Dato>
    </RejillaDatos>
  );
}
