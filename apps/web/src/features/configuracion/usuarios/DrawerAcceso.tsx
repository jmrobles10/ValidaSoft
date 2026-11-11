import { Box, Drawer, IconButton, Stack, Typography } from '@mui/material';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cerrarSesionUsuario,
  getListarPasskeysUsuarioQueryOptions,
  getListarSesionesUsuarioQueryOptions,
  revocarPasskeyUsuario,
  type UsuarioDto,
} from '@validasoft/api-client';
import { AvatarActor } from '@validasoft/ui';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { invalidarRuta } from '../../../shared/consultas';
import { PanelDeAcceso } from './PanelDeAcceso';

/** Passkeys y sesiones de una persona del grupo (ING-5): lo que un administrador revoca o cierra cuando pierde un dispositivo. */
export function DrawerAcceso({ usuario, onCerrar }: { usuario: UsuarioDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const passkeys = useQuery(getListarPasskeysUsuarioQueryOptions(usuario.id, { request: { empresa: null } }));
  const sesiones = useQuery({ ...getListarSesionesUsuarioQueryOptions(usuario.id, { request: { empresa: null } }), refetchOnWindowFocus: true });

  return (
    <Drawer anchor="right" open onClose={onCerrar} slotProps={{ paper: { sx: { width: { xs: '100%', sm: 520 } }, 'aria-labelledby': 'titulo-acceso' } }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start', px: 3, pt: 2.5, pb: 1 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="h3" component="h2" id="titulo-acceso">
            {t('accesoUsuario.titulo', { nombre: usuario.nombre })}
          </Typography>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mt: 0.5 }}>
            <AvatarActor tipo="persona" nombre={usuario.nombre} tamano={24} />
            <Typography variant="body2" noWrap sx={{ color: 'text.secondary' }}>
              {usuario.correo}
            </Typography>
          </Stack>
        </Box>
        <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar}>
          <X size={16} />
        </IconButton>
      </Stack>
      <Box sx={{ px: 3, py: 1.5, overflowY: 'auto', flex: 1 }}>
        <PanelDeAcceso
          persona={usuario.nombre}
          exigePasskey={usuario.exigePasskey ?? false}
          passkeys={passkeys}
          sesiones={sesiones}
          onRevocar={(passkeyId, motivo) => revocarPasskeyUsuario(usuario.id, passkeyId, { motivo }, { empresa: null })}
          onCerrarSesion={(sesionId) => cerrarSesionUsuario(usuario.id, sesionId, { empresa: null })}
          alCambiar={() => invalidarRuta(cliente, '/api/v1/usuarios')}
        />
      </Box>
    </Drawer>
  );
}
