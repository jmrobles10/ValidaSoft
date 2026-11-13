import { Alert, Box, Button, Card, Stack, Typography } from '@mui/material';
import type { CertificadoPublicoDto } from '@validasoft/api-client';
import { AnimacionExito, ChipEstado, tonoDe } from '@validasoft/ui';
import { ArrowRight, Download } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { mensajeDeError } from '../shared/errores';
import { LogoEmpresa } from '../shared/componentes';
import { DatosCertificado } from './DatosCertificado';
import { estadoVisibleDelCertificado, rutaDeVerificacion, textoDelEstado } from './estadoCertificado';
import { useDescargarCertificado } from './useVerificacion';

/**
 * Lo primero que ve quien escanea el QR: el estado en grande (con su ícono y su tono, legible sin color), qué
 * significa y, debajo, los datos del certificado y la descarga del PDF (solo de una emisión vigente).
 */
export function TarjetaEstadoCertificado({ certificado: c, huella }: { certificado: CertificadoPublicoDto; huella: string | null }) {
  const { t } = useTranslation();
  const estado = estadoVisibleDelCertificado(c);
  const tono = estado ? tonoDe(estado, c.bloqueante) : 'neutro';
  const { titulo, texto } = textoDelEstado(c, t);
  const descargar = useDescargarCertificado(c.folio, huella);

  return (
    <Card component="section" aria-labelledby="estado-certificado" sx={{ p: 0, overflow: 'hidden' }}>
      <Box
        role="status"
        sx={(tema) => ({ p: 2.5, bgcolor: tema.palette.estado[tono].fondo, borderBottom: `1px solid ${tema.palette.estado[tono].borde}` })}
      >
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 1.5 }}>
          <LogoEmpresa nombre={c.emisor.empresa} logoUrl={c.emisor.logoUrl} />
          <Typography sx={{ fontWeight: 500, fontSize: 15, flex: 1, minWidth: 0, overflowWrap: 'anywhere' }}>{c.emisor.empresa}</Typography>
          {estado && <ChipEstado estado={estado} bloqueante={c.bloqueante} size="medium" />}
        </Stack>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
          {/* Un certificado vigente se «confirma» al abrirlo: la animación de éxito de marca (el estado ya está en texto, así que es decorativa). */}
          {tono === 'exito' && <AnimacionExito tamano={44} decorativa />}
          <Box sx={{ minWidth: 0 }}>
            <Typography id="estado-certificado" variant="h3" component="h2">
              {titulo}
            </Typography>
            <Typography sx={{ color: 'text.secondary', mt: 0.5, fontSize: 15, lineHeight: '22px' }}>
              {texto}
            </Typography>
          </Box>
        </Stack>
        {c.reemplazadoPor && (
          <Button
            component={RouterLink}
            to={rutaDeVerificacion(c.reemplazadoPor)}
            variant="contained"
            endIcon={<ArrowRight size={18} aria-hidden />}
            sx={{ mt: 1.5 }}
          >
            {t('verificacion.verActual', { folio: c.reemplazadoPor })}
          </Button>
        )}
      </Box>

      <Box sx={{ p: 2.5 }}>
        <DatosCertificado certificado={c} />
        <Box sx={{ mt: 2.5 }}>
          {c.pdfDescargable ? (
            <Button
              variant="outlined"
              startIcon={<Download size={18} aria-hidden />}
              onClick={() => descargar.mutate()}
              disabled={descargar.isPending}
            >
              {descargar.isPending ? t('verificacion.descargando') : t('verificacion.descargar')}
            </Button>
          ) : (
            <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>
              {t('verificacion.soloVigente')}
            </Typography>
          )}
          {descargar.isError && (
            <Alert severity="error" sx={{ mt: 1.5 }}>
              {mensajeDeError(descargar.error, t)}
            </Alert>
          )}
        </Box>
      </Box>
    </Card>
  );
}
