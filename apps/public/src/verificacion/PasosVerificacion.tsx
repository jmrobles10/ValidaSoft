import { Alert, Box, Button, Card, CircularProgress, Stack, Typography } from '@mui/material';
import type { CertificadoPublicoDto } from '@validasoft/api-client';
import { visualmenteOculto } from '@validasoft/ui';
import { CircleAlert, CircleCheck, CircleHelp, CircleX, RotateCcw, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { DetallesTecnicos } from './DetallesTecnicos';
import { useVerificacionCriptografica } from './useVerificacion';
import { conclusion, ESTADO_DEL_PASO, normalizarHuella, type Conclusion, type EstadoPaso } from './verificacion';

const ICONO: Record<EstadoPaso, { icono: LucideIcon; tono: 'exito' | 'error' | 'advertencia' | 'neutro' }> = {
  ok: { icono: CircleCheck, tono: 'exito' },
  falla: { icono: CircleX, tono: 'error' },
  aviso: { icono: CircleAlert, tono: 'advertencia' },
  'no-soportado': { icono: CircleHelp, tono: 'neutro' },
};

const SEVERIDAD: Record<Conclusion, 'success' | 'error' | 'warning' | 'info'> = {
  autentico: 'success',
  'no-autentico': 'error',
  'no-soportado': 'info',
  incompleto: 'warning',
};

function Paso({ estado, titulo, detalle }: { estado: EstadoPaso; titulo: string; detalle: string }) {
  const { t } = useTranslation();
  const { icono: Icono, tono } = ICONO[estado];
  return (
    <Stack component="li" direction="row" spacing={1.5} data-estado={estado} sx={{ py: 1.25, borderTop: 1, borderColor: 'divider', listStyle: 'none' }}>
      <Box sx={(tema) => ({ color: tema.palette.estado[tono].texto, pt: 0.25, flex: 'none' })}>
        <Icono size={20} strokeWidth={1.75} aria-hidden />
      </Box>
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontWeight: 500, fontSize: 15 }}>
          {titulo}
          <Box component="span" sx={visualmenteOculto}>
            {`: ${t(`verificacion.cripto.estadosPaso.${estado}`)}`}
          </Box>
        </Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px', overflowWrap: 'anywhere' }}>
          {detalle}
        </Typography>
      </Box>
    </Stack>
  );
}

/**
 * Verificación criptográfica hecha en el navegador de quien consulta, con el resultado de cada paso: contenido
 * íntegro, huella del enlace, clave de la plataforma y sello. Si el navegador no soporta Ed25519 lo dice sin romper.
 */
export function PasosVerificacion({ certificado: c, huella }: { certificado: CertificadoPublicoDto; huella: string | null }) {
  const { t } = useTranslation();
  const { resultado, clavesFallaron, reintentarClaves } = useVerificacionCriptografica(c, huella);
  const valores = { huella: normalizarHuella(huella) ?? huella ?? '', huellaCertificado: c.huella, kid: c.firma.kid };

  return (
    <Card component="section" aria-labelledby="autenticidad" sx={{ p: 2.5 }}>
      <Typography id="autenticidad" variant="h3" component="h2">
        {t('verificacion.cripto.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', mt: 0.5, mb: 2, fontSize: 14, lineHeight: '20px' }}>
        {t('verificacion.cripto.sub')}
      </Typography>

      {resultado ? (
        <>
          <Alert severity={SEVERIDAD[conclusion(resultado)]} role="status" sx={{ mb: 1.5 }}>
            {t(`verificacion.cripto.conclusiones.${conclusion(resultado)}`)}
          </Alert>
          <Box component="ol" sx={{ m: 0, p: 0 }}>
            <Paso estado={ESTADO_DEL_PASO.contenido[resultado.contenido]} titulo={t('verificacion.cripto.pasos.contenido.titulo')} detalle={t(`verificacion.cripto.pasos.contenido.${resultado.contenido}`)} />
            <Paso estado={ESTADO_DEL_PASO.huella[resultado.huella]} titulo={t('verificacion.cripto.pasos.huella.titulo')} detalle={t(`verificacion.cripto.pasos.huella.${resultado.huella}`, valores)} />
            <Paso estado={ESTADO_DEL_PASO.clave[resultado.clave]} titulo={t('verificacion.cripto.pasos.clave.titulo')} detalle={t(`verificacion.cripto.pasos.clave.${resultado.clave}`, valores)} />
            <Paso estado={ESTADO_DEL_PASO.sello[resultado.sello]} titulo={t('verificacion.cripto.pasos.sello.titulo')} detalle={t(`verificacion.cripto.pasos.sello.${resultado.sello}`)} />
          </Box>
          {clavesFallaron && (
            <Button variant="outlined" startIcon={<RotateCcw size={16} aria-hidden />} onClick={reintentarClaves} sx={{ mt: 1.5 }}>
              {t('comun.reintentar')}
            </Button>
          )}
          <DetallesTecnicos certificado={c} hashCalculado={resultado.hashCalculado} />
        </>
      ) : (
        <Stack direction="row" spacing={1.5} role="status" sx={{ alignItems: 'center', py: 1 }}>
          <CircularProgress size={20} aria-hidden />
          <Typography sx={{ color: 'text.secondary' }}>{t('verificacion.cripto.verificando')}</Typography>
        </Stack>
      )}
    </Card>
  );
}
