import { Alert, Box, LinearProgress, Stack, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../../shared/errores';
import { useVistaPrevia } from './api';
import type { BorradorPlantilla } from './modelo';

type Formato = 'html' | 'texto';

/**
 * Vista previa en vivo del correo con datos de ejemplo. El HTML va en un `<iframe sandbox="">` (sin scripts, sin
 * formularios, origen opaco); la alternativa en texto plano es la que leen los clientes de correo sin HTML.
 */
export function VistaPreviaCorreo({ empresaId, evento, nombre, borrador }: { empresaId: string; evento: string; nombre: string; borrador: BorradorPlantilla }) {
  const { t } = useTranslation();
  const [formato, setFormato] = useState<Formato>('html');
  const previa = useVistaPrevia(empresaId, evento, borrador);
  const datos = previa.data;

  return (
    <Stack spacing={1.25} component="section" aria-labelledby="titulo-vista-previa">
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Box>
          <Typography id="titulo-vista-previa" variant="h5" component="h3">
            {t('plantillas.vistaPrevia')}
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('plantillas.vistaPreviaAyuda')}
          </Typography>
        </Box>
        <ToggleButtonGroup size="small" exclusive value={formato} onChange={(_, valor: Formato | null) => valor && setFormato(valor)} aria-label={t('plantillas.formato.etiqueta')}>
          <ToggleButton value="html">{t('plantillas.formato.html')}</ToggleButton>
          <ToggleButton value="texto">{t('plantillas.formato.texto')}</ToggleButton>
        </ToggleButtonGroup>
      </Stack>
      <Box sx={{ height: 4 }}>{previa.isFetching && <LinearProgress aria-label={t('plantillas.vistaPreviaCargando')} />}</Box>
      {previa.isError && (
        <Alert severity="warning">
          {t('plantillas.vistaPreviaError')} {mensajeDeError(previa.error, t)}
        </Alert>
      )}
      {datos && (
        <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, overflow: 'hidden', opacity: previa.isPlaceholderData ? 0.6 : 1 }}>
          <Typography data-testid="asunto-previa" sx={{ px: 1.5, py: 1, borderBottom: 1, borderColor: 'divider', bgcolor: 'action.hover', fontSize: 13 }}>
            {t('plantillas.asuntoPrevia', { asunto: datos.asunto })}
          </Typography>
          {formato === 'html' ? (
            <Box
              component="iframe"
              sandbox=""
              srcDoc={documentoDe(datos.html)}
              title={t('plantillas.marcoPrevia', { nombre })}
              sx={{ display: 'block', width: '100%', height: 320, border: 0, bgcolor: '#fff' }}
            />
          ) : (
            <Box component="pre" sx={{ m: 0, p: 1.5, maxHeight: 320, overflow: 'auto', whiteSpace: 'pre-wrap', fontFamily: 'inherit', fontSize: 13 }}>
              {datos.texto}
            </Box>
          )}
        </Box>
      )}
    </Stack>
  );
}

/** Documento mínimo para el marco: tipografía legible y el HTML del correo tal como sale (ya escapado por la API). */
const documentoDe = (html: string) =>
  `<!doctype html><html lang="es"><head><meta charset="utf-8"><style>body{font:14px/1.5 system-ui,sans-serif;color:#1f2328;margin:16px}</style></head><body>${html}</body></html>`;
