import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { crearSistema, desactivarSistema, getListarSistemasQueryOptions, rotarClavePublicaSistema, SCOPES, type SistemaIntegradoDto } from '@validasoft/api-client';
import { AvatarActor, Dato, EstadoVacio, FilasEsqueleto, formatearFecha, MarcoTabla, RejillaDatos } from '@validasoft/ui';
import { Ban, CircleCheck, Plug, Plus, RefreshCw } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, IconoCarga, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { resumenClave } from './clave';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'rotar'; sistema: SistemaIntegradoDto };

function EstadoSistema({ activo }: { activo: boolean }) {
  const { t } = useTranslation();
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 13, '& svg': { color: activo ? 'estado.exito.texto' : 'text.secondary' } }}>
      {activo ? <CircleCheck size={14} strokeWidth={1.75} aria-hidden /> : <Ban size={14} strokeWidth={1.75} aria-hidden />}
      {activo ? t('comun.activo') : t('sistemas.desactivado')}
    </Box>
  );
}

/** Sistemas integrados del tenant y sus claves públicas (ADR-0011). */
export function PaginaSistemas() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.sistemasAdministrar);
  const consulta = useQuery({ ...getListarSistemasQueryOptions({ request: { empresa: null } }), enabled: puede });
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [elegidoId, setElegidoId] = useState<string | null>(null);
  const [edicion, setEdicion] = useState<Edicion | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<SistemaIntegradoDto | null>(null);

  const desactivar = useMutation({
    mutationFn: (s: SistemaIntegradoDto) => desactivarSistema(s.id, { empresa: null }),
    onSuccess: () => mostrar(t('sistemas.desactivadoAviso')),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => {
      setPorDesactivar(null);
      return invalidarRuta(cliente, '/api/v1/sistemas');
    },
  });

  if (!puede) return <SinPermiso seccion={t('nav.sistemas')} />;

  const sistemas = consulta.data ?? [];
  const elegido = sistemas.find((s) => s.id === elegidoId) ?? sistemas[0] ?? null;

  return (
    <>
      <EncabezadoPagina
        titulo={t('sistemas.titulo')}
        subtitulo={t('sistemas.ayuda')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setEdicion({ tipo: 'crear', clave: crypto.randomUUID() })}>
            {t('sistemas.nuevo')}
          </Button>
        }
      />
      {consulta.isError && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('sistemas.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('sistemas.sistema')}</TableCell>
                <TableCell sx={{ width: 130 }}>{t('sistemas.estado')}</TableCell>
                <TableCell sx={{ width: 220 }}>{t('sistemas.huella')}</TableCell>
                <TableCell sx={{ width: 120 }}>{t('sistemas.registrado')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={4} filas={3} />}
              {consulta.data && sistemas.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={Plug} titulo={t('sistemas.vacio')} texto={t('sistemas.vacioAyuda')} />
                  </TableCell>
                </TableRow>
              )}
              {sistemas.map((s) => (
                <TableRow
                  key={s.id}
                  selected={s.id === elegido?.id}
                  tabIndex={0}
                  aria-label={t('sistemas.ver', { nombre: s.nombre })}
                  onClick={() => setElegidoId(s.id)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      setElegidoId(s.id);
                    }
                  }}
                  sx={{ cursor: 'pointer', '&:focus-visible': { outlineOffset: -2 } }}
                >
                  <TableCell>
                    <Stack direction="row" spacing={1.25} alignItems="center">
                      <AvatarActor tipo="integracion" />
                      <span style={{ fontWeight: 500 }}>{s.nombre}</span>
                      <Typography component="span" variant="monoTabla" color="text.secondary">
                        {s.codigo}
                      </Typography>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <EstadoSistema activo={s.activo} />
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{resumenClave(s.clavePublicaPem)}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{formatearFecha(s.creadoEn)}</Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {elegido && (
        <Box sx={{ mt: 2 }}>
          <Tarjeta etiqueta={elegido.nombre} titulo={elegido.nombre}>
            <Typography variant="body2" color="text.secondary" sx={{ mt: -1, mb: 2 }}>
              {t('sistemas.detalleAyuda', { codigo: elegido.codigo })}
            </Typography>
            <Stack spacing={2}>
              <Box>
                <Typography sx={{ fontSize: 13, fontWeight: 500, mb: 0.5 }}>{t('sistemas.clavePublica')}</Typography>
                <Box
                  component="pre"
                  sx={{
                    m: 0,
                    px: 1.5,
                    py: 1.25,
                    borderRadius: (th) => `${String(th.radio.sm)}px`,
                    bgcolor: 'superficie.nivel2',
                    fontFamily: (th) => th.typography.mono.fontFamily,
                    fontSize: 12,
                    lineHeight: '18px',
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-all',
                    maxHeight: 180,
                    overflow: 'auto',
                  }}
                >
                  {elegido.clavePublicaPem.trim()}
                </Box>
              </Box>
              <RejillaDatos>
                <Dato etiqueta={t('sistemas.emisor')}>
                  <Typography component="span" variant="mono">
                    {elegido.codigo}
                  </Typography>
                </Dato>
                <Dato etiqueta={t('sistemas.registrado')}>
                  <Typography component="span" variant="mono">
                    {formatearFecha(elegido.creadoEn)}
                  </Typography>
                </Dato>
                <Dato etiqueta={t('sistemas.estado')}>
                  <EstadoSistema activo={elegido.activo} />
                </Dato>
              </RejillaDatos>
              {elegido.activo && (
                <Stack direction="row" spacing={1}>
                  <Button variant="outlined" startIcon={<RefreshCw size={16} aria-hidden />} onClick={() => setEdicion({ tipo: 'rotar', sistema: elegido })}>
                    {t('sistemas.rotar')}
                  </Button>
                  <Button
                    variant="outlined"
                    color="error"
                    startIcon={<Ban size={16} aria-hidden />}
                    onClick={() => setPorDesactivar(elegido)}
                    sx={{ color: 'estado.error.texto', borderColor: 'estado.error.borde' }}
                  >
                    {t('sistemas.desactivar')}
                  </Button>
                </Stack>
              )}
            </Stack>
          </Tarjeta>
        </Box>
      )}

      {edicion && <DialogoSistema edicion={edicion} onCerrar={() => setEdicion(null)} onListo={setElegidoId} />}
      {porDesactivar && (
        <DialogoConfirmacion
          titulo={t('sistemas.desactivarTitulo', { nombre: porDesactivar.nombre })}
          subtitulo={porDesactivar.codigo}
          mensaje={t('sistemas.confirmarDesactivar', { codigo: porDesactivar.codigo })}
          confirmar={t('sistemas.desactivar')}
          icono={<Ban size={16} aria-hidden />}
          ocupado={desactivar.isPending}
          onConfirmar={() => desactivar.mutate(porDesactivar)}
          onCerrar={() => setPorDesactivar(null)}
        />
      )}
    </>
  );
}

