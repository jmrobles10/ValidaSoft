import { Box, Button, Typography } from '@mui/material';
import { Camera, CloudUpload, FileUp } from 'lucide-react';
import { useRef, useState, type ChangeEvent, type DragEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { aceptaDe, aceptaFotos, textoReglas } from '../tipoDocumento/reglas';
import { REGLAS_GLOBALES, type ReglasArchivo } from './subida';

/**
 * Zona de subida móvil (Dropzone del design system): «Tomar foto» abre la cámara trasera (`capture="environment"`),
 * «Elegir archivo» la galería o los archivos, y en escritorio se puede arrastrar. Zona baja (ícono al lado del texto,
 * límites en una línea) y acciones de tamaño natural: en móvil se reparten una fila de 44 px; en escritorio, 36 px y sin
 * estirarse. Con las reglas del tipo: dice qué admite, el selector solo ofrece esos formatos y sin imágenes no hay cámara.
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
      sx={{ display: 'flex', flexDirection: 'column', gap: 1, opacity: deshabilitado ? 0.6 : 1 }}
    >
      <Box
        sx={(th) => ({
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          py: 1.25,
          px: 1.5,
          border: '1.5px dashed',
          borderColor: encima ? 'primary.main' : th.palette.borde.control,
          borderRadius: `${String(th.radio.md)}px`,
          bgcolor: encima ? 'superficie.nivel2' : 'background.paper',
        })}
      >
        <Box sx={{ flex: 'none', color: 'primary.main', display: 'flex' }} aria-hidden>
          <CloudUpload size={22} />
        </Box>
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px' }}>{titulo}</Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {textoReglas(reglas, t)}
            <Box component="span" sx={{ display: { xs: 'none', md: 'inline' } }}>
              {' · '}
              {t('subir.arrastra')}
            </Box>
          </Typography>
        </Box>
      </Box>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, '& > .MuiButton-root': { flex: { xs: '1 1 0', sm: '0 1 auto' }, minWidth: 0 } }}>
        {fotos && (
          <Button variant="contained" size="large" startIcon={<Camera size={18} aria-hidden />} disabled={deshabilitado} onClick={() => camara.current?.click()}>
            {t('subir.tomarFoto')}
          </Button>
        )}
        <Button
          variant={fotos ? 'outlined' : 'contained'}
          size="large"
          startIcon={<FileUp size={18} aria-hidden />}
          disabled={deshabilitado}
          onClick={() => archivos.current?.click()}
        >
          {t('subir.elegirArchivo')}
        </Button>
      </Box>
      {fotos && <input ref={camara} type="file" accept={fotos} capture="environment" hidden onChange={alElegir} data-testid="entrada-camara" />}
      <input ref={archivos} type="file" accept={aceptaDe(reglas)} hidden onChange={alElegir} data-testid="entrada-archivo" />
    </Box>
  );
}
