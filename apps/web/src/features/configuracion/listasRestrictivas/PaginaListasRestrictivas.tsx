import { Alert, Box, Button, Chip, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { SCOPES, type ListasRestrictivasConfigDto, type ProveedorListasDto } from '@validasoft/api-client';
import { Skeleton } from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, IconoCarga, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useActualizarListasRestrictivas, useListasRestrictivas } from '../../cumplimiento/api';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { borradorCredenciales, credencialesADto, type BorradorCredenciales } from './modelo';

/**
 * Listas restrictivas de la empresa activa (VE-7, VE-11): proveedor con el que se consulta (Infolaft es el principal entre
 * los reales), credenciales propias de solo escritura («Clave configurada · …1234») y consumo del mes por proveedor.
 */
export function PaginaListasRestrictivas() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = useListasRestrictivas(empresa.id, puedeLeer);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.listas-restrictivas')} />;

  return (
    <>
      <EncabezadoPagina titulo={t('cumplimiento.configuracion.titulo')} subtitulo={t('cumplimiento.configuracion.subtitulo', { empresa: empresa.nombre })} />
      {consulta.isPending && <Skeleton variant="rounded" height={240} />}
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.data && <Contenido empresaId={empresa.id} datos={consulta.data} puedeEscribir={puedeEscribir} />}
    </>
  );
}

