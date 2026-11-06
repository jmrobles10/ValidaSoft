import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  Skeleton,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarArea, crearArea, definirMiembrosArea, SCOPES, type AreaDto, type UsuarioDto } from '@validasoft/api-client';
import { AvatarActor, EstadoVacio, MarcoTabla } from '@validasoft/ui';
import { ChevronRight, Pencil, Plus, Users, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, IconoCarga, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useUsuarios } from '../usuarios/api';
import { useAreas } from './api';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'editar'; area: AreaDto };

/** Áreas revisoras de la empresa activa (X-Empresa): lista a la izquierda, área elegida y sus miembros a la derecha. */
export function PaginaAreas() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = useAreas(empresa.id, puedeLeer);
  // Los nombres de los miembros solo se ven si la persona puede listar usuarios.
  const usuarios = useUsuarios(permisos.puede(SCOPES.usuariosAdministrar));
  const [elegidaId, setElegidaId] = useState<string | null>(null);
  const [edicion, setEdicion] = useState<Edicion | null>(null);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.areas')} />;

  const areas = consulta.data ?? [];
  const elegida = areas.find((a) => a.id === elegidaId) ?? areas[0] ?? null;

  return (
    <>
      <EncabezadoPagina titulo={t('areas.titulo', { empresa: empresa.nombre })} subtitulo={t('areas.ayuda')} />
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', md: '280px minmax(0,1fr)' }, gap: 2 }}>
          <Skeleton variant="rounded" height={180} />
          <Skeleton variant="rounded" height={260} />
        </Box>
      )}
      {consulta.data && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', md: '280px minmax(0,1fr)' }, gap: 2, alignItems: 'start' }}>
          <Tarjeta etiqueta={t('areas.lista')}>
            {areas.length === 0 ? (
              <EstadoVacio compacto icono={Users} titulo={t('areas.vacio')} />
            ) : (
              <Box component="ul" sx={{ m: 0, mb: 1, p: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '2px' }}>
                {areas.map((area) => {
                  const actual = area.id === elegida?.id;
                  return (
                    <li key={area.id}>
                      <Box
                        component="button"
                        type="button"
                        aria-current={actual ? 'true' : undefined}
                        onClick={() => setElegidaId(area.id)}
                        sx={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: 1,
                          width: '100%',
                          py: 1,
                          pl: 1.5,
                          pr: 1,
                          border: 0,
                          borderRadius: (th) => `${String(th.radio.sm)}px`,
                          bgcolor: actual ? 'acento.tinte' : 'transparent',
                          color: area.activa ? 'text.primary' : 'text.secondary',
                          font: 'inherit',
                          textAlign: 'left',
                          cursor: 'pointer',
                          '&:hover': { bgcolor: actual ? 'acento.tinte' : 'superficie.nivel2' },
                        }}
                      >
                        <Box component="span" sx={{ flex: 1, fontWeight: 500, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {area.nombre}
                        </Box>
                        <Typography component="span" variant="monoTabla" color="text.secondary">
                          {area.activa ? t('areas.nMiembros', { count: area.miembros.length }) : t('comun.inactiva')}
                        </Typography>
                        <Box component="span" sx={{ display: 'flex', color: 'text.secondary' }}>
                          <ChevronRight size={16} strokeWidth={1.75} aria-hidden />
                        </Box>
                      </Box>
                    </li>
                  );
                })}
              </Box>
            )}
            {puedeEscribir && (
              <Button variant="text" startIcon={<Plus size={16} aria-hidden />} onClick={() => setEdicion({ tipo: 'crear', clave: crypto.randomUUID() })}>
                {t('areas.nueva')}
              </Button>
            )}
          </Tarjeta>
          {elegida && (
            <DetalleArea
              key={elegida.id}
              empresaId={empresa.id}
              empresaNombre={empresa.nombre}
              area={elegida}
              usuarios={usuarios.data}
              puedeEscribir={puedeEscribir}
              onEditar={() => setEdicion({ tipo: 'editar', area: elegida })}
            />
          )}
        </Box>
      )}
      {edicion && <DialogoArea empresaId={empresa.id} edicion={edicion} onCerrar={() => setEdicion(null)} onCreada={setElegidaId} />}
    </>
  );
}

