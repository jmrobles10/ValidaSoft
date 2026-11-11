import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Drawer, IconButton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cerrarSesionOperador,
  crearOperadorPlataforma,
  desactivarOperadorPlataforma,
  getListarOperadoresPlataformaQueryOptions,
  getListarPasskeysOperadorQueryOptions,
  getListarSesionesOperadorQueryOptions,
  revocarPasskeyOperador,
  type OperadorDePlataformaDto,
} from '@validasoft/api-client';
import { AvatarActor, BarraFiltros, CampoBusqueda, enfocarNodo, EstadoVacio, FilasEsqueleto, formatearFechaHora, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Ban, CircleCheck, KeyRound, Plus, UserCheck, UserX, X } from 'lucide-react';
import { useMemo, useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { esCorreoPlausible } from '../../ingreso/estado';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, IconoCarga } from '../../shared/componentes';
import { invalidarRuta } from '../../shared/consultas';
import { mensajeDeError } from '../../shared/errores';
import { MenuFila } from '../../shared/MenuFila';
import { useAvisos } from '../../shared/useAvisos';
import { PanelDeAcceso } from '../configuracion/usuarios/PanelDeAcceso';
import { useReactivarOperador } from './apiIdentidad';

const RUTA = '/api/v1/plataforma/operadores';

function EstadoOperador({ activo }: { activo: boolean }) {
  const { t } = useTranslation();
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 13, '& svg': { color: activo ? 'estado.exito.texto' : 'text.secondary' } }}>
      {activo ? <CircleCheck size={14} strokeWidth={1.75} aria-hidden /> : <Ban size={14} strokeWidth={1.75} aria-hidden />}
      {activo ? t('operadores.activo') : t('operadores.inactivo')}
    </Box>
  );
}

