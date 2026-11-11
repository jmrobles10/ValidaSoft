import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography, useMediaQuery, useTheme } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarContactosDelTercero, getListarVinculosPublicosQueryKey, type VinculoPublicoDto } from '@validasoft/api-client';
import { aEditables, desdeEditables, EditorDeContactos, ROLES_DE_CONTACTO, validarContactos, type ContactoEditable } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';

/**
 * Mis contactos con esta empresa (IO-3): el mismo tercero tiene contactos distintos para cada empresa. La lista que se guarda
 * reemplaza a la anterior y es la que la empresa y sus sistemas ven (comercial, pagos, cumplimiento, notificaciones).
 */
export function DialogoContactos({ vinculo, onCerrar }: { vinculo: VinculoPublicoDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const tema = useTheme();
  const pantallaCompleta = useMediaQuery(tema.breakpoints.down('sm'));
  const mostrar = useAvisos((s) => s.mostrar);
  const cliente = useQueryClient();
  const [contactos, setContactos] = useState<ContactoEditable[]>(() => aEditables(vinculo.contactos));
  const [intentado, setIntentado] = useState(false);
  const errores = validarContactos(contactos, { nombre: t('contactos.errores.nombre'), correo: t('contactos.errores.correo'), repetido: t('contactos.errores.repetido') });

  const guardar = useMutation({
    mutationFn: () => actualizarContactosDelTercero(vinculo.id, { contactos: desdeEditables(contactos) }),
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: getListarVinculosPublicosQueryKey() });
      mostrar(t('contactos.exito', { empresa: vinculo.empresa.nombre }));
      onCerrar();
    },
  });

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (Object.keys(errores).length > 0) return;
    guardar.mutate();
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" fullScreen={pantallaCompleta} aria-labelledby="titulo-contactos">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-contactos">{t('contactos.titulo', { empresa: vinculo.empresa.nombre })}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography sx={{ color: 'text.secondary' }}>{t('contactos.ayuda', { empresa: vinculo.empresa.nombre })}</Typography>
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            <EditorDeContactos
              contactos={contactos}
              onCambiar={setContactos}
              roles={ROLES_DE_CONTACTO.map((r) => ({ valor: r, etiqueta: t(`contactos.roles.${r}`) }))}
              etiquetas={{
                nombre: t('contactos.nombre'),
                cargo: t('contactos.cargo'),
                correo: t('contactos.correo'),
                telefono: t('contactos.telefono'),
                rol: t('contactos.rol'),
                agregar: t('contactos.agregar'),
                quitar: (n) => t('contactos.quitar', { n }),
                vacio: t('contactos.vacio', { empresa: vinculo.empresa.nombre }),
                contacto: (n) => t('contactos.contacto', { n }),
              }}
              errores={intentado ? errores : {}}
              deshabilitado={guardar.isPending}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending}>
            {t('contactos.guardar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
