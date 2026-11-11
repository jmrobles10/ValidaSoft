import { Box, Divider, IconButton, ListItemIcon, ListItemText, Menu, MenuItem, Tooltip, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { getObtenerMiCuentaQueryOptions } from '@validasoft/api-client';
import { AvatarActor } from '@validasoft/ui';
import { LogOut, UserRound } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { cerrarSesionDirecta } from '../../sesion/sesionDirecta';
import { useSesion } from '../../sesion/sesionStore';

/**
 * Menú de la persona que entró directo (ING-3): su nombre y correo, «Mi cuenta» y «Cerrar sesión». No aparece con la identidad delegada: ahí la
 * persona se autentica y sale en el sistema integrado.
 */
export function MenuDeCuenta({ rutaMiCuenta }: { rutaMiCuenta: string }) {
  const sesion = useSesion((s) => s.sesion);
  if (sesion?.sesionId == null) return null;
  return <MenuDeLaPersona rutaMiCuenta={rutaMiCuenta} nombreEnSesion={sesion.nombre ?? ''} />;
}

// Aparte para que la consulta de la cuenta solo exista cuando la persona entró directo (con la identidad delegada no hay nada que pedir).
function MenuDeLaPersona({ rutaMiCuenta, nombreEnSesion }: { rutaMiCuenta: string; nombreEnSesion: string }) {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const cuenta = useQuery({ ...getObtenerMiCuentaQueryOptions({ request: { empresa: null } }), staleTime: 5 * 60_000 });
  const nombre = cuenta.data?.nombre ?? nombreEnSesion;

  return (
    <>
      <Tooltip title={t('cuenta.menu')}>
        <IconButton
          aria-label={t('cuenta.menu')}
          aria-haspopup="menu"
          aria-expanded={ancla !== null}
          onClick={(evento) => setAncla(evento.currentTarget)}
          sx={{ p: 0.5 }}
        >
          <AvatarActor tipo="persona" nombre={nombre} tamano={28} />
        </IconButton>
      </Tooltip>
      <Menu anchorEl={ancla} open={ancla !== null} onClose={() => setAncla(null)} slotProps={{ paper: { sx: { minWidth: 240 } } }}>
        <Box sx={{ px: 2, py: 1 }}>
          <Typography noWrap sx={{ fontWeight: 600 }}>
            {nombre}
          </Typography>
          {cuenta.data?.correo && (
            <Typography noWrap variant="body2" sx={{ color: 'text.secondary' }}>
              {cuenta.data.correo}
            </Typography>
          )}
        </Box>
        <Divider />
        <MenuItem
          onClick={() => {
            setAncla(null);
            void navegar(rutaMiCuenta);
          }}
        >
          <ListItemIcon>
            <UserRound size={16} strokeWidth={1.75} aria-hidden />
          </ListItemIcon>
          <ListItemText>{t('nav.mi-cuenta')}</ListItemText>
        </MenuItem>
        <MenuItem
          onClick={() => {
            setAncla(null);
            void cerrarSesionDirecta();
          }}
        >
          <ListItemIcon>
            <LogOut size={16} strokeWidth={1.75} aria-hidden />
          </ListItemIcon>
          <ListItemText>{t('cuenta.salir')}</ListItemText>
        </MenuItem>
      </Menu>
    </>
  );
}