function DialogoSistema({ edicion, onCerrar, onListo }: { edicion: Edicion; onCerrar: () => void; onListo: (id: string) => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [codigo, setCodigo] = useState('');
  const [nombre, setNombre] = useState('');
  const [pem, setPem] = useState('');

  const guardar = useMutation({
    mutationFn: async () => {
      if (edicion.tipo === 'rotar') {
        await rotarClavePublicaSistema(edicion.sistema.id, { clavePublicaPem: pem }, { empresa: null });
        return edicion.sistema.id;
      }
      const creado = await crearSistema({ codigo, nombre, clavePublicaPem: pem }, { empresa: null, idempotencyKey: edicion.clave });
      return creado.id;
    },
    onSuccess: (id) => {
      mostrar(edicion.tipo === 'rotar' ? t('sistemas.rotado') : t('sistemas.creado'));
      onListo(id);
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/sistemas'),
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    guardar.mutate();
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 520, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-sistema">
      <form onSubmit={enviar}>
        <DialogTitle id="titulo-sistema">
          {edicion.tipo === 'rotar' ? t('sistemas.rotarTitulo', { codigo: edicion.sistema.codigo }) : t('sistemas.nuevo')}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {edicion.tipo === 'rotar' && <Alert severity="warning">{t('sistemas.rotarAyuda')}</Alert>}
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            {edicion.tipo === 'crear' && (
              <Stack direction="row" spacing={1.5}>
                <TextField
                  required
                  label={t('sistemas.codigo')}
                  helperText={t('sistemas.codigoAyuda')}
                  value={codigo}
                  onChange={(e) => setCodigo(e.target.value.toUpperCase())}
                  sx={{ width: 160, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
                />
                <TextField required label={t('sistemas.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} sx={{ flex: 1 }} />
              </Stack>
            )}
            <TextField
              required
              multiline
              minRows={6}
              label={t('sistemas.clavePublica')}
              helperText={t('sistemas.clavePublicaAyuda')}
              value={pem}
              onChange={(e) => setPem(e.target.value)}
              sx={{ '& textarea': { fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 12 } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
            {edicion.tipo === 'rotar' ? t('sistemas.rotar') : t('sistemas.registrar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
