import { Box, Button, Typography } from '@mui/material';
import type { AdjuntoDeCumplimientoDto, PropositoDeAdjunto } from '@validasoft/api-client';
import { FileUp } from 'lucide-react';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useSubirAdjunto } from '../api';
import { ACEPTA_ADJUNTO, errorDeArchivo } from '../modelo';

export interface SelectorDeArchivoProps {
  empresaId: string;
  expedienteId: string;
  proposito: PropositoDeAdjunto;
  etiqueta: string;
  /** Recibe el archivo ya subido y confirmado (queda en el expediente; el estudio o la consulta lo citan por su id). */
  onSubido: (adjunto: AdjuntoDeCumplimientoDto) => void;
  deshabilitado?: boolean;
  variante?: 'outlined' | 'text';
}

/**
 * Botón que sube un archivo del oficial en los dos pasos de siempre (URL prefirmada y confirmación). Solo PDF o imagen de hasta
 * 15 MB: lo demás se avisa antes de subir; el servidor vuelve a revisar el contenido.
 */
export function SelectorDeArchivo({ empresaId, expedienteId, proposito, etiqueta, onSubido, deshabilitado = false, variante = 'outlined' }: SelectorDeArchivoProps) {
  const { t } = useTranslation();
  const entrada = useRef<HTMLInputElement>(null);
  const subir = useSubirAdjunto(empresaId, expedienteId);
  const [aviso, setAviso] = useState<string | null>(null);

  const elegir = (archivo: File | undefined) => {
    setAviso(null);
    if (!archivo) return;
    const problema = errorDeArchivo(archivo);
    if (problema) {
      setAviso(problema === 'tipo' ? t('cumplimiento.archivos.errorTipo') : t('cumplimiento.archivos.errorTamano'));
      return;
    }
    subir.mutate({ archivo, proposito }, { onSuccess: onSubido, onError: (e) => setAviso(mensajeDeErrorDeSubida(e, archivo.name, t)) });
  };

  return (
    <Box>
      <input
        ref={entrada}
        hidden
        type="file"
        accept={ACEPTA_ADJUNTO}
        data-testid={`entrada-${proposito}`}
        onChange={(e) => {
          elegir(e.target.files?.[0]);
          e.target.value = '';
        }}
      />
      <Button
        size="small"
        variant={variante}
        startIcon={subir.isPending ? <IconoCarga /> : <FileUp size={16} aria-hidden />}
        disabled={deshabilitado || subir.isPending}
        onClick={() => entrada.current?.click()}
      >
        {subir.isPending ? t('cumplimiento.archivos.subiendo') : etiqueta}
      </Button>
      {aviso && (
        <Typography role="alert" sx={{ color: 'error.main', fontSize: 12, mt: 0.5 }}>
          {aviso}
        </Typography>
      )}
    </Box>
  );
}

/** Un error de la API (adjunto inválido, demasiados archivos…) con su texto; una falla de la carga directa, el genérico de la subida. */
function mensajeDeErrorDeSubida(error: unknown, nombre: string, t: Parameters<typeof mensajeDeError>[1]): string {
  return error instanceof Error && error.message.startsWith('carga-fallida') ? t('cumplimiento.archivos.errorSubida', { nombre }) : mensajeDeError(error, t);
}
