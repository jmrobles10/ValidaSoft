import { Box, Button, Card, Stack, Typography } from '@mui/material';
import { ChipEstado, EstadoVacio, formatearFecha } from '@validasoft/ui';
import { Building2, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { useVinculosPublicos } from '../shared/api';
import { CabeceraPagina, ErrorRecuperable, EsqueletoTarjetas, LogoEmpresa } from '../shared/componentes';

/** Mis empresas: las que invitaron al tercero, con el estado del vínculo y el siguiente paso de cada una. */
export function PaginaEmpresas() {
  const { t } = useTranslation();
  const consulta = useVinculosPublicos();

  return (
    <>
      <CabeceraPagina titulo={t('empresas.titulo')} sub={t('empresas.sub')} />
      {consulta.isPending ? (
        <EsqueletoTarjetas />
      ) : consulta.isError ? (
        <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />
      ) : consulta.data.length === 0 ? (
        <EstadoVacio icono={Building2} titulo={t('empresas.vacio')} texto={t('empresas.vacioTexto')} />
      ) : (
        <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {consulta.data.map((v) => (
            <Card key={v.id} component="li" sx={{ p: 2 }}>
              <Stack direction="row" spacing={1.5} alignItems="center">
                <LogoEmpresa nombre={v.empresa.nombre} logoUrl={v.empresa.logoUrl} />
                <Box sx={{ minWidth: 0, flex: 1 }}>
                  <Typography component="h2" sx={{ fontWeight: 600, fontSize: 16, lineHeight: '22px' }}>
                    {v.empresa.nombre}
                  </Typography>
                  <Typography color="text.secondary" sx={{ fontSize: 14 }}>
                    {v.tipoVinculo.nombre}
                  </Typography>
                </Box>
                <ChipEstado estado={v.estado} size="medium" />
              </Stack>
              <Typography color="text.secondary" sx={{ fontSize: 14, lineHeight: '20px', mt: 1.5 }}>
                {v.invitacionAceptada && v.invitacionAceptadaEn
                  ? t('empresas.aceptada', { fecha: formatearFecha(v.invitacionAceptadaEn) })
                  : t('empresas.pendienteAceptar')}
              </Typography>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.5 }}>
                {!v.invitacionAceptada && (
                  <Button component={RouterLink} to={`/empresas/${v.id}`} variant="contained" endIcon={<ChevronRight size={18} aria-hidden />}>
                    {t('empresas.aceptar')}
                  </Button>
                )}
                {v.expedienteId && (
                  <Button component={RouterLink} to={`/expediente/${v.expedienteId}`} variant={v.invitacionAceptada ? 'contained' : 'outlined'}>
                    {t('empresas.verExpediente')}
                  </Button>
                )}
              </Stack>
            </Card>
          ))}
        </Stack>
      )}
    </>
  );
}
