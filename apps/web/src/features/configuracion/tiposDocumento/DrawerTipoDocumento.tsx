import { Alert, Box, Button, Drawer, IconButton, Stack, Typography } from '@mui/material';
import type { TipoDocumentoTenantDto } from '@validasoft/api-client';
import { FaltaParaEnviar } from '@validasoft/ui';
import { X } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useCanonicos, useGuardarTipo } from './api';
import { EditorCampos } from './EditorCampos';
import { borradorDe, borradorNuevo, validarBorrador, type BorradorTipo, type ErrorBorrador } from './modelo';
import { SeccionArchivo, SeccionIdentidad, SeccionVigencia } from './SeccionesTipo';
import { SeccionPlantilla } from './SeccionPlantilla';

/**
 * Crear o editar un tipo de documento. El botón de guardar queda deshabilitado mientras falte algo (dice qué); los
 * errores de cada campo salen cuando ya se tocó. Las reglas que la API rechaza (más laxas que el canónico, sobre el
 * global…) llegan con su `detail` en español.
 */
export function DrawerTipoDocumento({ tipoDocumento, claveIdempotencia, onCerrar }: { tipoDocumento: TipoDocumentoTenantDto | null; claveIdempotencia: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const creando = tipoDocumento === null;
  const [borrador, setBorrador] = useState<BorradorTipo>(() => (tipoDocumento ? borradorDe(tipoDocumento) : borradorNuevo()));
  const [tocados, setTocados] = useState<ReadonlySet<string>>(new Set());
  const canonicos = useCanonicos(true);
  const guardar = useGuardarTipo(tipoDocumento, claveIdempotencia);
  const errores = validarBorrador(borrador, creando);
  const canonico = canonicos.data?.find((c) => c.codigo === borrador.tipoCanonico);

  const cambiar = (parcial: Partial<BorradorTipo>) => {
    setBorrador((b) => ({ ...b, ...parcial }));
    setTocados((previos) => new Set([...previos, ...Object.keys(parcial)]));
  };
  const mensaje = (e: ErrorBorrador) => t(`tiposDocumento.errores.${e.codigo}`);
  const error = (campo: string) => {
    const e = errores.find((x) => x.campo === campo);
    return e && tocados.has(campo) ? mensaje(e) : null;
  };
  const errorDeCampo = (indice: number) => (tocados.has('campos') ? errores.find((e) => e.campo === 'campo' && e.indice === indice) : undefined);
  const primero = errores[0];
  const pendiente = primero ? (primero.campo === 'campo' ? t('tiposDocumento.errores.enCampo', { n: String(primero.indice + 1), error: mensaje(primero) }) : mensaje(primero)) : null;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (pendiente) return;
    guardar.mutate(borrador, {
      onSuccess: (tipo) => {
        mostrar(creando ? t('tiposDocumento.creado', { nombre: tipo.nombre }) : t('tiposDocumento.guardado'));
        onCerrar();
      },
    });
  };

  return (
    <Drawer anchor="right" open onClose={guardar.isPending ? undefined : onCerrar} slotProps={{ paper: { sx: { width: 600, maxWidth: '100%' } } }} aria-labelledby="titulo-tipo-documento">
      <Box component="form" onSubmit={enviar} noValidate sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Typography id="titulo-tipo-documento" variant="h4" component="h2">
            {creando ? t('tiposDocumento.nuevo') : t('tiposDocumento.editarNombre', { nombre: tipoDocumento.nombre })}
          </Typography>
          <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar} disabled={guardar.isPending}>
            <X size={18} strokeWidth={1.75} />
          </IconButton>
        </Stack>
        <Box sx={{ p: 2.5, flex: 1, overflowY: 'auto' }}>
          {guardar.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {mensajeDeError(guardar.error, t)}
            </Alert>
          )}
          <SeccionIdentidad borrador={borrador} cambiar={cambiar} error={error} creando={creando} canonicos={canonicos.data ?? []} />
          <EditorCampos borrador={borrador} cambiar={cambiar} error={error} erroresCampos={errorDeCampo} />
          {borrador.modo !== 'Formulario' && (
            <SeccionArchivo borrador={borrador} cambiar={cambiar} error={error} canonico={canonico} descripcion={tipoDocumento?.reglasEfectivas?.descripcion ?? null} />
          )}
          <SeccionVigencia borrador={borrador} cambiar={cambiar} error={error} />
          {tipoDocumento && borrador.modo !== 'Formulario' && <SeccionPlantilla tipoId={tipoDocumento.id} />}
        </Box>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'flex-end', px: 2.5, py: 1.5, borderTop: 1, borderColor: 'divider' }}>
          {pendiente && <FaltaParaEnviar>{pendiente}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending || pendiente !== null} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
            {creando ? t('tiposDocumento.crear') : t('comun.guardar')}
          </Button>
        </Stack>
      </Box>
    </Drawer>
  );
}
