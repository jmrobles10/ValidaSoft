import { Box, Button, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { asignarRolesUsuario, crearUsuario, desactivarUsuario, getListarSistemasQueryOptions, SCOPES, type RolAsignadoDto, type UsuarioDto } from '@validasoft/api-client';
import { AvatarActor, BarraFiltros, CampoBusqueda, EstadoVacio, FiltroMenu, FilasEsqueleto, formatearFechaHora, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Ban, CircleCheck, Pencil, Plus, UserX } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { MenuFila } from '../../../shared/MenuFila';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useUsuarios } from './api';
import { DrawerRoles } from './DrawerRoles';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'roles'; usuario: UsuarioDto };

/** Estado de la cuenta: texto con ícono (no es un estado canónico del dominio, así que no usa ChipEstado). */
function EstadoCuenta({ activa }: { activa: boolean }) {
  const { t } = useTranslation();
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 13, '& svg': { color: activa ? 'estado.exito.texto' : 'text.secondary' } }}>
      {activa ? <CircleCheck size={14} strokeWidth={1.75} aria-hidden /> : <Ban size={14} strokeWidth={1.75} aria-hidden />}
      {activa ? t('usuarios.activa') : t('usuarios.desactivada')}
    </Box>
  );
}

export function PaginaUsuarios() {
  const { t } = useTranslation();
  const { empresa, empresas } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.usuariosAdministrar);
  const consulta = useUsuarios(puede);
  const sistemas = useQuery({ ...getListarSistemasQueryOptions({ request: { empresa: null } }), enabled: permisos.puede(SCOPES.sistemasAdministrar) });
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [edicion, setEdicion] = useState<Edicion | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<UsuarioDto | null>(null);
  const [busqueda, setBusqueda] = useState('');
  const [estado, setEstado] = useState('activas');

  const guardar = useMutation({
    mutationFn: ({ nombre, correo, roles }: { nombre: string; correo: string; roles: RolAsignadoDto[] }) => {
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
    onSuccess: () => {
      mostrar(t('usuarios.desactivado'));
      setEdicion(null);
    },
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => {
      setPorDesactivar(null);
      return invalidarRuta(cliente, '/api/v1/usuarios');
    },
  });

  const codigoSistema = useMemo(() => new Map((sistemas.data ?? []).map((s) => [s.id, s.codigo])), [sistemas.data]);
  const visibles = useMemo(() => {
    const q = busqueda.trim().toLowerCase();
    return (consulta.data ?? []).filter(
      (u) => (estado === '' || (estado === 'activas') === u.activo) && (!q || `${u.nombre} ${u.correo}`.toLowerCase().includes(q)),
    );
  }, [consulta.data, busqueda, estado]);

  if (!puede) return <SinPermiso seccion={t('nav.usuarios')} />;

  const nombreEmpresa = (id: string | null) => (id ? (empresas.find((e) => e.id === id)?.nombre ?? id) : t('comun.todoElTenant'));
  const abrirRoles = (usuario: UsuarioDto) => {
    guardar.reset();
    setEdicion({ tipo: 'roles', usuario });
  };

  return (
    <>
      <EncabezadoPagina
        titulo={t('usuarios.titulo')}
        subtitulo={t('usuarios.ayuda')}
        acciones={
          <Button
            variant="contained"
            startIcon={<Plus size={16} aria-hidden />}
            onClick={() => {
              guardar.reset();
              setEdicion({ tipo: 'crear', clave: crypto.randomUUID() });
            }}
          >
            {t('usuarios.nuevo')}
          </Button>
        }
      />
      <BarraFiltros>
        <CampoBusqueda valor={busqueda} onCambiar={setBusqueda} placeholder={t('usuarios.buscar')} ancho={280} />
        <FiltroMenu
          etiqueta={t('usuarios.columnas.estado')}
          textoTodos={t('comun.todas')}
          valor={estado}
          onCambiar={setEstado}
          opciones={[
            { valor: 'activas', etiqueta: t('usuarios.activas') },
            { valor: 'desactivadas', etiqueta: t('usuarios.desactivadas') },
          ]}
        />
      </BarraFiltros>
      {consulta.isError && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('usuarios.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('usuarios.columnas.persona')}</TableCell>
                <TableCell>{t('usuarios.columnas.roles')}</TableCell>
                <TableCell sx={{ width: 120 }}>{t('usuarios.columnas.entraDesde')}</TableCell>
                <TableCell sx={{ width: 170 }}>{t('usuarios.columnas.ultimoAcceso')}</TableCell>
                <TableCell sx={{ width: 130 }}>{t('usuarios.columnas.estado')}</TableCell>
                <TableCell sx={{ width: 48 }}>
                  <Box component="span" sx={visualmenteOculto}>
                    {t('comun.acciones')}
                  </Box>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={6} alto={48} />}
              {consulta.data && visibles.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto titulo={consulta.data.length === 0 ? t('usuarios.vacio') : t('usuarios.sinCoincidencias')} />
                  </TableCell>
                </TableRow>
              )}
              {visibles.map((u) => (
                <TableRow key={u.id}>
                  <TableCell sx={{ height: 48 }}>
                    <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
                      <AvatarActor tipo="persona" nombre={u.nombre} />
                      <Box sx={{ minWidth: 0 }}>
                        <Typography noWrap sx={{ fontWeight: 500 }}>
                          {u.nombre}
                        </Typography>
                        <Typography noWrap color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px' }}>
                          {u.correo}
                        </Typography>
                      </Box>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    {u.roles.length === 0 ? (
                      <Typography component="span" variant="body2" color="text.secondary">
                        {t('usuarios.sinRoles')}
                      </Typography>
                    ) : (
                      <Box sx={{ display: 'flex', flexWrap: 'wrap', columnGap: 1.5, rowGap: 0.5 }}>
                        {u.roles.map((r) => (
                          <Box component="span" key={`${r.rol}|${r.empresaId ?? ''}`} sx={{ display: 'inline-flex', flexDirection: 'column', fontSize: 13, lineHeight: '16px' }}>
                            <span style={{ fontWeight: 500 }}>{t(`usuarios.roles.${r.rol}`)}</span>
                            <Typography component="span" color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px' }}>
                              {nombreEmpresa(r.empresaId)}
                            </Typography>
                          </Box>
                        ))}
                      </Box>
                    )}
                  </TableCell>
                  <TableCell>{u.identidades.map((i) => codigoSistema.get(i.sistemaId) ?? '—').join(', ') || '—'}</TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{u.ultimoAcceso ? formatearFechaHora(u.ultimoAcceso) : t('usuarios.nunca')}</Typography>
                  </TableCell>
                  <TableCell>
                    <EstadoCuenta activa={u.activo} />
                  </TableCell>
                  <TableCell align="right">
                    <MenuFila
                      etiqueta={t('comun.accionesDe', { nombre: u.nombre })}
                      acciones={
                        u.activo
                          ? [
                              { etiqueta: t('usuarios.editarRoles'), icono: Pencil, onClick: () => abrirRoles(u) },
                              { etiqueta: t('comun.desactivar'), icono: UserX, destructiva: true, onClick: () => setPorDesactivar(u) },
                            ]
                          : []
                      }
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {porDesactivar && (
        <DialogoConfirmacion
          titulo={t('usuarios.desactivarA', { nombre: porDesactivar.nombre })}
          subtitulo={porDesactivar.correo}
          mensaje={t('usuarios.desactivarAyuda')}
          confirmar={t('comun.desactivar')}
          icono={<Ban size={16} aria-hidden />}
          ocupado={desactivar.isPending}
          onConfirmar={() => desactivar.mutate(porDesactivar)}
          onCerrar={() => setPorDesactivar(null)}
        />
      )}

      {edicion && (
        <DrawerRoles
          key={edicion.tipo === 'roles' ? edicion.usuario.id : edicion.clave}
          usuario={edicion.tipo === 'roles' ? edicion.usuario : null}
          empresas={empresas}
          guardando={guardar.isPending}
          error={guardar.error}
          onGuardar={(datos) => guardar.mutate(datos)}
          onDesactivar={edicion.tipo === 'roles' ? () => setPorDesactivar(edicion.usuario) : undefined}
          onCerrar={() => setEdicion(null)}
        />
      )}
    </>
  );
}
