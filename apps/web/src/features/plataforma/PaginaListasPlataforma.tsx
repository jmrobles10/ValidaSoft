import { Alert, Box, Button, Chip, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material';
import type { ProveedorDePlataformaDto } from '@validasoft/api-client';
import { CampoNumero, formatearFechaHora } from '@validasoft/ui';
import { DatabaseZap, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, IconoCarga, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { borradorDePlataforma, credencialesADto, type BorradorCredenciales } from '../configuracion/listasRestrictivas/modelo';
import { useActualizarListasDePlataforma, useListasDePlataforma, useMigrarCredencialesDeListas } from './apiListas';

/**
 * Listas restrictivas de la plataforma (G-2, PP-5): Infolaft es un solo proveedor para todos los clientes, con la cuenta de Sinco. Aquí Plataforma la
 * configura (la clave es de solo escritura y se cifra con la clave maestra) y fija la cuota mensual de cada empresa; las empresas no ven ni cargan
 * estas credenciales. Si quedan empresas con las suyas de antes, se migran desde aquí.
 */
export function PaginaListasPlataforma() {
  const { t } = useTranslation();
  const consulta = useListasDePlataforma();

  return (
    <>
      <EncabezadoPagina titulo={t('plataforma.listas.titulo')} subtitulo={t('plataforma.listas.subtitulo')} />
      {consulta.isPending && <Skeleton variant="rounded" height={320} role="status" aria-busy aria-label={t('comun.cargando')} />}
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.data && (
        <Stack spacing={2}>
          {consulta.data.proveedores.map((p) => (
            <CuentaDeProveedor key={p.nombre} proveedor={p} actualizada={consulta.data.actualizadaEn} actualizadaPor={consulta.data.actualizadaPor} />
          ))}
        </Stack>
      )}
    </>
  );
}

function CuentaDeProveedor({ proveedor: p, actualizada, actualizadaPor }: { proveedor: ProveedorDePlataformaDto; actualizada: string | null; actualizadaPor: string | null }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const actualizar = useActualizarListasDePlataforma();
  const migrar = useMigrarCredencialesDeListas();
  const [borrador, setBorrador] = useState<BorradorCredenciales>(() => borradorDePlataforma(p));
  const [confirmando, setConfirmando] = useState<'quitar' | 'migrar' | null>(null);
  const cambiar = (cambio: Partial<BorradorCredenciales>) => setBorrador((b) => ({ ...b, ...cambio }));
  // Si la cuenta cambia en el servidor (se guardó, se quitó o la migración tomó una), el formulario vuelve a lo guardado: la clave nunca se conserva en pantalla.
  const huella = `${p.configurado ? '1' : '0'}|${p.usuario ?? ''}|${p.ultimosCuatroClave ?? ''}|${p.urlBase ?? ''}|${p.plan ?? ''}|${String(p.cuotaMensualPorEmpresa)}|${p.modoBusqueda ?? ''}`;
  const [vista, setVista] = useState(huella);
  if (vista !== huella) {
    setVista(huella);
    setBorrador(borradorDePlataforma(p));
  }

  const guardar = (quitar = false) =>
    actualizar.mutate(credencialesADto(p.nombre, borrador, quitar), {
      onSuccess: () => mostrar(quitar ? t('plataforma.listas.quitada') : t('plataforma.listas.guardada')),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setConfirmando(null),
    });

  const ejecutarMigracion = () =>
    migrar.mutate(undefined, {
      onSuccess: (r) => mostrar(r.cuentaTomada || r.empresasLimpiadas > 0 ? t('plataforma.listas.migrar.hecha', { count: r.empresasLimpiadas }) : t('plataforma.listas.migrar.sinCuenta')),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setConfirmando(null),
    });

  return (
    <>
      {p.empresasConCredencialesPropias > 0 && (
        <Alert
          severity="warning"
          action={
            <Button size="small" color="inherit" startIcon={<DatabaseZap size={16} aria-hidden />} onClick={() => setConfirmando('migrar')} disabled={migrar.isPending}>
              {t('plataforma.listas.migrar.boton')}
            </Button>
          }
        >
          {t('plataforma.listas.migrar.aviso', { count: p.empresasConCredencialesPropias })}
        </Alert>
      )}
      <Tarjeta
        titulo={
          <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            {t('plataforma.listas.cuenta', { proveedor: p.nombre })}
            {!p.habilitadoEnInstalacion && <Chip size="small" variant="outlined" label={t('plataforma.listas.noHabilitado')} />}
          </Box>
        }
        acciones={
          <Chip
            size="small"
            variant="outlined"
            color={p.configurado ? 'success' : 'default'}
            label={p.configurado ? t('plataforma.listas.configurada', { ultimos: p.ultimosCuatroClave ?? '' }) : t('plataforma.listas.sinConfigurar')}
          />
        }
      >
        {!p.habilitadoEnInstalacion && (
          <Typography sx={{ color: 'text.secondary', fontSize: 13, mb: 1.5 }}>{t('plataforma.listas.ayudaNoHabilitado')}</Typography>
        )}
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(3, minmax(0, 1fr))' }, gap: 1.5 }}>
          <TextField size="small" label={t('plataforma.listas.usuario')} value={borrador.usuario} onChange={(e) => cambiar({ usuario: e.target.value })} />
          <TextField
            size="small"
            type="password"
            autoComplete="new-password"
            label={t('plataforma.listas.clave')}
            value={borrador.clave}
            onChange={(e) => cambiar({ clave: e.target.value })}
            helperText={t('plataforma.listas.claveAyuda')}
          />
          <TextField size="small" label={t('plataforma.listas.url')} value={borrador.urlBase} onChange={(e) => cambiar({ urlBase: e.target.value })} />
          <TextField size="small" label={t('plataforma.listas.plan')} value={borrador.plan} onChange={(e) => cambiar({ plan: e.target.value })} />
          <CampoNumero
            etiqueta={t('plataforma.listas.cuota')}
            valor={borrador.cuotaMensual}
            onCambiar={(texto) => cambiar({ cuotaMensual: texto })}
            ayuda={t('plataforma.listas.cuotaAyuda')}
          />
          <TextField
            select
            size="small"
            label={t('plataforma.listas.modo')}
            value={borrador.modoBusqueda}
            onChange={(e) => cambiar({ modoBusqueda: e.target.value as BorradorCredenciales['modoBusqueda'] })}
          >
            <MenuItem value="PorNombre">{t('plataforma.listas.modos.PorNombre')}</MenuItem>
            <MenuItem value="PorDocumento">{t('plataforma.listas.modos.PorDocumento')}</MenuItem>
          </TextField>
        </Box>
        {actualizar.isError && (
          <Alert severity="error" sx={{ mt: 1.5 }}>
            {mensajeDeError(actualizar.error, t)}
          </Alert>
        )}
        <Stack direction="row" spacing={1} sx={{ mt: 1.5, alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap' }} useFlexGap>
          <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>
            {actualizada ? `${t('plataforma.listas.actualizada')}: ${formatearFechaHora(actualizada)}${actualizadaPor ? ` · ${actualizadaPor}` : ''}` : ''}
          </Typography>
          <Stack direction="row" spacing={1}>
            {p.configurado && (
              <Button variant="text" color="error" startIcon={<Trash2 size={16} aria-hidden />} onClick={() => setConfirmando('quitar')} disabled={actualizar.isPending}>
                {t('plataforma.listas.quitar')}
              </Button>
            )}
            <Button variant="contained" onClick={() => guardar()} disabled={actualizar.isPending} startIcon={actualizar.isPending ? <IconoCarga /> : undefined}>
              {t('plataforma.listas.guardar')}
            </Button>
          </Stack>
        </Stack>
      </Tarjeta>
      {confirmando === 'quitar' && (
        <DialogoConfirmacion
          titulo={t('plataforma.listas.quitarTitulo')}
          mensaje={t('plataforma.listas.quitarMensaje')}
          confirmar={t('plataforma.listas.quitar')}
          ocupado={actualizar.isPending}
          onConfirmar={() => guardar(true)}
          onCerrar={() => setConfirmando(null)}
        />
      )}
      {confirmando === 'migrar' && (
        <DialogoConfirmacion
          titulo={t('plataforma.listas.migrar.titulo')}
          mensaje={t('plataforma.listas.migrar.mensaje')}
          confirmar={t('plataforma.listas.migrar.confirmar')}
          tono="primary"
          ocupado={migrar.isPending}
          onConfirmar={ejecutarMigracion}
          onCerrar={() => setConfirmando(null)}
        />
      )}
    </>
  );
}
