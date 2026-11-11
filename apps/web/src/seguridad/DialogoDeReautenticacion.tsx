import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { confirmarReautenticacion, obtenerOpcionesReautenticacion } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { Fingerprint } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { esCO } from '../i18n/es-CO';
import { mensajeDeIngreso } from '../ingreso/mensajes';
import { firmarConPasskey } from '../ingreso/passkeys';
import { IconoCarga } from '../shared/componentes';
import { resolverReautenticacion, rutaDeMiCuenta, useReautenticacion, type PeticionDeReautenticacion } from './reautenticacion';

const SIN_EMPRESA = { empresa: null } as const;

/** La acción con palabras («rotar una API key»); una acción nueva de la API se dice de forma genérica. */
function textoDeAccion(accion: string, t: TFunction): string {
  return accion in esCO.reautenticacion.acciones ? t(`reautenticacion.acciones.${accion as keyof typeof esCO.reautenticacion.acciones}`) : t('reautenticacion.accionGenerica');
}

function Contenido({ peticion }: { peticion: PeticionDeReautenticacion }) {
  const { t } = useTranslation();
  const confirmar = useMutation({
    mutationFn: async () => {
      const { desafio, opciones } = await obtenerOpcionesReautenticacion(SIN_EMPRESA);
      return confirmarReautenticacion({ desafio, respuesta: await firmarConPasskey(opciones) }, SIN_EMPRESA);
    },
    onSuccess: () => resolverReautenticacion(true),
  });
  const cancelar = () => resolverReautenticacion(false);
  const accion = textoDeAccion(peticion.accion, t);

  if (!peticion.tienePasskey)
    return (
      <>
        <DialogTitle id="titulo-reautenticacion">{t('reautenticacion.sinPasskeyTitulo')}</DialogTitle>
        <DialogContent>
          <Typography>{t('reautenticacion.sinPasskeyTexto', { accion })}</Typography>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={cancelar} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button
            variant="contained"
            onClick={() => {
              resolverReautenticacion(false);
              window.location.hash = rutaDeMiCuenta(window.location.hash);
            }}
          >
            {t('reautenticacion.irAMiCuenta')}
          </Button>
        </DialogActions>
      </>
    );

  return (
    <>
      <DialogTitle id="titulo-reautenticacion">{t('reautenticacion.titulo')}</DialogTitle>
      <DialogContent>
        <Typography sx={{ mb: 1 }}>{t('reautenticacion.texto', { accion })}</Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('reautenticacion.ayuda', { count: peticion.minutos })}
        </Typography>
        {confirmar.error !== null && (
          <Alert severity="error" role="alert" sx={{ mt: 2 }}>
            {mensajeDeIngreso(confirmar.error, t)}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={cancelar} disabled={confirmar.isPending} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" onClick={() => confirmar.mutate()} disabled={confirmar.isPending} startIcon={confirmar.isPending ? <IconoCarga /> : <Fingerprint size={16} aria-hidden />}>
          {confirmar.isPending ? t('reautenticacion.esperando') : t('reautenticacion.confirmar')}
        </Button>
      </DialogActions>
    </>
  );
}

/**
 * El diálogo que pide la passkey antes de una acción sensible (PI-13). Va montado una sola vez en la app: lo abre el cliente de la API cuando una petición
 * responde `reautenticacion-requerida`, y al confirmar esa petición se repite sola.
 */
export function DialogoDeReautenticacion() {
  const peticion = useReautenticacion((s) => s.peticion);
  if (!peticion) return null;
  return (
    <Dialog open onClose={() => resolverReautenticacion(false)} maxWidth={false} slotProps={{ paper: { sx: { width: 440, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-reautenticacion">
      <Contenido peticion={peticion} />
    </Dialog>
  );
}