function DetalleArea({
  empresaId,
  empresaNombre,
  area,
  usuarios,
  puedeEscribir,
  onEditar,
}: {
  empresaId: string;
  empresaNombre: string;
  area: AreaDto;
  usuarios: UsuarioDto[] | undefined;
  puedeEscribir: boolean;
  onEditar: () => void;
}) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [porAgregar, setPorAgregar] = useState<UsuarioDto | null>(null);

  const guardar = useMutation({
    mutationFn: (miembros: string[]) => definirMiembrosArea(area.id, { usuarios: miembros }, { empresa: empresaId }),
    onSuccess: () => mostrar(t('areas.miembrosGuardados')),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => invalidarRuta(cliente, '/api/v1/areas'),
  });

  const usuarioDe = (id: string) => usuarios?.find((u) => u.id === id);
  const rolEnEmpresa = (u: UsuarioDto | undefined) => {
    const rol = u?.roles.find((r) => r.empresaId === empresaId) ?? u?.roles.find((r) => r.empresaId === null);
    return rol ? t(`usuarios.roles.${rol.rol}`) : '—';
  };
  const candidatos = (usuarios ?? []).filter(
    (u) => u.activo && !area.miembros.includes(u.id) && u.roles.some((r) => r.empresaId === null || r.empresaId === empresaId),
  );
  const editable = puedeEscribir && Boolean(usuarios) && area.activa;

  return (
    <Tarjeta
      etiqueta={area.nombre}
      titulo={
        <>
          {area.nombre}
          <Typography component="span" variant="mono" color="text.secondary" sx={{ ml: 1, fontWeight: 400 }}>
            {area.codigo}
          </Typography>
        </>
      }
      acciones={
        puedeEscribir && (
          <Button variant="outlined" startIcon={<Pencil size={16} aria-hidden />} onClick={onEditar}>
            {t('areas.editar')}
          </Button>
        )
      }
    >
      {!area.activa && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {t('areas.inactivaAyuda')}
        </Alert>
      )}
      <Typography variant="overline" component="h3" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
        {t('areas.miembros')}
      </Typography>
      {editable && (
        <Stack direction="row" spacing={1} sx={{ mb: 1.5 }}>
          <Autocomplete
            options={candidatos}
            value={porAgregar}
            onChange={(_e, valor) => setPorAgregar(valor)}
            getOptionLabel={(u) => `${u.nombre} (${u.correo})`}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            noOptionsText={t('areas.sinCandidatos')}
            renderInput={(params) => <TextField {...params} placeholder={t('areas.agregarMiembro')} slotProps={{ htmlInput: { ...params.inputProps, 'aria-label': t('areas.agregarMiembro') } }} />}
            sx={{ flex: 1, maxWidth: 420 }}
          />
          <Button
            variant="outlined"
            disabled={!porAgregar || guardar.isPending}
            startIcon={guardar.isPending ? <IconoCarga /> : <Plus size={16} aria-hidden />}
            onClick={() => {
              if (!porAgregar) return;
              guardar.mutate([...area.miembros, porAgregar.id], { onSuccess: () => setPorAgregar(null) });
            }}
          >
            {t('areas.agregar')}
          </Button>
        </Stack>
      )}
      <MarcoTabla>
        {area.miembros.length === 0 ? (
          <EstadoVacio compacto icono={Users} titulo={t('areas.sinMiembros')} texto={t('areas.sinMiembrosAyuda')} />
        ) : (
          <Table size="small" aria-label={t('areas.miembros')}>
            <TableHead>
              <TableRow>
                <TableCell>{t('usuarios.columnas.persona')}</TableCell>
                <TableCell sx={{ width: 240 }}>{t('areas.rolEn', { empresa: empresaNombre })}</TableCell>
                {editable && <TableCell sx={{ width: 48 }} />}
              </TableRow>
            </TableHead>
            <TableBody>
              {area.miembros.map((id) => {
                const u = usuarioDe(id);
                const nombre = u?.nombre ?? t('areas.personaSinAcceso', { id: id.slice(0, 8) });
                return (
                  <TableRow key={id}>
                    <TableCell>
                      <Stack direction="row" spacing={1.25} alignItems="center">
                        <AvatarActor tipo="persona" nombre={u?.nombre ?? '?'} />
                        <span style={{ fontWeight: 500 }}>{nombre}</span>
                      </Stack>
                    </TableCell>
                    <TableCell>{rolEnEmpresa(u)}</TableCell>
                    {editable && (
                      <TableCell align="right">
                        <Tooltip title={t('areas.quitarDe', { nombre, area: area.nombre })}>
                          <IconButton
                            aria-label={t('areas.quitarDe', { nombre, area: area.nombre })}
                            disabled={guardar.isPending}
                            onClick={() => guardar.mutate(area.miembros.filter((m) => m !== id))}
                          >
                            <X size={16} />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    )}
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </MarcoTabla>
    </Tarjeta>
  );
}

function DialogoArea({ empresaId, edicion, onCerrar, onCreada }: { empresaId: string; edicion: Edicion; onCerrar: () => void; onCreada: (id: string) => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const area = edicion.tipo === 'editar' ? edicion.area : null;
  const [codigo, setCodigo] = useState('');
  const [nombre, setNombre] = useState(area?.nombre ?? '');
  const [activa, setActiva] = useState(area?.activa ?? true);

  const guardar = useMutation({
    mutationFn: async () => {
      if (area) {
        await actualizarArea(area.id, { nombre, activa }, { empresa: empresaId });
        return area.id;
      }
      const creada = await crearArea({ codigo, nombre }, { empresa: empresaId, idempotencyKey: edicion.tipo === 'crear' ? edicion.clave : undefined });
      return creada.id;
    },
    onSuccess: (id) => {
      mostrar(area ? t('areas.guardada') : t('areas.creada'));
      onCreada(id);
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/areas'),
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    guardar.mutate();
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 440, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-area">
      <form onSubmit={enviar}>
        <DialogTitle id="titulo-area">{area ? t('areas.editarTitulo', { nombre: area.nombre }) : t('areas.nueva')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            {!area && (
              <TextField
                required
                label={t('areas.codigo')}
                helperText={t('areas.codigoAyuda')}
                value={codigo}
                onChange={(e) => setCodigo(e.target.value.toUpperCase())}
                sx={{ '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
              />
            )}
            <TextField required label={t('areas.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} />
            {area && <FormControlLabel control={<Switch checked={activa} onChange={(e) => setActiva(e.target.checked)} />} label={t('areas.activa')} />}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
            {area ? t('comun.guardar') : t('areas.crear')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
