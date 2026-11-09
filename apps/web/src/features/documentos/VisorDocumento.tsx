import { Alert, Box, Button, CircularProgress, Dialog, IconButton, Stack, Typography } from '@mui/material';
import type { DatoDeclaradoDto, VigenciaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { Download, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../shared/errores';
import { tipoDeVisor } from './api';
import { ResumenVersion } from './ResumenVersion';
import { useArchivoDelVisor, type ContenidoDelVisor } from './useArchivoDelVisor';

/** Lo que el visor necesita de una versión: sirve desde la lista de documentos del vínculo y desde un requisito. */
export interface DocumentoEnVisor {
  documentoId: string;
  /** Nombre del tipo de documento («Registro Único Tributario»). */
  tipo: string;
  n: number;
  cargadaEn: string;
  /** Lo que declaró el tercero en la versión (TD-4); va al lado del archivo. */
  datosDeclarados?: readonly DatoDeclaradoDto[];
  vigencia?: VigenciaDto | null;
  /** Versión de solo formulario: el visor muestra solo los datos y no pide la descarga (409 `version-sin-archivo`). */
  sinArchivo?: boolean;
}

/**
 * Visor de PDF o imagen dentro de la app (VisorDocumento del design system, sin comparación todavía), con los datos
 * declarados y la vigencia en un panel al lado (debajo en pantallas angostas). Para una versión de solo formulario es
 * el mismo diálogo en modo «solo datos». Sin pantalla completa y con alto máximo en px: embebido, el iframe mide lo que
 * su contenido y `100%` o `vh` serían toda esa altura.
 */
export function VisorDocumento({ empresaId, vinculoId, documento, onCerrar }: { empresaId: string; vinculoId: string; documento: DocumentoEnVisor; onCerrar: () => void }) {
  const { t } = useTranslation();
  const conArchivo = !documento.sinArchivo;
  const { descarga, contenido, errorAlPedir } = useArchivoDelVisor(empresaId, vinculoId, documento.documentoId, documento.n, conArchivo);
  const datos = documento.datosDeclarados ?? [];
  const conPanel = datos.length > 0 || Boolean(documento.vigencia);
  const titulo = `${documento.tipo} · ${t('documentos.version', { n: String(documento.n), fecha: formatearFecha(documento.cargadaEn) })}`;
  const resumen = <ResumenVersion datos={datos} vigencia={documento.vigencia} conTitulo />;

  return (
    <Dialog
      open
      onClose={onCerrar}
      maxWidth={false}
      aria-labelledby="titulo-visor"
      slotProps={{
        paper: {
          sx: conArchivo
            ? { width: { xs: 'calc(100% - 16px)', md: conPanel ? 'min(1280px, 94vw)' : 'min(1100px, 92vw)' }, height: 'min(90vh, 820px)', m: { xs: 1, md: 4 } }
            : { width: 'min(640px, calc(100% - 16px))', maxHeight: 'min(90vh, 820px)', m: { xs: 1, md: 4 } },
        },
      }}
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', px: 2, py: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography id="titulo-visor" variant="h4" component="h2" noWrap>
            {titulo}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12 }} noWrap>
            {conArchivo ? (descarga?.nombre ?? ' ') : t('expedientes.datosDeclarados.formulario')}
          </Typography>
        </Box>
        {conArchivo && (
          <Button component="a" href={descarga?.url} download={descarga?.nombre} disabled={!descarga} variant="outlined" startIcon={<Download size={16} aria-hidden />}>
            {t('documentos.descargar')}
          </Button>
        )}
        <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar}>
          <X size={18} />
        </IconButton>
      </Stack>
      {conArchivo ? (
        <Box sx={{ flex: 1, minHeight: 0, display: 'grid', gridTemplateColumns: conPanel ? { xs: 'minmax(0,1fr)', md: 'minmax(0,1fr) 320px' } : 'minmax(0,1fr)', gridTemplateRows: conPanel ? { xs: 'minmax(0,1fr) auto', md: 'minmax(0,1fr)' } : 'minmax(0,1fr)' }}>
          <ArchivoEnVisor contenido={contenido} mime={descarga?.mime ?? null} titulo={titulo} errorAlPedir={errorAlPedir} />
          {conPanel && (
            <Box sx={{ p: 2, overflow: 'auto', maxHeight: { xs: 240, md: 'none' }, borderLeft: { md: 1 }, borderTop: { xs: 1, md: 0 }, borderColor: 'divider' }}>
              {resumen}
            </Box>
          )}
        </Box>
      ) : (
        <Stack spacing={2} sx={{ p: 2, overflow: 'auto' }}>
          <Alert severity="info">{t('expedientes.datosDeclarados.soloDatos')}</Alert>
          {resumen}
          {datos.length === 0 && <Typography sx={{ color: 'text.secondary' }}>{t('expedientes.datosDeclarados.vacio')}</Typography>}
        </Stack>
      )}
    </Dialog>
  );
}

/** El archivo: PDF en el visor del navegador, imagen, o el aviso de que solo se descarga. */
function ArchivoEnVisor({ contenido, mime, titulo, errorAlPedir }: { contenido: ContenidoDelVisor; mime: string | null; titulo: string; errorAlPedir: boolean }) {
  const { t } = useTranslation();
  // El tipo de archivo se sabe al pedir la descarga (`DescargaDto.mime`); mientras tanto se muestra la carga.
  const visor = mime ? tipoDeVisor(mime) : 'pdf';
  return (
    <Box sx={{ minHeight: 0, bgcolor: 'superficie.nivel2', display: 'grid', placeItems: 'center', overflow: 'auto' }}>
      {visor === 'otro' ? (
        <Typography sx={{ color: 'text.secondary', p: 3 }}>
          {t('documentos.sinVisor')}
        </Typography>
      ) : contenido.estado === 'cargando' ? (
        <Stack sx={{ alignItems: 'center' }} spacing={1} role="status">
          <CircularProgress size={24} aria-hidden />
          <Typography sx={{ color: 'text.secondary' }}>{t('documentos.cargando')}</Typography>
        </Stack>
      ) : contenido.estado === 'error' ? (
        <Alert severity="error" sx={{ m: 3 }}>
          {errorAlPedir ? mensajeDeError(contenido.error, t) : t('documentos.errorVisor')}
        </Alert>
      ) : visor === 'pdf' ? (
        <Box component="iframe" src={contenido.url} title={titulo} sx={{ width: '100%', height: '100%', border: 0, bgcolor: 'background.paper' }} />
      ) : (
        <Box component="img" src={contenido.url} alt={titulo} sx={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain', p: 2 }} />
      )}
    </Box>
  );
}
