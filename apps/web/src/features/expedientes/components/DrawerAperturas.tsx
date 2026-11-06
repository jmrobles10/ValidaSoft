import { Box, Button, Drawer, IconButton, Stack, Typography } from '@mui/material';
import type { VinculoDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFecha } from '@validasoft/ui';
import { FolderOpen, X } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NombreTercero } from '../../vinculos/components/NombreTercero';
import { DialogoAbrirExpediente } from '../DialogoAbrirExpediente';

/**
 * Vínculos cuyo tercero aceptó sin que se pudiera abrir el expediente por falta de flujo publicado (FL-6). El
 * administrador abre cada uno a mano; al publicar un flujo, los nuevos se abren solos.
 */
export function DrawerAperturas({ empresaId, vinculos, onCerrar }: { empresaId: string; vinculos: readonly VinculoDto[]; onCerrar: () => void }) {
  const { t } = useTranslation();
  const [abriendo, setAbriendo] = useState<VinculoDto | null>(null);

  return (
    <Drawer anchor="right" open onClose={onCerrar} slotProps={{ paper: { sx: { width: 440, maxWidth: '100%' } } }} aria-labelledby="titulo-aperturas">
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
        <Typography id="titulo-aperturas" variant="h4" component="h2">
          {t('bandeja.aperturas.titulo')}
        </Typography>
        <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar}>
          <X size={18} strokeWidth={1.75} />
        </IconButton>
      </Stack>
      <Box sx={{ p: 2.5, overflowY: 'auto' }}>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {t('bandeja.aperturas.ayuda')}
        </Typography>
        {vinculos.length === 0 ? (
          <EstadoVacio compacto titulo={t('bandeja.aperturas.vacio')} />
        ) : (
          <Box component="ul" sx={{ m: 0, p: 0, listStyle: 'none', border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.md)}px` }}>
            {vinculos.map((v) => (
              <Box
                component="li"
                key={v.id}
                sx={{ display: 'flex', alignItems: 'center', gap: 1.5, px: 1.5, py: 1.25, '& + &': { borderTop: 1, borderColor: 'divider' } }}
              >
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <NombreTercero terceroId={v.terceroId} />
                  <Typography color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px', mt: 0.25 }}>
                    {v.tipoVinculo.nombre}
                    {v.invitacionAceptadaEn ? ` · ${t('bandeja.aperturas.aceptada', { fecha: formatearFecha(v.invitacionAceptadaEn) })}` : ''}
                  </Typography>
                </Box>
                <Button variant="outlined" size="small" startIcon={<FolderOpen size={14} aria-hidden />} onClick={() => setAbriendo(v)} sx={{ flex: 'none' }}>
                  {t('expedientes.abrir.boton')}
                </Button>
              </Box>
            ))}
          </Box>
        )}
      </Box>
      {abriendo && (
        <DialogoAbrirExpediente empresaId={empresaId} vinculoId={abriendo.id} tipoVinculo={abriendo.tipoVinculo} onCerrar={() => setAbriendo(null)} />
      )}
    </Drawer>
  );
}
