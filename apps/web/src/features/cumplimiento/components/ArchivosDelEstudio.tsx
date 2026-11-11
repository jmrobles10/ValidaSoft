import { Box, IconButton, Stack, Typography } from '@mui/material';
import type { AdjuntoDeCumplimientoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { FileText, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { formatearPeso } from '../modelo';
import { SelectorDeArchivo } from './SelectorDeArchivo';

export interface ArchivosDelEstudioProps {
  empresaId: string;
  expedienteId: string;
  /** Los archivos subidos que irán con el estudio. */
  archivos: readonly AdjuntoDeCumplimientoDto[];
  onSubido: (adjunto: AdjuntoDeCumplimientoDto) => void;
  /** Quita uno de los que aún no se envían (el archivo queda en el expediente; solo deja de citarse en el estudio). */
  onQuitar: (id: string) => void;
}

/** Archivos del estudio: el oficial sube los que soportan su concepto y ve cuáles irán citados al registrarlo. */
export function ArchivosDelEstudio({ empresaId, expedienteId, archivos, onSubido, onQuitar }: ArchivosDelEstudioProps) {
  const { t } = useTranslation();
  return (
    <Stack spacing={1} component="section" aria-label={t('cumplimiento.archivos.titulo')}>
      <Box>
        <Typography sx={{ fontSize: 13, fontWeight: 500 }}>{t('cumplimiento.archivos.titulo')}</Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('cumplimiento.archivos.ayuda')}</Typography>
      </Box>
      {archivos.length > 0 && (
        <Stack component="ul" spacing={0.75} sx={{ listStyle: 'none', p: 0, m: 0 }}>
          {archivos.map((a) => (
            <Stack
              key={a.id}
              component="li"
              direction="row"
              spacing={1}
              sx={{ alignItems: 'center', border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, px: 1.25, py: 0.75 }}
            >
              <FileText size={16} aria-hidden />
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 14, overflowWrap: 'anywhere' }}>{a.nombre}</Typography>
                <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('cumplimiento.archivos.pesoYFecha', { peso: formatearPeso(a.tamano), fecha: formatearFecha(a.fecha) })}</Typography>
              </Box>
              <IconButton size="small" aria-label={t('cumplimiento.archivos.quitar', { nombre: a.nombre })} onClick={() => onQuitar(a.id)}>
                <Trash2 size={16} aria-hidden />
              </IconButton>
            </Stack>
          ))}
        </Stack>
      )}
      <Box>
        <SelectorDeArchivo empresaId={empresaId} expedienteId={expedienteId} proposito="Estudio" etiqueta={t('cumplimiento.archivos.subir')} onSubido={onSubido} />
      </Box>
    </Stack>
  );
}
