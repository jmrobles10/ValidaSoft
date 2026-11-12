import { Alert, Box, Button, Drawer, IconButton, Stack, TextField, Typography } from '@mui/material';
import type { AyudaDeDocumentoDto } from '@validasoft/api-client';
import { Undo2, X } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useGuardarAyudaDeDocumento } from './api';
import { cambioDeAyuda, MAXIMO_AYUDA, textoParaGuardar } from './modelo';

interface Props {
  empresaId: string;
  empresaNombre: string;
  ayuda: AyudaDeDocumentoDto;
  puedeEscribir: boolean;
  onCerrar: () => void;
}

/** Ayuda de un tipo de documento: la del grupo (solo lectura, atenuada) y la de la empresa (editable, hasta 2000 caracteres). */
export function DrawerAyuda({ empresaId, empresaNombre, ayuda, puedeEscribir, onCerrar }: Props) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useGuardarAyudaDeDocumento(empresaId);
  const [texto, setTexto] = useState(ayuda.ayudaDeLaEmpresa ?? '');
  const [volviendo, setVolviendo] = useState(false);
  const cambio = cambioDeAyuda(ayuda.ayudaDeLaEmpresa, texto);
  const tienePropia = Boolean(ayuda.ayudaDeLaEmpresa?.trim());
  const ocupado = guardar.isPending;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!puedeEscribir || !cambio) return;
    guardar.mutate(
      { tipoDocumentoId: ayuda.tipoDocumentoId, texto: textoParaGuardar(texto) },
      {
        onSuccess: () => {
          mostrar(textoParaGuardar(texto) === null ? t('ayudas.drawer.vueltaAlGrupo') : t('ayudas.drawer.guardada'));
          onCerrar();
        },
      },
    );
  };

  const volverALaDelGrupo = () =>
    guardar.mutate(
      { tipoDocumentoId: ayuda.tipoDocumentoId, texto: null },
      {
        onSuccess: () => {
          mostrar(t('ayudas.drawer.vueltaAlGrupo'));
          onCerrar();
        },
        onSettled: () => setVolviendo(false),
      },
    );

  return (
    <Drawer
      anchor="right"
      open
      onClose={ocupado ? undefined : onCerrar}
      slotProps={{ paper: { role: 'dialog', 'aria-modal': true, 'aria-labelledby': 'titulo-ayuda', sx: { width: 480, maxWidth: '100%' } } }}
    >
      <Box component="form" onSubmit={enviar} noValidate sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Box sx={{ minWidth: 0 }}>
            <Typography id="titulo-ayuda" variant="h4" component="h2">
              {t('ayudas.drawer.titulo', { nombre: ayuda.nombre })}
            </Typography>
            <Typography variant="monoTabla" sx={{ color: 'text.secondary' }}>
              {ayuda.codigo}
            </Typography>
          </Box>
          <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar} disabled={ocupado}>
            <X size={18} strokeWidth={1.75} />
          </IconButton>
        </Stack>

        <Stack spacing={2.5} sx={{ p: 2.5, flex: 1, overflowY: 'auto' }}>
          {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
          <Box component="section" aria-labelledby="titulo-ayuda-grupo">
            <Typography id="titulo-ayuda-grupo" variant="body2" component="h3" sx={{ mb: 0.75, color: 'text.secondary' }}>
              {t('ayudas.drawer.delGrupo')}
            </Typography>
            <Box
              sx={{
                p: 1.5,
                border: 1,
                borderColor: 'divider',
                borderRadius: (th) => `${String(th.radio.md)}px`,
                bgcolor: 'superficie.nivel2',
                color: 'text.secondary',
                whiteSpace: 'pre-wrap',
                minHeight: 56,
              }}
            >
              <Typography variant="body2" sx={{ color: 'inherit', fontStyle: ayuda.ayudaDelGrupo ? 'normal' : 'italic' }}>
                {ayuda.ayudaDelGrupo ?? t('ayudas.drawer.grupoSinAyuda')}
              </Typography>
            </Box>
            <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 0.5 }}>
              {t('ayudas.drawer.delGrupoAyuda')}
            </Typography>
          </Box>

          <TextField
            label={t('ayudas.drawer.deLaEmpresa', { empresa: empresaNombre })}
            value={texto}
            onChange={(e) => setTexto(e.target.value.slice(0, MAXIMO_AYUDA))}
            disabled={!puedeEscribir || ocupado}
            multiline
            minRows={6}
            placeholder={t('ayudas.drawer.placeholder')}
            helperText={
              <Box component="span" sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}>
                <span>{puedeEscribir ? t('ayudas.drawer.deLaEmpresaAyuda') : t('ayudas.soloLectura')}</span>
                <span aria-live="polite">{t('plantillas.contador', { actual: String(texto.length), maximo: String(MAXIMO_AYUDA) })}</span>
              </Box>
            }
          />
        </Stack>

        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', px: 2.5, py: 1.5, borderTop: 1, borderColor: 'divider' }}>
          {puedeEscribir && tienePropia && (
            <Button variant="text" startIcon={<Undo2 size={16} aria-hidden />} onClick={() => setVolviendo(true)} disabled={ocupado} sx={{ color: 'text.primary', mr: 'auto' }}>
              {t('ayudas.drawer.volver')}
            </Button>
          )}
          <Box sx={{ flex: 1 }} />
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {puedeEscribir ? t('comun.cancelar') : t('comun.cerrar')}
          </Button>
          {puedeEscribir && (
            <Button type="submit" variant="contained" disabled={!cambio || ocupado} startIcon={ocupado ? <IconoCarga /> : undefined}>
              {t('comun.guardar')}
            </Button>
          )}
        </Stack>
      </Box>
      {volviendo && (
        <DialogoConfirmacion
          tono="primary"
          titulo={t('ayudas.drawer.volverTitulo')}
          mensaje={t(ayuda.ayudaDelGrupo ? 'ayudas.drawer.volverMensaje' : 'ayudas.drawer.volverMensajeSinGrupo', { empresa: empresaNombre, nombre: ayuda.nombre })}
          confirmar={t('ayudas.drawer.volverConfirmar')}
          ocupado={ocupado}
          onConfirmar={volverALaDelGrupo}
          onCerrar={() => setVolviendo(false)}
        />
      )}
    </Drawer>
  );
}
