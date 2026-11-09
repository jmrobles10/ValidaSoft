import { Alert, Box, Button, Drawer, IconButton, MenuItem, Stack, TextField, Tooltip, Typography } from '@mui/material';
import type { EmpresaDto, RolAsignadoDto, UsuarioDto } from '@validasoft/api-client';
import { AvatarActor, AlineadoConCampo } from '@validasoft/ui';
import { Ban, Plus, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { aFilas, ALCANCE_TENANT, aRoles, ROLES, type FilaRol } from './roles';

export interface DrawerRolesProps {
  /** `null` = persona nueva (pide nombre y correo). */
  usuario: UsuarioDto | null;
  empresas: readonly EmpresaDto[];
  guardando: boolean;
  error: unknown;
  onGuardar: (datos: { nombre: string; correo: string; roles: RolAsignadoDto[] }) => void;
  onDesactivar?: () => void;
  onCerrar: () => void;
}

/** Editor lateral de roles por empresa o para todo el grupo (ID-2); también da de alta a una persona. */
export function DrawerRoles({ usuario, empresas, guardando, error, onGuardar, onDesactivar, onCerrar }: DrawerRolesProps) {
  const { t } = useTranslation();
  const [nombre, setNombre] = useState('');
  const [correo, setCorreo] = useState('');
  const [filas, setFilas] = useState<FilaRol[]>(() => aFilas(usuario?.roles ?? []));

  const cambiar = (indice: number, cambios: Partial<FilaRol>) => setFilas((actuales) => actuales.map((f, i) => (i === indice ? { ...f, ...cambios } : f)));
  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    onGuardar({ nombre: nombre.trim(), correo: correo.trim(), roles: aRoles(filas) });
  };

  return (
    <Drawer anchor="right" open onClose={guardando ? undefined : onCerrar} slotProps={{ paper: { sx: { width: { xs: '100%', sm: 480 } }, 'aria-labelledby': 'titulo-roles' } }}>
      <Box component="form" onSubmit={enviar} sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <Stack direction="row" spacing={1.5} alignItems="flex-start" sx={{ px: 3, pt: 2.5, pb: 1 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="h3" component="h2" id="titulo-roles">
            {usuario ? t('usuarios.editarRoles') : t('usuarios.nuevo')}
          </Typography>
          {usuario && (
            <Typography variant="body2" color="text.secondary">
              {usuario.nombre}
            </Typography>
          )}
        </Box>
        <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar} disabled={guardando}>
          <X size={16} />
        </IconButton>
      </Stack>

      <Stack spacing={2.5} sx={{ px: 3, py: 1.5, flex: 1, overflowY: 'auto' }}>
        {usuario ? (
          <Stack direction="row" spacing={1.5} alignItems="center">
            <AvatarActor tipo="persona" nombre={usuario.nombre} tamano={40} />
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontWeight: 600 }}>{usuario.nombre}</Typography>
              <Typography variant="body2" color="text.secondary" noWrap>
                {usuario.correo}
              </Typography>
            </Box>
          </Stack>
        ) : (
          <>
            <TextField required label={t('usuarios.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} />
            <TextField required type="email" label={t('usuarios.correo')} value={correo} onChange={(e) => setCorreo(e.target.value)} />
          </>
        )}

        {error !== null && error !== undefined && <Alert severity="error">{mensajeDeError(error, t)}</Alert>}

        <Box component="fieldset" sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
          <Typography component="legend" sx={{ fontSize: 13, fontWeight: 500, mb: 1 }}>
            {t('usuarios.rolesPorEmpresa')}
          </Typography>
          <Stack spacing={1}>
            {filas.length === 0 && (
              <Typography variant="body2" color="text.secondary">
                {t('usuarios.sinRolesAyuda')}
              </Typography>
            )}
            {filas.map((fila, indice) => (
              <Stack key={indice} direction="row" spacing={1} alignItems="flex-start">
                <TextField
                  select
                  label={indice === 0 ? t('usuarios.alcance') : undefined}
                  aria-label={t('usuarios.alcance')}
                  value={fila.alcance}
                  onChange={(e) => cambiar(indice, { alcance: e.target.value })}
                  sx={{ flex: 1.3, minWidth: 0 }}
                >
                  <MenuItem value={ALCANCE_TENANT}>{t('comun.todoElTenant')}</MenuItem>
                  {empresas.map((e) => (
                    <MenuItem key={e.id} value={e.id}>
                      {e.nombre}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  select
                  label={indice === 0 ? t('usuarios.rol') : undefined}
                  aria-label={t('usuarios.rol')}
                  value={fila.rol}
                  onChange={(e) => cambiar(indice, { rol: e.target.value as FilaRol['rol'] })}
                  sx={{ flex: 1, minWidth: 0 }}
                >
                  {ROLES.map((r) => (
                    <MenuItem key={r} value={r}>
                      {t(`usuarios.roles.${r}`)}
                    </MenuItem>
                  ))}
                </TextField>
                {/* Solo la primera fila lleva etiquetas: la X se centra en el control, no en la etiqueta. */}
                <AlineadoConCampo conEtiqueta={indice === 0}>
                  <Tooltip title={t('usuarios.quitarRol')}>
                    <IconButton size="small" aria-label={t('usuarios.quitarRol')} onClick={() => setFilas((a) => a.filter((_, i) => i !== indice))}>
                      <X size={16} />
                    </IconButton>
                  </Tooltip>
                </AlineadoConCampo>
              </Stack>
            ))}
          </Stack>
          <Button
            variant="text"
            startIcon={<Plus size={16} aria-hidden />}
            onClick={() => setFilas((a) => [...a, { rol: 'Revisor', alcance: empresas[0]?.id ?? ALCANCE_TENANT }])}
            sx={{ mt: 1 }}
          >
            {t('usuarios.agregarRol')}
          </Button>
        </Box>

        <Alert severity="info">{t('usuarios.aplicanAlIngresar')}</Alert>

        {usuario?.activo && onDesactivar && (
          <Stack
            direction="row"
            spacing={1.5}
            alignItems="center"
            justifyContent="space-between"
            sx={{ px: 1.75, py: 1.5, border: 1, borderColor: 'estado.error.borde', borderRadius: (th) => `${String(th.radio.md)}px` }}
          >
            <Box>
              <Typography sx={{ fontWeight: 600 }}>{t('usuarios.desactivarA', { nombre: usuario.nombre })}</Typography>
              <Typography variant="body2" color="text.secondary">
                {t('usuarios.desactivarAyuda')}
              </Typography>
            </Box>
            <Button variant="outlined" color="error" startIcon={<Ban size={16} aria-hidden />} onClick={onDesactivar} sx={{ flex: 'none', color: 'estado.error.texto', borderColor: 'estado.error.borde' }}>
              {t('comun.desactivar')}
            </Button>
          </Stack>
        )}
      </Stack>

      <Stack direction="row" spacing={1} justifyContent="flex-end" sx={{ px: 3, py: 2, borderTop: 1, borderColor: 'divider' }}>
        <Button variant="text" onClick={onCerrar} disabled={guardando} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button type="submit" variant="contained" disabled={guardando} startIcon={guardando ? <IconoCarga /> : undefined}>
          {usuario ? t('usuarios.guardarRoles') : t('usuarios.crear')}
        </Button>
      </Stack>
      </Box>
    </Drawer>
  );
}
