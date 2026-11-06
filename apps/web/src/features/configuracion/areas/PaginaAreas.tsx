import {
  Alert,
  Autocomplete,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControlLabel,
  IconButton,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarArea, crearArea, definirMiembrosArea, getListarAreasQueryOptions, SCOPES, type AreaDto, type UsuarioDto } from '@validasoft/api-client';
import { Pencil, Plus, Users } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useAvisos } from '../../../shared/useAvisos';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { conEmpresa, invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useUsuarios } from '../usuarios/api';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'editar'; area: AreaDto } | { tipo: 'miembros'; area: AreaDto };

/** Áreas revisoras de la empresa activa (X-Empresa) y sus miembros. */
export function PaginaAreas() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const opciones = getListarAreasQueryOptions({ request: { empresa: empresa.id } });
  const consulta = useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresa.id), enabled: puedeLeer });
  // Los nombres de los miembros solo se ven si la persona puede listar usuarios.
  const usuarios = useUsuarios(permisos.puede(SCOPES.usuariosAdministrar));
  const [edicion, setEdicion] = useState<Edicion | null>(null);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.areas')} />;

  const nombreDe = (id: string) => usuarios.data?.find((u) => u.id === id)?.nombre ?? `${id.slice(0, 8)}…`;

  return (
    <>
      <EncabezadoPagina
        titulo={t('areas.titulo')}
        subtitulo={t('areas.ayuda', { empresa: empresa.nombre })}
        acciones={
          puedeEscribir && (
            <Button variant="contained" startIcon={<Plus size={16} />} onClick={() => setEdicion({ tipo: 'crear', clave: crypto.randomUUID() })}>
              {t('areas.nueva')}
            </Button>
          )
        }
      />
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 3 }}>
        <Table size="small" aria-label={t('areas.titulo')} aria-busy={consulta.isPending}>
          <TableHead>
            <TableRow>
              <TableCell>{t('areas.codigo')}</TableCell>
              <TableCell>{t('areas.nombre')}</TableCell>
              <TableCell>{t('areas.miembros')}</TableCell>
              <TableCell align="right" />
            </TableRow>
          </TableHead>
          <TableBody>
            {consulta.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={4}>{t('areas.vacio')}</TableCell>
              </TableRow>
            )}
            {consulta.data?.map((area) => (
              <TableRow key={area.id} sx={{ opacity: area.activa ? 1 : 0.55 }}>
                <TableCell>
                  <Typography variant="mono">{area.codigo}</Typography>
                </TableCell>
                <TableCell>
                  {area.nombre} {!area.activa && <Chip size="small" label={t('comun.inactivo')} sx={{ ml: 1 }} />}
                </TableCell>
                <TableCell>
                  <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                    {area.miembros.length === 0 && (
                      <Typography variant="body2" color="text.secondary">
                        {t('areas.sinMiembros')}
                      </Typography>
                    )}
                    {area.miembros.map((m) => (
                      <Chip key={m} size="small" label={nombreDe(m)} />
                    ))}
                  </Stack>
                </TableCell>
                <TableCell align="right">
                  {puedeEscribir && (
                    <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                      <Tooltip title={t('comun.editar')}>
                        <IconButton aria-label={`${t('comun.editar')}: ${area.nombre}`} onClick={() => setEdicion({ tipo: 'editar', area })}>
                          <Pencil size={16} />
                        </IconButton>
                      </Tooltip>
                      {usuarios.data && (
                        <Tooltip title={t('areas.editarMiembros')}>
                          <IconButton aria-label={`${t('areas.editarMiembros')}: ${area.nombre}`} onClick={() => setEdicion({ tipo: 'miembros', area })}>
                            <Users size={16} />
                          </IconButton>
                        </Tooltip>
                      )}
                    </Stack>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {edicion && edicion.tipo !== 'miembros' && <DialogoArea empresaId={empresa.id} edicion={edicion} onCerrar={() => setEdicion(null)} />}
      {edicion?.tipo === 'miembros' && usuarios.data && (
        <DialogoMiembros empresaId={empresa.id} area={edicion.area} usuarios={usuarios.data} onCerrar={() => setEdicion(null)} />
      )}
    </>
  );
}

function DialogoArea({ empresaId, edicion, onCerrar }: { empresaId: string; edicion: Exclude<Edicion, { tipo: 'miembros' }>; onCerrar: () => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const area = edicion.tipo === 'editar' ? edicion.area : null;
  const [codigo, setCodigo] = useState('');
  const [nombre, setNombre] = useState(area?.nombre ?? '');
  const [activa, setActiva] = useState(area?.activa ?? true);

  const guardar = useMutation({
    mutationFn: () =>
      area
        ? actualizarArea(area.id, { nombre, activa }, { empresa: empresaId })
        : crearArea({ codigo, nombre }, { empresa: empresaId, idempotencyKey: edicion.tipo === 'crear' ? edicion.clave : undefined }),
    onSuccess: () => {
      mostrar(area ? t('areas.guardada') : t('areas.creada'));
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/areas'),
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    guardar.mutate();
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} fullWidth maxWidth="xs" aria-labelledby="titulo-area">
      <form onSubmit={enviar}>
        <DialogTitle id="titulo-area">{area ? area.nombre : t('areas.nueva')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            {!area && <TextField required label={t('areas.codigo')} value={codigo} onChange={(e) => setCodigo(e.target.value)} />}
            <TextField required label={t('areas.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} />
            {area && <FormControlLabel control={<Switch checked={activa} onChange={(e) => setActiva(e.target.checked)} />} label={t('areas.activa')} />}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onCerrar} disabled={guardar.isPending}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending}>
            {area ? t('comun.guardar') : t('comun.crear')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

function DialogoMiembros({ empresaId, area, usuarios, onCerrar }: { empresaId: string; area: AreaDto; usuarios: UsuarioDto[]; onCerrar: () => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const candidatos = usuarios.filter((u) => u.activo && u.roles.some((r) => r.empresaId === null || r.empresaId === empresaId));
  const [elegidos, setElegidos] = useState<UsuarioDto[]>(() => usuarios.filter((u) => area.miembros.includes(u.id)));

  const guardar = useMutation({
    mutationFn: () => definirMiembrosArea(area.id, { usuarios: elegidos.map((u) => u.id) }, { empresa: empresaId }),
    onSuccess: () => {
      mostrar(t('areas.guardada'));
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/areas'),
  });

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="titulo-miembros">
      <DialogTitle id="titulo-miembros">{t('areas.miembrosDe', { nombre: area.nombre })}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <DialogContentText>{t('areas.miembrosAyuda')}</DialogContentText>
          {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
          <Autocomplete
            multiple
            options={candidatos}
            value={elegidos}
            onChange={(_e, valor) => setElegidos(valor)}
            getOptionLabel={(u) => `${u.nombre} (${u.correo})`}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            renderInput={(params) => <TextField {...params} label={t('areas.miembros')} />}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCerrar} disabled={guardar.isPending}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" onClick={() => guardar.mutate()} disabled={guardar.isPending}>
          {t('comun.guardar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
