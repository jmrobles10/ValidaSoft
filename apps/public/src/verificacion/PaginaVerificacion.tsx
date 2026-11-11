import { Stack } from '@mui/material';
import { EsqueletoTarjetas } from '@validasoft/ui';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useSearchParams } from 'react-router';
import { CabeceraPagina } from '../shared/componentes';
import { FalloVerificacion } from './FalloVerificacion';
import { PasosVerificacion } from './PasosVerificacion';
import { TarjetaEstadoCertificado } from './TarjetaEstadoCertificado';
import { useCertificadoVerificado } from './useVerificacion';

/**
 * `/verificar/{folio}?huella=…`: a donde lleva el QR del PDF (`Certificado.UrlDeVerificacion`). Anónima: no pide la
 * sesión del tercero. Muestra el estado vigente del certificado y lo verifica criptográficamente en el navegador.
 */
export function PaginaVerificacion() {
  const { t } = useTranslation();
  const { folio = '' } = useParams();
  const [parametros] = useSearchParams();
  const huella = parametros.get('huella');
  const consulta = useCertificadoVerificado(folio, huella);

  useEffect(() => {
    const anterior = document.title;
    document.title = `${t('verificacion.titulo')} ${folio}`;
    return () => {
      document.title = anterior;
    };
  }, [folio, t]);

  if (consulta.isPending) {
    return (
      <EsqueletoTarjetas cantidad={2} lineas={4} etiqueta={t('verificacion.cargando')} />
    );
  }
  if (consulta.isError) return <FalloVerificacion error={consulta.error} onReintentar={() => void consulta.refetch()} />;

  return (
    <Stack spacing={2}>
      <CabeceraPagina titulo={t('verificacion.titulo')} sub={t('verificacion.sub')} />
      <TarjetaEstadoCertificado certificado={consulta.data} huella={huella} />
      <PasosVerificacion certificado={consulta.data} huella={huella} />
    </Stack>
  );
}
