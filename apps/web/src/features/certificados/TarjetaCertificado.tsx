import { Alert, Box, Button, Link, Stack, Typography } from '@mui/material';
import { SCOPES, type VinculoDto } from '@validasoft/api-client';
import { ChipEstado, Dato, formatearDiaCivil, formatearFecha, RejillaDatos } from '@validasoft/ui';
import { Ban, Download, ExternalLink, ShieldEllipsis } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, IconoCarga, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { esEstadoCertificado, estadoVisibleDelCertificado, pdfPendiente, useCertificadoVinculo, useDescargarCertificado } from './api';
import { DialogoRevocarCertificado } from './DialogoRevocarCertificado';

/** Certificado del vínculo: estado de la emisión actual, descarga del PDF, verificación pública, revocación e historial. */
export function TarjetaCertificado({ vinculo }: { vinculo: Pick<VinculoDto, 'id'> }) {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.certificadosLeer);
  const puedeRevocar = permisos.puede(SCOPES.expedientesAdministrar);
  const consulta = useCertificadoVinculo(empresa.id, vinculo.id, puedeLeer);
  const descargar = useDescargarCertificado(empresa.id);
  const [revocando, setRevocando] = useState(false);

  if (!puedeLeer) return null;
  const actual = consulta.data?.actual ?? null;
  const anteriores = (consulta.data?.emisiones ?? []).filter((e) => e.folio !== actual?.folio);
  const visible = actual ? estadoVisibleDelCertificado(actual) : '';

  return (
    <Tarjeta titulo={t('certificados.titulo')}>
      {consulta.isError && <ErrorRecuperable mensaje={`${t('certificados.errorCarga')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      {consulta.data && !actual && (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('certificados.sinCertificado')}
        </Typography>
      )}
      {actual && (
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }} useFlexGap>
            {esEstadoCertificado(visible) ? <ChipEstado estado={visible} bloqueante={actual.bloqueante} /> : <Typography variant="body2">{actual.estado}</Typography>}
            <Typography variant="mono" sx={{ fontSize: 13 }}>
              {actual.folio}
            </Typography>
          </Stack>
          {actual.enVerificacion && (
            <Alert severity="info" icon={<ShieldEllipsis size={18} strokeWidth={1.75} />}>
              {t('certificados.enVerificacion', { fecha: actual.vigenteHasta ? formatearDiaCivil(actual.vigenteHasta) : t('certificados.sinVencimiento').toLowerCase() })}
            </Alert>
          )}
          {actual.pendientes.length > 0 && <Alert severity={actual.bloqueante ? 'error' : 'warning'}>{t('certificados.pendientes', { pendientes: actual.pendientes.join(', ') })}</Alert>}
          {actual.suspension && <Alert severity="warning">{t('certificados.suspendido', { fecha: formatearFecha(actual.suspension.desde), motivo: actual.suspension.motivo })}</Alert>}
          {actual.revocacion && (
            <Alert severity="error">
              {t('certificados.revocado', {
                fecha: formatearFecha(actual.revocacion.fecha),
                origen: actual.revocacion.origen === 'Sistema' ? t('certificados.origenes.Sistema') : t('certificados.origenes.Administrador'),
                motivo: actual.revocacion.motivo,
              })}
            </Alert>
          )}
          <RejillaDatos>
            <Dato etiqueta={t('certificados.emitido')}>{formatearFecha(actual.emitidoEn)}</Dato>
            <Dato etiqueta={t('certificados.vigenteHasta')}>{actual.vigenteHasta ? formatearDiaCivil(actual.vigenteHasta) : t('certificados.sinVencimiento')}</Dato>
            <Dato etiqueta={t('certificados.expediente')}>
              <Typography component="span" variant="mono">
                {actual.expedienteFolio}
              </Typography>
            </Dato>
            <Dato etiqueta={t('certificados.huella')}>
              <Typography component="span" variant="mono" sx={{ overflowWrap: 'anywhere' }}>
                {actual.huella}
              </Typography>
            </Dato>
          </RejillaDatos>
          {descargar.isError && <Alert severity={pdfPendiente(descargar.error) ? 'info' : 'error'}>{pdfPendiente(descargar.error) ? t('certificados.generando') : mensajeDeError(descargar.error, t)}</Alert>}
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }} useFlexGap>
            <Button variant="outlined" startIcon={descargar.isPending ? <IconoCarga /> : <Download size={16} aria-hidden />} disabled={descargar.isPending} onClick={() => descargar.mutate(actual.folio)}>
              {t('certificados.descargar')}
            </Button>
            <Button variant="text" component={Link} href={actual.urlVerificacion} target="_blank" rel="noopener" startIcon={<ExternalLink size={16} aria-hidden />}>
              {t('certificados.verificar')}
            </Button>
            {puedeRevocar && actual.estado !== 'Revocado' && actual.esLaActual && (
              <Button variant="text" color="error" startIcon={<Ban size={16} aria-hidden />} onClick={() => setRevocando(true)} sx={{ color: 'estado.error.texto' }}>
                {t('certificados.revocar')}
              </Button>
            )}
          </Stack>
          {anteriores.length > 0 && (
            <Box component="details">
              <Typography component="summary" variant="body2" sx={{ cursor: 'pointer', color: 'text.secondary' }}>
                {t('certificados.emisiones')} ({anteriores.length})
              </Typography>
              <Stack component="ul" spacing={0.5} sx={{ m: 0, mt: 1, pl: 2.5 }}>
                {anteriores.map((e) => (
                  <Typography component="li" variant="body2" key={e.folio}>
                    {t('certificados.emisionN', { n: String(e.emision) })} · <span style={{ fontFamily: 'monospace' }}>{e.folio}</span> · {e.estado} · {formatearFecha(e.emitidoEn)}
                  </Typography>
                ))}
              </Stack>
            </Box>
          )}
        </Stack>
      )}
      {revocando && actual && <DialogoRevocarCertificado empresaId={empresa.id} folio={actual.folio} onCerrar={() => setRevocando(false)} />}
    </Tarjeta>
  );
}
