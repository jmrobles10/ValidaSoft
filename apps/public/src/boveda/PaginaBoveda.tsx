import { Button, Stack } from '@mui/material';
import { EstadoVacio } from '@validasoft/ui';
import { FolderOpen, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { useBoveda } from '../shared/api';
import { CabeceraPagina, ErrorRecuperable, EsqueletoTarjetas } from '../shared/componentes';
import { AvisoConsolidado } from './AvisoConsolidado';
import { TarjetaDocumento } from './TarjetaDocumento';

/** Mi bóveda: cada documento con su versión vigente y con qué empresas está compartido. */
export function PaginaBoveda() {
  const { t } = useTranslation();
  const consulta = useBoveda();
  const subir = (
    <Button component={RouterLink} to="/boveda/subir" variant="contained" startIcon={<Upload size={18} aria-hidden />} sx={{ width: { xs: '100%', sm: 'auto' } }}>
      {t('boveda.subir')}
    </Button>
  );

  return (
    <>
      <CabeceraPagina titulo={t('boveda.titulo')} sub={t('boveda.sub')} accion={subir} />
      {consulta.isPending ? (
        <EsqueletoTarjetas />
      ) : consulta.isError ? (
        <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />
      ) : consulta.data.documentos.length === 0 ? (
        <EstadoVacio icono={FolderOpen} titulo={t('boveda.vacio')} texto={t('boveda.vacioTexto')} />
      ) : (
        <>
          {/* Lo que hay que renovar, primero lo que vence antes, consolidado entre todas las empresas. */}
          <AvisoConsolidado documentos={consulta.data.documentos} />
            <Stack spacing={1.5}>
            {/* Una tarjeta compacta por documento: la versión es una para todas las empresas y cada fila es una empresa. */}
            {consulta.data.documentos.map((d) => (
              <TarjetaDocumento key={d.id} documento={d} />
            ))}
          </Stack>
        </>
      )}
    </>
  );
}
