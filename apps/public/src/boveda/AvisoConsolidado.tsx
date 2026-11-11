import { Box, Button, Card, Stack, Typography } from '@mui/material';
import type { DocumentoBovedaDto } from '@validasoft/api-client';
import { CalendarClock, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { ChipVigencia } from '../vigencia/ChipVigencia';
import { lineasPorRenovar } from './porRenovar';

/**
 * «Por renovar» (AvisoConsolidado de docs/06): arriba de la bóveda, qué documentos renovar primero y con qué empresas
 * quedas al día, con la acción de subir la versión nueva. No se muestra si no hay nada que atender.
 */
export function AvisoConsolidado({ documentos }: { documentos: readonly DocumentoBovedaDto[] }) {
  const { t } = useTranslation();
  const lineas = lineasPorRenovar(documentos, t);
  if (lineas.length === 0) return null;
  const urgente = lineas.some((l) => l.vigencia.estado === 'Vencida');

  return (
    <Card component="section" aria-labelledby="por-renovar" sx={{ p: 0, overflow: 'hidden', mb: 1.5 }}>
      <Stack direction="row" spacing={1.25} sx={{ alignItems: 'flex-start', py: 1, px: { xs: 1.5, sm: 2 } }}>
        <Box aria-hidden sx={{ display: 'grid', placeItems: 'center', flex: 'none', height: 20, color: urgente ? 'error.main' : 'warning.main' }}>
          <CalendarClock size={18} />
        </Box>
        <Box sx={{ minWidth: 0 }}>
          <Typography id="por-renovar" component="h2" sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px' }}>
            {t('boveda.porRenovar.titulo')}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {t('boveda.porRenovar.sub')}
          </Typography>
        </Box>
      </Stack>
      <Box component="ul" sx={{ p: 0, m: 0 }}>
        {lineas.map((l) => (
          <Stack
            key={l.documentoId}
            component="li"
            direction={{ xs: 'column', sm: 'row' }}
            sx={{ alignItems: { xs: 'stretch', sm: 'center' }, columnGap: 2, rowGap: 0.5, py: { xs: 1, sm: 0.75 }, px: { xs: 1.5, sm: 2 }, borderTop: 1, borderColor: 'divider', listStyle: 'none' }}
          >
            <Box sx={{ minWidth: 0, flex: 1 }}>
              <Box sx={{ mb: 0.25 }}>
                <ChipVigencia vigencia={l.vigencia} />
              </Box>
              <Typography sx={{ fontSize: { xs: 15, sm: 14 }, lineHeight: { xs: '22px', sm: '20px' } }}>
                <Box component="strong" sx={{ fontWeight: 600 }}>{`${l.documento}:`}</Box> {l.texto}
              </Typography>
            </Box>
            {l.subir && (
              <Box sx={{ flex: 'none', ml: { xs: -1, sm: 0 } }}>
                <Button
                  component={RouterLink}
                  to={l.subir}
                  variant="text"
                  startIcon={<Upload size={16} aria-hidden />}
                  aria-label={`${t('boveda.porRenovar.subir')}: ${l.documento}`}
                  size="small"
                >
                  {t('boveda.porRenovar.subir')}
                </Button>
              </Box>
            )}
          </Stack>
        ))}
      </Box>
    </Card>
  );
}
