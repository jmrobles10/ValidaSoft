import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  IconButton,
  Paper,
  Stack,
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
import {
  crearSistema,
  desactivarSistema,
  getListarSistemasQueryOptions,
  rotarClavePublicaSistema,
  SCOPES,
  type SistemaIntegradoDto,
} from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { Ban, KeyRound, Plus } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useAvisos } from '../../../shared/useAvisos';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { resumenClave } from './clave';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'rotar'; sistema: SistemaIntegradoDto };

/** Sistemas integrados del tenant y sus claves públicas (ADR-0011). */
export function PaginaSistemas() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.sistemasAdministrar);
  const consulta = useQuery({ ...getListarSistemasQueryOptions({ request: { empresa: null } }), enabled: puede });
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [edicion, setEdicion] = useState<Edicion | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<SistemaIntegradoDto | null>(null);

  const desactivar = useMutation({
    mutationFn: (s: SistemaIntegradoDto) => desactivarSistema(s.id, { empresa: null }),
    onSuccess: () => mostrar(t('sistemas.desactivado')),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => {
      setPorDesactivar(null);
      return invalidarRuta(cliente, '/api/v1/sistemas');
    },
  });

  if (!puede) return <SinPermiso seccion={t('nav.sistemas')} />;

  return (
    <>
      <EncabezadoPagina
        titulo={t('sistemas.titulo')}
        subtitulo={t('sistemas.ayuda')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} />} onClick={() => setEdicion({ tipo: 'crear', clave: crypto.randomUUID() })}>
            {t('sistemas.nuevo')}
          </Button>
        }
      />
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 3 }}>
        <Table size="small" aria-label={t('sistemas.titulo')} aria-busy={consulta.isPending}>
          <TableHead>
            <TableRow>
              <TableCell>{t('sistemas.codigo')}</TableCell>
              <TableCell>{t('sistemas.nombre')}</TableCell>
              <TableCell>{t('sistemas.huella')}</TableCell>
              <TableCell>{t('vinculos.detalle.creado')}</TableCell>
              <TableCell align="right" />
            </TableRow>
          </TableHead>
          <TableBody>
            {consulta.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={5}>{t('sistemas.vacio')}</TableCell>
              </TableRow>
            )}
            {consulta.data?.map((s) => (
              <TableRow key={s.id} sx={{ opacity: s.activo ? 1 : 0.55 }}>
                <TableCell>
                  <Chip size="small" color="primary" variant="outlined" label={s.codigo} />
                </TableCell>
                <TableCell>
                  {s.nombre} {!s.activo && <Chip size="small" label={t('comun.inactivo')} sx={{ ml: 1 }} />}
                </TableCell>
                <TableCell>
                  <Typography variant="mono" color="text.secondary">
                    {resumenClave(s.clavePublicaPem)}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="mono">{formatearFecha(s.creadoEn)}</Typography>
                </TableCell>
                <TableCell align="right">
                  {s.activo && (
                    <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                      <Tooltip title={t('sistemas.rotar')}>
                        <IconButton aria-label={`${t('sistemas.rotar')}: ${s.codigo}`} onClick={() => setEdicion({ tipo: 'rotar', sistema: s })}>
                          <KeyRound size={16} />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t('comun.desactivar')}>
                        <IconButton aria-label={`${t('comun.desactivar')}: ${s.codigo}`} onClick={() => setPorDesactivar(s)}>
                          <Ban size={16} />
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

      {edicion && <DialogoSistema edicion={edicion} onCerrar={() => setEdicion(null)} />}
      {porDesactivar && (
        <DialogoConfirmacion
          titulo={t('comun.desactivar')}
          mensaje={t('sistemas.confirmarDesactivar', { codigo: porDesactivar.codigo })}
          confirmar={t('comun.desactivar')}
          ocupado={desactivar.isPending}
          onConfirmar={() => desactivar.mutate(porDesactivar)}
          onCerrar={() => setPorDesactivar(null)}
        />
      )}
    </>
  );
}

function DialogoSistema({ edicion, onCerrar }: { edicion: Edicion; onCerrar: () => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [codigo, setCodigo] = useState('');
  const [nombre, setNombre] = useState('');
  const [pem, setPem] = useState('');

  const guardar = useMutation({
    mutationFn: () =>
      edicion.tipo === 'rotar'
        ? rotarClavePublicaSistema(edicion.sistema.id, { clavePublicaPem: pem }, { empresa: null })
        : crearSistema({ codigo, nombre, clavePublicaPem: pem }, { empresa: null, idempotencyKey: edicion.clave }),
    onSuccess: () => {
      mostrar(edicion.tipo === 'rotar' ? t('sistemas.rotado') : t('sistemas.creado'));
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/sistemas'),
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    guardar.mutate();
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="titulo-sistema">
      <form onSubmit={enviar}>
        <DialogTitle id="titulo-sistema">
          {edicion.tipo === 'rotar' ? t('sistemas.rotarTitulo', { codigo: edicion.sistema.codigo }) : t('sistemas.nuevo')}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {edicion.tipo === 'rotar' && <DialogContentText>{t('sistemas.rotarAyuda')}</DialogContentText>}
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            {edicion.tipo === 'crear' && (
              <>
                <TextField required label={t('sistemas.codigo')} value={codigo} onChange={(e) => setCodigo(e.target.value.toUpperCase())} />
                <TextField required label={t('sistemas.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} />
              </>
            )}
            <TextField
              required
              multiline
              minRows={6}
              label={t('sistemas.clavePublica')}
              helperText={t('sistemas.clavePublicaAyuda')}
              value={pem}
              onChange={(e) => setPem(e.target.value)}
              slotProps={{ htmlInput: { style: { fontFamily: 'monospace', fontSize: 12 } } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onCerrar} disabled={guardar.isPending}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending}>
            {edicion.tipo === 'rotar' ? t('sistemas.rotar') : t('comun.crear')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
