import { Button, Chip, IconButton, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Tooltip, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { asignarRolesUsuario, crearUsuario, desactivarUsuario, SCOPES, type RolAsignadoDto, type UsuarioDto } from '@validasoft/api-client';
import { formatearFechaHora } from '@validasoft/ui';
import { Pencil, Plus, UserX } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAvisos } from '../../../shared/useAvisos';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useUsuarios } from './api';
import { DialogoRoles } from './DialogoRoles';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'roles'; usuario: UsuarioDto };

export function PaginaUsuarios() {
  const { t } = useTranslation();
  const { empresa, empresas } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.usuariosAdministrar);
  const consulta = useUsuarios(puede);
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [edicion, setEdicion] = useState<Edicion | null>(null);
  const [nombre, setNombre] = useState('');
  const [correo, setCorreo] = useState('');
  const [porDesactivar, setPorDesactivar] = useState<UsuarioDto | null>(null);

  const guardar = useMutation({
    mutationFn: (roles: RolAsignadoDto[]) => {
      if (edicion?.tipo === 'roles') return asignarRolesUsuario(edicion.usuario.id, { roles }, { empresa: null });
      return crearUsuario({ nombre, correo, identificacion: null, roles, identidades: null }, { empresa: null, idempotencyKey: edicion?.clave });
    },
    onSuccess: () => {
      mostrar(edicion?.tipo === 'roles' ? t('usuarios.rolesGuardados') : t('usuarios.creado'));
      setEdicion(null);
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/usuarios'),
  });

  const desactivar = useMutation({
    mutationFn: (usuario: UsuarioDto) => desactivarUsuario(usuario.id, { empresa: null }),
    onSuccess: () => mostrar(t('usuarios.desactivado')),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => {
      setPorDesactivar(null);
      return invalidarRuta(cliente, '/api/v1/usuarios');
    },
  });

  if (!puede) return <SinPermiso seccion={t('nav.usuarios')} />;

  const nombreEmpresa = (id: string | null) => (id ? (empresas.find((e) => e.id === id)?.nombre ?? id) : t('comun.todoElTenant'));
  const abrirCrear = () => {
    setNombre('');
    setCorreo('');
    guardar.reset();
    setEdicion({ tipo: 'crear', clave: crypto.randomUUID() });
  };

  return (
    <>
      <EncabezadoPagina
        titulo={t('usuarios.titulo')}
        subtitulo={t('usuarios.ayuda')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} />} onClick={abrirCrear}>
            {t('usuarios.nuevo')}
          </Button>
        }
      />
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 3 }}>
        <Table size="small" aria-label={t('usuarios.titulo')} aria-busy={consulta.isPending}>
          <TableHead>
            <TableRow>
              <TableCell>{t('usuarios.columnas.nombre')}</TableCell>
              <TableCell>{t('usuarios.columnas.roles')}</TableCell>
              <TableCell>{t('usuarios.columnas.identidades')}</TableCell>
              <TableCell>{t('usuarios.columnas.ultimoAcceso')}</TableCell>
              <TableCell align="right" />
            </TableRow>
          </TableHead>
          <TableBody>
            {consulta.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={5}>{t('usuarios.vacio')}</TableCell>
              </TableRow>
            )}
            {consulta.data?.map((u) => (
              <TableRow key={u.id} sx={{ opacity: u.activo ? 1 : 0.55 }}>
                <TableCell>
                  <Typography variant="body2">{u.nombre}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {u.correo}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                    {u.roles.length === 0 && <Chip size="small" variant="outlined" label={t('usuarios.sinRoles')} />}
                    {u.roles.map((r) => (
                      <Chip key={`${r.rol}|${r.empresaId ?? ''}`} size="small" label={`${t(`usuarios.roles.${r.rol}`)} · ${nombreEmpresa(r.empresaId)}`} />
                    ))}
                    {!u.activo && <Chip size="small" color="default" label={t('comun.inactivo')} />}
                  </Stack>
                </TableCell>
                <TableCell>
                  <Typography variant="mono" color="text.secondary">
                    {u.identidades.map((i) => i.idExterno).join(', ') || '—'}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="mono" color="text.secondary">
                    {u.ultimoAcceso ? formatearFechaHora(u.ultimoAcceso) : t('usuarios.nunca')}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  {u.activo && (
                    <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                      <Tooltip title={t('usuarios.editarRoles')}>
                        <IconButton
                          aria-label={`${t('usuarios.editarRoles')}: ${u.nombre}`}
                          onClick={() => {
                            guardar.reset();
                            setEdicion({ tipo: 'roles', usuario: u });
                          }}
                        >
                          <Pencil size={16} />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t('comun.desactivar')}>
                        <IconButton
                          aria-label={`${t('comun.desactivar')}: ${u.nombre}`}
                          onClick={() => setPorDesactivar(u)}
                        >
                          <UserX size={16} />
                        </IconButton>
                      </Tooltip>
                    </Stack>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {porDesactivar && (
        <DialogoConfirmacion
          titulo={t('comun.desactivar')}
          mensaje={t('usuarios.confirmarDesactivar', { nombre: porDesactivar.nombre })}
          confirmar={t('comun.desactivar')}
          ocupado={desactivar.isPending}
          onConfirmar={() => desactivar.mutate(porDesactivar)}
          onCerrar={() => setPorDesactivar(null)}
        />
      )}

      {edicion && (
        <DialogoRoles
          titulo={edicion.tipo === 'roles' ? t('usuarios.rolesDe', { nombre: edicion.usuario.nombre }) : t('usuarios.nuevo')}
          empresas={empresas}
          rolesIniciales={edicion.tipo === 'roles' ? edicion.usuario.roles : []}
          guardando={guardar.isPending}
          error={guardar.error}
          onGuardar={(roles) => guardar.mutate(roles)}
          onCerrar={() => setEdicion(null)}
          encabezado={
            edicion.tipo === 'crear' ? (
              <>
                <TextField required label={t('usuarios.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} />
                <TextField required type="email" label={t('usuarios.correo')} value={correo} onChange={(e) => setCorreo(e.target.value)} />
              </>
            ) : undefined
          }
        />
      )}
    </>
  );
}
