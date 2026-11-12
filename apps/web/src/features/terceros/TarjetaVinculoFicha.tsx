import { Alert, AlertTitle, Button, Divider, Stack } from '@mui/material';
import type { VinculoEnFichaDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { ArrowRight, Lock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../shared/componentes';
import { estadoVisibleDelVinculo } from '../vinculos/estadoVisible';
import { CertificadoEnFicha, CuentaEnFicha, DocumentosEnFicha } from './SeccionesVinculo';

/** Un vínculo del tercero con la empresa activa: estado, bloqueo, certificado, cuenta designada y documentos. */
export function TarjetaVinculoFicha({ empresaId, datos }: { empresaId: string; datos: VinculoEnFichaDto }) {
  const { t } = useTranslation();
  const { vinculo } = datos;
  const tipo = vinculo.tipoVinculo.nombre;

  return (
    <Tarjeta
      nivel="h3"
      etiqueta={tipo}
      titulo={
        <Stack direction="row" spacing={1} component="span" sx={{ alignItems: 'center', flexWrap: 'wrap' }} useFlexGap>
          <span>{tipo}</span>
          <ChipEstado estado={estadoVisibleDelVinculo(vinculo)} fecha={vinculo.bloqueo?.desde ?? vinculo.actualizadoEn} />
        </Stack>
      }
      acciones={
        <Button
          component={RouterLink}
          to={`/e/${empresaId}/vinculos/${vinculo.id}`}
          variant="text"
          size="small"
          endIcon={<ArrowRight size={16} aria-hidden />}
          aria-label={t('terceros.ficha.vinculos.verVinculoAccesible', { tipo })}
          sx={{ flex: 'none' }}
        >
          {t('terceros.ficha.vinculos.verVinculo')}
        </Button>
      }
    >
      <Stack spacing={1.5} divider={<Divider flexItem />}>
        {vinculo.bloqueo && (
          <Alert severity="error" icon={<Lock size={18} strokeWidth={1.75} />}>
            <AlertTitle>{t('vinculos.detalle.bloqueo')}</AlertTitle>
            {vinculo.bloqueo.motivo}{' '}
            {t('vinculos.detalle.bloqueoDetalle', {
              desde: formatearFecha(vinculo.bloqueo.desde),
              hasta: vinculo.bloqueo.hasta ? t('vinculos.detalle.bloqueoHasta', { hasta: formatearFecha(vinculo.bloqueo.hasta) }) : '',
            })}
          </Alert>
        )}
        <CertificadoEnFicha certificado={datos.certificado} />
        <CuentaEnFicha cuenta={datos.cuentaDesignada} />
        <DocumentosEnFicha documentos={datos.documentos} />
      </Stack>
    </Tarjeta>
  );
}
