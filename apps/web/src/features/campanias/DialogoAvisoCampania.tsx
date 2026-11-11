import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from '@mui/material';
import type { VistaPreviaCampaniaDto } from '@validasoft/api-client';
import { Send } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';

/**
 * Aviso antes de crear la campaña: qué pasará y a cuántos terceros les llega correo y campana, que se procesa en segundo
 * plano y que se puede pausar o cancelar. Recién al confirmar se crea; cancelar no deja nada hecho.
 */
export function DialogoAvisoCampania({
  vista,
  ocupado,
  onConfirmar,
  onCerrar,
}: {
  vista: Pick<VistaPreviaCampaniaDto, 'abririan' | 'recordarian' | 'omitidos' | 'conCorreo'>;
  ocupado: boolean;
  onConfirmar: () => void;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  const lineas: string[] = [
    vista.abririan > 0 ? t('campanias.aviso.abren', { count: vista.abririan }) : '',
    vista.recordarian > 0 ? t('campanias.aviso.recuerdan', { count: vista.recordarian }) : '',
    vista.omitidos > 0 ? t('campanias.aviso.omiten', { count: vista.omitidos }) : '',
    t('campanias.aviso.correo', { count: vista.conCorreo }),
    t('campanias.aviso.segundoPlano'),
    t('campanias.aviso.pausar'),
  ].filter(Boolean);

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 480, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-aviso-campania">
      <DialogTitle id="titulo-aviso-campania">
        {t('campanias.aviso.titulo')}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25 }}>
          {t('campanias.aviso.subtitulo')}
        </Typography>
      </DialogTitle>
      <DialogContent>
        <Box component="ul" sx={{ m: 0, pl: 2.5, display: 'grid', gap: 1 }}>
          {lineas.map((linea) => (
            <Typography key={linea} component="li">
              {linea}
            </Typography>
          ))}
        </Box>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" onClick={onConfirmar} disabled={ocupado} startIcon={ocupado ? <IconoCarga /> : <Send size={16} aria-hidden />}>
          {t('campanias.aviso.confirmar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
