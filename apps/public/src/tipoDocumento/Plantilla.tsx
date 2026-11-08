import { Box, Button, Stack, Typography } from '@mui/material';
import { Download, FileSignature } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useDescargarPlantilla } from '../shared/api';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';

/** «Descarga el formato, llénalo, fírmalo y súbelo»: el formato vigente que la empresa publicó para este documento (TD-5). */
export function Plantilla({ expedienteId, requisitoId, empresa }: { expedienteId: string; requisitoId: string; empresa: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const descarga = useDescargarPlantilla();

  const descargar = () =>
    descarga.mutate(
      { expedienteId, requisitoId },
      {
        onSuccess: (d) => mostrar(t('plantilla.descargando', { nombre: d.nombre }), 'info'),
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      },
    );

  return (
    <Box sx={{ p: 1.5, borderRadius: 1, bgcolor: 'superficie.nivel2' }}>
      <Stack direction="row" spacing={1.25} alignItems="flex-start">
        <Box sx={{ color: 'text.secondary', pt: 0.25 }} aria-hidden>
          <FileSignature size={20} />
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography sx={{ fontWeight: 600, fontSize: 15 }}>{t('plantilla.titulo', { empresa })}</Typography>
          <Typography color="text.secondary" sx={{ fontSize: 14, lineHeight: '20px' }}>
            {t('plantilla.texto')}
          </Typography>
          <Button variant="outlined" onClick={descargar} disabled={descarga.isPending} startIcon={<Download size={18} aria-hidden />} sx={{ mt: 1 }}>
            {t('plantilla.descargar')}
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