function Contenido({ empresaId, datos, puedeEscribir }: { empresaId: string; datos: ListasRestrictivasConfigDto; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const actualizar = useActualizarListasRestrictivas(empresaId);
  const elegir = (proveedor: string) =>
    actualizar.mutate({ proveedor }, { onSuccess: () => mostrar(t('cumplimiento.configuracion.guardado')), onError: (e) => mostrar(mensajeDeError(e, t), 'error') });

  return (
    <Stack spacing={2}>
      <Alert severity={datos.proveedorEfectivo ? 'info' : 'warning'}>
        {datos.proveedorEfectivo ? t('cumplimiento.configuracion.efectivo', { proveedor: datos.proveedorEfectivo }) : t('cumplimiento.configuracion.sinEfectivo')}
        {' '}
        {t('cumplimiento.configuracion.monitoreo', { dias: String(datos.diasMonitoreo), vigencia: String(datos.vigenciaEstudioDias) })}
      </Alert>
      <Tarjeta titulo={t('cumplimiento.configuracion.proveedor')}>
        <TextField
          select
          size="small"
          label={t('cumplimiento.configuracion.proveedor')}
          value={datos.proveedor ?? ''}
          onChange={(e) => elegir(e.target.value)}
          disabled={!puedeEscribir || actualizar.isPending}
          sx={{ minWidth: 320 }}
        >
          <MenuItem value="">{t('cumplimiento.configuracion.deLaInstalacion')}</MenuItem>
          {datos.proveedores.map((p) => (
            <MenuItem key={p.nombre} value={p.nombre} disabled={!p.habilitadoEnInstalacion || !p.configurado}>
              {p.real ? p.nombre : t('cumplimiento.configuracion.simulado')}
              {p.principal ? ` · ${t('cumplimiento.configuracion.principal')}` : ''}
              {!p.habilitadoEnInstalacion ? ` · ${t('cumplimiento.configuracion.noHabilitado')}` : ''}
            </MenuItem>
          ))}
        </TextField>
        <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 1 }}>{t('cumplimiento.configuracion.habeasData')}</Typography>
      </Tarjeta>
      {datos.proveedores
        .filter((p) => p.real)
        .map((p) => (
          <Credenciales key={p.nombre} empresaId={empresaId} proveedor={p} puedeEscribir={puedeEscribir} />
        ))}
      <Tarjeta titulo={t('cumplimiento.configuracion.uso', { mes: datos.uso[0]?.mes ?? '' })}>
        {datos.uso.length === 0 ? (
          <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.configuracion.usoVacio')}</Typography>
        ) : (
          <Table size="small" aria-label={t('cumplimiento.configuracion.uso', { mes: datos.uso[0]?.mes ?? '' })}>
            <TableHead>
              <TableRow>
                {(['proveedor', 'consultas', 'busquedas', 'conCoincidencias', 'cuota'] as const).map((c) => (
                  <TableCell key={c} align={c === 'proveedor' ? 'left' : 'right'}>
                    {t(`cumplimiento.configuracion.usoColumnas.${c}`)}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {datos.uso.map((u) => (
                <TableRow key={u.proveedor}>
                  <TableCell>{u.proveedor}</TableCell>
                  <TableCell align="right">{u.consultas}</TableCell>
                  <TableCell align="right">{u.busquedas}</TableCell>
                  <TableCell align="right">{u.conCoincidencias}</TableCell>
                  <TableCell align="right">{u.cuotaMensual ?? '—'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Tarjeta>
    </Stack>
  );
}

function Credenciales({ empresaId, proveedor: p, puedeEscribir }: { empresaId: string; proveedor: ProveedorListasDto; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const actualizar = useActualizarListasRestrictivas(empresaId);
  const [borrador, setBorrador] = useState<BorradorCredenciales>(() => borradorCredenciales(p));
  const cambiar = (cambio: Partial<BorradorCredenciales>) => setBorrador((b) => ({ ...b, ...cambio }));
  const guardar = (quitar = false) =>
    actualizar.mutate(
      { credenciales: credencialesADto(p.nombre, borrador, quitar) },
      {
        onSuccess: (datos) => {
          mostrar(t('cumplimiento.configuracion.guardado'));
          const nuevo = datos.proveedores.find((x) => x.nombre === p.nombre);
          if (nuevo) setBorrador(borradorCredenciales(nuevo));
        },
      },
    );

  return (
    <Tarjeta
      titulo={
        <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
          {p.nombre}
          {p.principal && <Chip size="small" color="primary" variant="outlined" label={t('cumplimiento.configuracion.principal')} />}
          {!p.habilitadoEnInstalacion && <Chip size="small" variant="outlined" label={t('cumplimiento.configuracion.noHabilitado')} />}
        </Box>
      }
      acciones={
        <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
          {p.configurado ? t('cumplimiento.configuracion.configurada', { ultimos: p.ultimosCuatroClave ?? '' }) : t('cumplimiento.configuracion.sinCredenciales')}
        </Typography>
      }
    >
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, minmax(0, 1fr))' }, gap: 1.5 }}>
        <TextField size="small" label={t('cumplimiento.configuracion.usuario')} value={borrador.usuario} onChange={(e) => cambiar({ usuario: e.target.value })} disabled={!puedeEscribir} />
        <TextField
          size="small"
          type="password"
          autoComplete="new-password"
          label={t('cumplimiento.configuracion.clave')}
          value={borrador.clave}
          onChange={(e) => cambiar({ clave: e.target.value })}
          helperText={t('cumplimiento.configuracion.claveAyuda')}
          disabled={!puedeEscribir}
        />
        <TextField size="small" label={t('cumplimiento.configuracion.url')} value={borrador.urlBase} onChange={(e) => cambiar({ urlBase: e.target.value })} disabled={!puedeEscribir} />
        <TextField size="small" label={t('cumplimiento.configuracion.plan')} value={borrador.plan} onChange={(e) => cambiar({ plan: e.target.value })} disabled={!puedeEscribir} />
        <TextField
          size="small"
          type="number"
          label={t('cumplimiento.configuracion.cuota')}
          value={borrador.cuotaMensual}
          onChange={(e) => cambiar({ cuotaMensual: e.target.value })}
          helperText={t('cumplimiento.configuracion.cuotaAyuda')}
          disabled={!puedeEscribir}
        />
        <TextField select size="small" label={t('cumplimiento.configuracion.modo')} value={borrador.modoBusqueda} onChange={(e) => cambiar({ modoBusqueda: e.target.value as BorradorCredenciales['modoBusqueda'] })} disabled={!puedeEscribir}>
          <MenuItem value="PorNombre">{t('cumplimiento.configuracion.modos.PorNombre')}</MenuItem>
          <MenuItem value="PorDocumento">{t('cumplimiento.configuracion.modos.PorDocumento')}</MenuItem>
        </TextField>
      </Box>
      {actualizar.isError && (
        <Alert severity="error" sx={{ mt: 1.5 }}>
          {mensajeDeError(actualizar.error, t)}
        </Alert>
      )}
      {puedeEscribir && (
        <Stack direction="row" spacing={1} sx={{ mt: 1.5, justifyContent: 'flex-end' }}>
          {p.configurado && (
            <Button variant="text" color="error" onClick={() => guardar(true)} disabled={actualizar.isPending}>
              {t('cumplimiento.configuracion.quitarCredenciales')}
            </Button>
          )}
          <Button variant="contained" onClick={() => guardar()} disabled={actualizar.isPending} startIcon={actualizar.isPending ? <IconoCarga /> : undefined}>
            {t('cumplimiento.configuracion.guardarCredenciales')}
          </Button>
        </Stack>
      )}
    </Tarjeta>
  );
}
