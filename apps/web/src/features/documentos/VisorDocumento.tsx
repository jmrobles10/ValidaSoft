import { Alert, Box, Button, CircularProgress, Dialog, IconButton, Stack, Typography } from '@mui/material';
import type { DescargaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { Download, X } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../shared/errores';
import { tipoDeVisor, usePedirDescarga } from './api';

/** Lo que el visor necesita de una versión: sirve desde la lista de documentos del vínculo y desde un requisito. */
export interface DocumentoEnVisor {
  documentoId: string;
  /** Nombre del tipo de documento («Registro Único Tributario»). */
  tipo: string;
  n: number;
  cargadaEn: string;
}

type Contenido = { estado: 'cargando' } | { estado: 'listo'; url: string } | { estado: 'error'; error: unknown };

/**
 * Visor de PDF o imagen dentro de la app (VisorDocumento del design system, sin comparación todavía). La URL prefirmada
 * trae `Content-Disposition: attachment`: para verlo en línea se lee el archivo y se muestra desde un `blob:` con su
 * tipo (el visor de PDF del navegador o una imagen). «Descargar» usa la misma URL. Sin pantalla completa y con alto
 * máximo en px: embebido, el iframe mide lo que su contenido y `100%` o `vh` serían toda esa altura.
 */
export function VisorDocumento({ empresaId, vinculoId, documento, onCerrar }: { empresaId: string; vinculoId: string; documento: DocumentoEnVisor; onCerrar: () => void }) {
  const { t } = useTranslation();
  const pedir = usePedirDescarga(empresaId, vinculoId);
  const [descarga, setDescarga] = useState<DescargaDto | null>(null);
  const [contenido, setContenido] = useState<Contenido>({ estado: 'cargando' });
  const { mutateAsync } = pedir;

  useEffect(() => {
    // Al desmontar se cancela la lectura del archivo y se libera el `blob:`.
    const control = new AbortController();
    let objeto: string | null = null;
    // Función y no la propiedad directa: TypeScript no estrecha su valor entre los `await`.
    const abortado = () => control.signal.aborted;
    const cargar = async () => {
      const d = await mutateAsync({ documentoId: documento.documentoId, numero: documento.n });
      if (abortado()) return;
      setDescarga(d);
      if (tipoDeVisor(d.mime) === 'otro') return;
      const respuesta = await fetch(d.url, { signal: control.signal });
      if (!respuesta.ok) throw new Error(`HTTP ${String(respuesta.status)}`);
      const blob = new Blob([await respuesta.arrayBuffer()], { type: d.mime });
      if (abortado()) return;
      objeto = URL.createObjectURL(blob);
      setContenido({ estado: 'listo', url: objeto });
    };
    cargar().catch((error: unknown) => {
      if (!abortado()) setContenido({ estado: 'error', error });
    });
    return () => {
      control.abort();
      if (objeto) URL.revokeObjectURL(objeto);
    };
  }, [mutateAsync, documento.documentoId, documento.n]);

  // El tipo de archivo se sabe al pedir la descarga (`DescargaDto.mime`); mientras tanto se muestra la carga.
  const visor = descarga ? tipoDeVisor(descarga.mime) : 'pdf';
  const titulo = `${documento.tipo} · ${t('documentos.version', { n: String(documento.n), fecha: formatearFecha(documento.cargadaEn) })}`;

  return (
    <Dialog
      open
      onClose={onCerrar}
      maxWidth={false}
      aria-labelledby="titulo-visor"
      slotProps={{ paper: { sx: { width: { xs: 'calc(100% - 16px)', md: 'min(1100px, 92vw)' }, height: 'min(90vh, 820px)', m: { xs: 1, md: 4 } } } }}
    >
      <Stack direction="row" alignItems="center" spacing={1} sx={{ px: 2, py: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography id="titulo-visor" variant="h4" component="h2" noWrap>
            {titulo}
          </Typography>
          <Typography color="text.secondary" sx={{ fontSize: 12 }} noWrap>
            {descarga?.nombre ?? ' '}
          </Typography>
        </Box>
        <Button component="a" href={descarga?.url} download={descarga?.nombre} disabled={!descarga} variant="outlined" startIcon={<Download size={16} aria-hidden />}>
          {t('documentos.descargar')}
        </Button>
        <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar}>
          <X size={18} />
        </IconButton>
      </Stack>
      <Box sx={{ flex: 1, minHeight: 0, bgcolor: 'superficie.nivel2', display: 'grid', placeItems: 'center', overflow: 'auto' }}>
        {visor === 'otro' ? (
          <Typography color="text.secondary" sx={{ p: 3 }}>
            {t('documentos.sinVisor')}
          </Typography>
        ) : contenido.estado === 'cargando' ? (
          <Stack alignItems="center" spacing={1} role="status">
            <CircularProgress size={24} aria-hidden />
            <Typography color="text.secondary">{t('documentos.cargando')}</Typography>
          </Stack>
        ) : contenido.estado === 'error' ? (
          <Alert severity="error" sx={{ m: 3 }}>
            {pedir.isError ? mensajeDeError(contenido.error, t) : t('documentos.errorVisor')}
          </Alert>
        ) : visor === 'pdf' ? (
          <Box component="iframe" src={contenido.url} title={titulo} sx={{ width: '100%', height: '100%', border: 0, bgcolor: 'background.paper' }} />
        ) : (
          <Box component="img" src={contenido.url} alt={titulo} sx={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain', p: 2 }} />
        )}
      </Box>
    </Dialog>
  );
}
