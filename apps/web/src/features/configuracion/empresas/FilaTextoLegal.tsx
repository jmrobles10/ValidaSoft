import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material';
import type { TextoLegalDto } from '@validasoft/api-client';
import { FaltaParaEnviar, formatearFecha } from '@validasoft/ui';
import { Eye, EyeOff, FilePen, Send } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { usePublicarTexto } from './api';
import { LONGITUD_MAXIMA_TEXTO, validarTexto, type RutaTexto } from './modelo';

type Modo = 'cerrado' | 'ver' | 'editar';

/**
 * Un texto legal de la empresa (autorización o términos): versión vigente y, para quien puede escribir, un editor que
 * publica una versión nueva. Publicar pide confirmación porque los terceros deberán aceptarla de nuevo.
 */
export function FilaTextoLegal({ empresaId, empresaNombre, ruta, vigente, puedeEscribir }: { empresaId: string; empresaNombre: string; ruta: RutaTexto; vigente: TextoLegalDto | null; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [modo, setModo] = useState<Modo>('cerrado');
  const [contenido, setContenido] = useState('');
  const [confirmando, setConfirmando] = useState(false);
  const publicar = usePublicarTexto(empresaId, ruta);
  const nombre = t(`empresasGrupo.textos.tipos.${ruta}`);
  const siguiente = (vigente?.version ?? 0) + 1;
  const error = validarTexto(contenido, vigente);

  const editar = () => {
    publicar.reset();
    setContenido(vigente?.contenido ?? '');
    setModo('editar');
  };
  const confirmar = () =>
    publicar.mutate(contenido, {
      onSuccess: (texto) => {
        mostrar(t('empresasGrupo.textos.publicado', { version: String(texto.version), texto: nombre }));
        setModo('cerrado');
      },
      onSettled: () => setConfirmando(false),
    });

  return (
    <Box component="li" sx={{ py: 1, '& + &': { borderTop: 1, borderColor: 'divider' } }}>
      <Stack direction="row" alignItems="center" spacing={1.5} sx={{ minHeight: 32 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography component="h4" sx={{ fontSize: 14, fontWeight: 500 }}>
            {nombre}
          </Typography>
          {vigente ? (
            <Typography variant="body2" color="text.secondary">
              {t('empresasGrupo.textos.version', { version: String(vigente.version) })} ·{' '}
              <Typography component="span" variant="monoTabla" color="text.secondary">
                {t('empresasGrupo.textos.vigenteDesde', { fecha: formatearFecha(vigente.publicadoEn) })}
              </Typography>
            </Typography>
          ) : (
            <Typography variant="body2" sx={{ color: 'estado.advertencia.texto' }}>
              {t('empresasGrupo.textos.faltante')}
            </Typography>
          )}
        </Box>
        {vigente && modo !== 'editar' && (
          <Button variant="text" size="small" startIcon={modo === 'ver' ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />} onClick={() => setModo(modo === 'ver' ? 'cerrado' : 'ver')} aria-expanded={modo === 'ver'}>
            {modo === 'ver' ? t('empresasGrupo.textos.ocultar') : t('empresasGrupo.textos.ver')}
          </Button>
        )}
        {puedeEscribir && modo !== 'editar' && (
          <Button variant="text" size="small" startIcon={<FilePen size={16} aria-hidden />} onClick={editar}>
            {vigente ? t('empresasGrupo.textos.publicarNueva') : t('empresasGrupo.textos.publicarPrimera')}
          </Button>
        )}
      </Stack>

      {modo === 'ver' && vigente && (
        <Box sx={{ mt: 1, px: 1.5, py: 1.25, bgcolor: 'superficie.nivel2', borderRadius: (th) => `${String(th.radio.sm)}px`, maxHeight: 240, overflow: 'auto' }}>
          <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
            {vigente.contenido}
          </Typography>
        </Box>
      )}

      {modo === 'editar' && (
        <Stack spacing={1.5} sx={{ mt: 1 }}>
          {vigente && <Alert severity="warning">{t('empresasGrupo.textos.avisoReaceptar')}</Alert>}
          {!vigente && <Alert severity="info">{t('empresasGrupo.textos.faltanteAyuda')}</Alert>}
          {publicar.isError && <Alert severity="error">{mensajeDeError(publicar.error, t)}</Alert>}
          <TextField
            multiline
            minRows={6}
            maxRows={16}
            label={t('empresasGrupo.textos.contenido', { version: String(siguiente) })}
            value={contenido}
            onChange={(e) => setContenido(e.target.value)}
            helperText={t('empresasGrupo.textos.caracteres', { n: contenido.trim().length.toLocaleString('es-CO'), maximo: LONGITUD_MAXIMA_TEXTO.toLocaleString('es-CO') })}
          />
          <Stack direction="row" spacing={1} alignItems="center" justifyContent="flex-end">
            {error && <FaltaParaEnviar>{t(`empresasGrupo.textos.errores.${error}`)}</FaltaParaEnviar>}
            <Button variant="text" onClick={() => setModo('cerrado')} disabled={publicar.isPending} sx={{ color: 'text.primary' }}>
              {t('comun.cancelar')}
            </Button>
            <Button variant="contained" startIcon={<Send size={16} aria-hidden />} disabled={error !== null || publicar.isPending} onClick={() => setConfirmando(true)}>
              {t('empresasGrupo.textos.publicar', { version: String(siguiente) })}
            </Button>
          </Stack>
        </Stack>
      )}

      {confirmando && (
        <DialogoConfirmacion
          tono="primary"
          titulo={t('empresasGrupo.textos.confirmarTitulo', { version: String(siguiente), texto: nombre })}
          subtitulo={empresaNombre}
          mensaje={vigente ? t('empresasGrupo.textos.confirmarNueva', { empresa: empresaNombre }) : t('empresasGrupo.textos.confirmarPrimera', { empresa: empresaNombre })}
          confirmar={t('empresasGrupo.textos.publicar', { version: String(siguiente) })}
          icono={<Send size={16} aria-hidden />}
          ocupado={publicar.isPending}
          onConfirmar={confirmar}
          onCerrar={() => setConfirmando(false)}
        />
      )}
    </Box>
  );
}
