import { Box, Button, Stack, Typography } from '@mui/material';
import { Camera, CloudUpload, FileUp } from 'lucide-react';
import { useRef, useState, type ChangeEvent, type DragEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { aceptaDe, aceptaFotos, textoReglas } from '../tipoDocumento/reglas';
import { REGLAS_GLOBALES, type ReglasArchivo } from './subida';

/**
 * Zona de subida móvil (Dropzone del design system): «Tomar foto» abre la cámara trasera (`capture="environment"`),
 * «Elegir archivo» la galería o los archivos, y en escritorio se puede arrastrar. Botones cómodos de 44 px. Con las
 * reglas del tipo: dice qué admite, el selector solo ofrece esos formatos y sin imágenes admitidas no hay cámara.
 */
export function Dropzone({
  titulo,
  deshabilitado,
  onArchivo,
  reglas = REGLAS_GLOBALES,
}: {
  titulo: string;
  deshabilitado?: boolean;
  onArchivo: (archivo: File) => void;
  reglas?: ReglasArchivo;
}) {
  const { t } = useTranslation();
  const fotos = aceptaFotos(reglas);
  const camara = useRef<HTMLInputElement>(null);
  const archivos = useRef<HTMLInputElement>(null);
  const [encima, setEncima] = useState(false);

  const alElegir = (evento: ChangeEvent<HTMLInputElement>) => {
    const archivo = evento.target.files?.[0];
    evento.target.value = ''; // permite elegir el mismo archivo otra vez tras un error
    if (archivo) onArchivo(archivo);
  };

  const alSoltar = (evento: DragEvent) => {
    evento.preventDefault();
    setEncima(false);
    const archivo = evento.dataTransfer.files[0];
    if (archivo && !deshabilitado) onArchivo(archivo);
  };

  return (
    <Box
      component="section"
      aria-label={titulo}
      onDragOver={(e) => {
        e.preventDefault();
        if (!deshabilitado) setEncima(true);
      }}
      onDragLeave={() => setEncima(false)}
      onDrop={alSoltar}
      sx={(th) => ({
        border: '1.5px dashed',
        borderColor: encima ? 'primary.main' : 'divider',
        borderRadius: `${String(th.radio.lg)}px`,
        bgcolor: encima ? 'superficie.nivel2' : 'background.paper',
        p: 2.5,
        opacity: deshabilitado ? 0.6 : 1,
      })}
    >
      <Stack alignItems="center" spacing={0.75} sx={{ textAlign: 'center', mb: 2 }}>
        <Box sx={{ color: 'text.secondary' }} aria-hidden>
          <CloudUpload size={28} />
        </Box>
        <Typography sx={{ fontWeight: 600, fontSize: 16 }}>{titulo}</Typography>
        <Typography color="text.secondary" sx={{ fontSize: 14 }}>
          {textoReglas(reglas, t)}
        </Typography>
      </Stack>
      <Stack spacing={1}>
        {fotos && (
          <Button variant="contained" size="large" fullWidth startIcon={<Camera size={20} aria-hidden />} disabled={deshabilitado} onClick={() => camara.current?.click()}>
            {t('subir.tomarFoto')}
          </Button>
        )}
        <Button
          variant={fotos ? 'outlined' : 'contained'}
          size="large"
          fullWidth
          startIcon={<FileUp size={20} aria-hidden />}
          disabled={deshabilitado}
          onClick={() => archivos.current?.click()}
        >
          {t('subir.elegirArchivo')}
        </Button>
        <Typography color="text.secondary" sx={{ fontSize: 13, textAlign: 'center', display: { xs: 'none', md: 'block' } }}>
          {t('subir.arrastra')}
        </Typography>
      </Stack>
      {fotos && <input ref={camara} type="file" accept={fotos} capture="environment" hidden onChange={alElegir} data-testid="entrada-camara" />}
      <input ref={archivos} type="file" accept={aceptaDe(reglas)} hidden onChange={alElegir} data-testid="entrada-archivo" />
    </Box>
  );
}