function DialogoNuevo({ onCerrar }: { onCerrar: () => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [nombre, setNombre] = useState('');
  const [correo, setCorreo] = useState('');
  const [intentado, setIntentado] = useState(false);
  const crear = useMutation({
    mutationFn: () => crearOperadorPlataforma({ nombre: nombre.trim(), correo: correo.trim() }, { empresa: null }),
    onSuccess: () => {
      mostrar(t('operadores.creado'));
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, RUTA),
  });
  const valido = nombre.trim().length > 0 && esCorreoPlausible(correo);
  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (valido && !crear.isPending) crear.mutate();
  };
  return (
    <Dialog open onClose={crear.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 440, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-nuevo-operador">
      <Box component="form" onSubmit={enviar} noValidate>
      <DialogTitle id="titulo-nuevo-operador">{t('operadores.nuevo')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 0.5 }}>
          {crear.isError && <Alert severity="error">{mensajeDeError(crear.error, t)}</Alert>}
          <TextField required label={t('operadores.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} error={intentado && nombre.trim().length === 0} inputRef={enfocarNodo} />
          <TextField
            required
            type="email"
            label={t('operadores.correo')}
            value={correo}
            onChange={(e) => setCorreo(e.target.value)}
            error={intentado && !esCorreoPlausible(correo)}
            helperText={intentado && !esCorreoPlausible(correo) ? t('errores.correo-invalido') : undefined}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={crear.isPending} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button type="submit" variant="contained" disabled={crear.isPending} startIcon={crear.isPending ? <IconoCarga /> : undefined}>
          {t('operadores.crear')}
        </Button>
      </DialogActions>
      </Box>
    </Dialog>
  );
}

function DrawerOperador({ operador, onCerrar }: { operador: OperadorDePlataformaDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const passkeys = useQuery(getListarPasskeysOperadorQueryOptions(operador.id, { request: { empresa: null } }));
  const sesiones = useQuery({ ...getListarSesionesOperadorQueryOptions(operador.id, { request: { empresa: null } }), refetchOnWindowFocus: true });
  return (
    <Drawer anchor="right" open onClose={onCerrar} slotProps={{ paper: { sx: { width: { xs: '100%', sm: 520 } }, 'aria-labelledby': 'titulo-acceso-operador' } }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start', px: 3, pt: 2.5, pb: 1 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="h3" component="h2" id="titulo-acceso-operador">
            {t('accesoUsuario.titulo', { nombre: operador.nombre })}
          </Typography>
          <Typography variant="body2" noWrap sx={{ color: 'text.secondary' }}>
            {operador.correo}
          </Typography>
        </Box>
        <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar}>
          <X size={16} />
        </IconButton>
      </Stack>
      <Box sx={{ px: 3, py: 1.5, overflowY: 'auto', flex: 1 }}>
        <PanelDeAcceso
          persona={operador.nombre}
          exigePasskey
          passkeys={passkeys}
          sesiones={sesiones}
          onRevocar={(passkeyId, motivo) => revocarPasskeyOperador(operador.id, passkeyId, { motivo }, { empresa: null })}
          onCerrarSesion={(sesionId) => cerrarSesionOperador(operador.id, sesionId, { empresa: null })}
          alCambiar={() => invalidarRuta(cliente, RUTA)}
        />
      </Box>
    </Drawer>
  );
}

/**
 * Operadores de Plataforma (ING-4): quienes operan la consola. Los de ingreso directo entran con correo, código y passkey obligatoria; un operador
 * revoca las passkeys y cierra las sesiones de otro cuando las pierde. La clave de arranque solo da de alta al primero.
 */
export function PaginaOperadores() {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = useQuery(getListarOperadoresPlataformaQueryOptions({ request: { empresa: null } }));
  const [busqueda, setBusqueda] = useState('');
  const [nuevo, setNuevo] = useState(false);
  const [acceso, setAcceso] = useState<OperadorDePlataformaDto | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<OperadorDePlataformaDto | null>(null);
  const [porReactivar, setPorReactivar] = useState<OperadorDePlataformaDto | null>(null);
  const reactivar = useReactivarOperador();

  const desactivar = useMutation({
    mutationFn: (operador: OperadorDePlataformaDto) => desactivarOperadorPlataforma(operador.id, { empresa: null }),
    onSuccess: () => mostrar(t('operadores.desactivado')),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => {
      setPorDesactivar(null);
      return invalidarRuta(cliente, RUTA);
    },
  });

  const confirmarReactivar = (operador: OperadorDePlataformaDto) =>
    reactivar.mutate(operador.id, {
      onSuccess: () => mostrar(t('operadores.reactivado', { nombre: operador.nombre })),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setPorReactivar(null),
    });

  const visibles = useMemo(() => {
    const q = busqueda.trim().toLowerCase();
    return (consulta.data ?? []).filter((o) => !q || `${o.nombre} ${o.correo}`.toLowerCase().includes(q));
  }, [consulta.data, busqueda]);

  return (
    <>
      <EncabezadoPagina
        titulo={t('operadores.titulo')}
        subtitulo={t('operadores.subtitulo')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setNuevo(true)}>
            {t('operadores.nuevo')}
          </Button>
        }
      />
      <Alert severity="info" sx={{ mb: 1.5 }}>
        {t('operadores.claveArranque')}
      </Alert>
      <BarraFiltros>
        <CampoBusqueda valor={busqueda} onCambiar={setBusqueda} placeholder={t('operadores.buscar')} ancho={280} />
      </BarraFiltros>
      {consulta.isError && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('operadores.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('operadores.columnas.persona')}</TableCell>
                <TableCell sx={{ width: 200 }}>{t('operadores.columnas.entraCon')}</TableCell>
                <TableCell sx={{ width: 110 }}>{t('operadores.columnas.passkeys')}</TableCell>
                <TableCell sx={{ width: 170 }}>{t('operadores.columnas.ultimoAcceso')}</TableCell>
                <TableCell sx={{ width: 130 }}>{t('operadores.columnas.estado')}</TableCell>
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
                    <EstadoVacio compacto titulo={t('operadores.vacio')} />
                  </TableCell>
                </TableRow>
              )}
              {visibles.map((o) => (
                <TableRow key={o.id}>
                  <TableCell sx={{ height: 48 }}>
                    <Stack direction="row" spacing={1.25} sx={{ alignItems: 'center', minWidth: 0 }}>
                      <AvatarActor tipo="persona" nombre={o.nombre} />
                      <Box sx={{ minWidth: 0 }}>
                        <Typography noWrap sx={{ fontWeight: 500 }}>
                          {o.nombre}
                        </Typography>
                        <Typography noWrap sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
                          {o.correo}
                        </Typography>
                      </Box>
                    </Stack>
                  </TableCell>
                  <TableCell>{o.directo ? t('operadores.directo') : t('operadores.delegado', { sistema: o.sistema })}</TableCell>
                  <TableCell>
                    {o.directo ? (
                      <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 13 }}>
                        <KeyRound size={14} strokeWidth={1.75} aria-hidden />
                        {t('usuariosIngreso.passkeysCuenta', { count: o.passkeys })}
                      </Box>
                    ) : (
                      '—'
                    )}
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla" sx={{ whiteSpace: 'nowrap' }}>{o.ultimoAcceso ? formatearFechaHora(o.ultimoAcceso) : t('usuarios.nunca')}</Typography>
                  </TableCell>
                  <TableCell>
                    <EstadoOperador activo={o.activo} />
                  </TableCell>
                  <TableCell align="right">
                    <MenuFila
                      etiqueta={t('comun.accionesDe', { nombre: o.nombre })}
                      acciones={
                        o.activo
                          ? [
                              ...(o.directo ? [{ etiqueta: t('accesoUsuario.menu'), icono: KeyRound, onClick: () => setAcceso(o) }] : []),
                              { etiqueta: t('operadores.desactivar'), icono: UserX, destructiva: true, onClick: () => setPorDesactivar(o) },
                            ]
                          : [{ etiqueta: t('operadores.reactivar'), icono: UserCheck, onClick: () => setPorReactivar(o) }]
                      }
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {nuevo && <DialogoNuevo onCerrar={() => setNuevo(false)} />}
      {acceso && <DrawerOperador key={acceso.id} operador={acceso} onCerrar={() => setAcceso(null)} />}
      {porDesactivar && (
        <DialogoConfirmacion
          titulo={t('operadores.desactivarA', { nombre: porDesactivar.nombre })}
          subtitulo={porDesactivar.correo}
          mensaje={t('operadores.desactivarAyuda')}
          confirmar={t('operadores.desactivar')}
          icono={<Ban size={16} aria-hidden />}
          ocupado={desactivar.isPending}
          onConfirmar={() => desactivar.mutate(porDesactivar)}
          onCerrar={() => setPorDesactivar(null)}
        />
      )}
      {porReactivar && (
        <DialogoConfirmacion
          titulo={t('operadores.reactivarA', { nombre: porReactivar.nombre })}
          subtitulo={porReactivar.correo}
          mensaje={t('operadores.reactivarAyuda')}
          confirmar={t('operadores.reactivar')}
          tono="primary"
          icono={<UserCheck size={16} aria-hidden />}
          ocupado={reactivar.isPending}
          onConfirmar={() => confirmarReactivar(porReactivar)}
          onCerrar={() => setPorReactivar(null)}
        />
      )}
    </>
  );
}
