import { Alert, Box, Button, Checkbox, FormControlLabel, MenuItem, Skeleton, Stack, Switch, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarConfiguracionIngreso, getObtenerConfiguracionIngresoQueryOptions, SCOPES, type ConfiguracionDeIngresoDto } from '@validasoft/api-client';
import { DosColumnas } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, IconoCarga, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { HORAS_DE_SESION, minutosDeInactividad, sinCambios } from './modelo';

/** Una opción de modo de ingreso: casilla, nombre y lo que significa para el grupo. */
function Modo({ marcado, titulo, ayuda, onCambiar }: { marcado: boolean; titulo: string; ayuda: string; onCambiar: (valor: boolean) => void }) {
  return (
    <Box>
      <FormControlLabel control={<Checkbox checked={marcado} onChange={(e) => onCambiar(e.target.checked)} />} label={<Typography sx={{ fontWeight: 600 }}>{titulo}</Typography>} />
      <Typography variant="body2" sx={{ color: 'text.secondary', ml: 4 }}>
        {ayuda}
      </Typography>
    </Box>
  );
}

function Formulario({ inicial }: { inicial: ConfiguracionDeIngresoDto }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [delegado, setDelegado] = useState(inicial.modos.includes('Delegado'));
  const [directo, setDirecto] = useState(inicial.modos.includes('Directo'));
  const [horas, setHoras] = useState(inicial.sesionHoras);
  const [inactividad, setInactividad] = useState(inicial.inactividadMinutos);
  const [passkeyParaTodos, setPasskeyParaTodos] = useState(inicial.passkeyParaTodos);

  const actual: ConfiguracionDeIngresoDto = {
    modos: [...(delegado ? ['Delegado'] : []), ...(directo ? ['Directo'] : [])],
    sesionHoras: horas,
    inactividadMinutos: Math.min(inactividad, horas * 60),
    passkeyParaTodos,
  };
  const cambio = !sinCambios(inicial, actual);
  const sinModo = !delegado && !directo;

  const guardar = useMutation({
    mutationFn: () => actualizarConfiguracionIngreso(actual, { empresa: null }),
    onSuccess: () => mostrar(t('ingresoGrupo.guardado')),
    onSettled: () => invalidarRuta(cliente, '/api/v1/configuracion/ingreso'),
  });

  const opcionesInactividad = minutosDeInactividad(horas);

  return (
    <Box
      component="form"
      onSubmit={(evento) => {
        evento.preventDefault();
        if (!sinModo && cambio && !guardar.isPending) guardar.mutate();
      }}
      noValidate
    >
      <EncabezadoPagina
        titulo={t('ingresoGrupo.titulo')}
        subtitulo={t('ingresoGrupo.ayuda')}
        acciones={
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            {cambio && !guardar.isPending && (
              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                {t('ingresoGrupo.cambiosSinGuardar')}
              </Typography>
            )}
            <Button type="submit" variant="contained" disabled={sinModo || !cambio || guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
              {t('ingresoGrupo.guardar')}
            </Button>
          </Stack>
        }
      />
      {guardar.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mensajeDeError(guardar.error, t)}
        </Alert>
      )}
      <DosColumnas
        izquierda={
          <>
            <Tarjeta titulo={t('ingresoGrupo.modos.titulo')}>
              <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
                {t('ingresoGrupo.modos.ayuda')}
              </Typography>
              <Stack spacing={1.5} component="fieldset" sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
                <Typography component="legend" sx={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0 0 0 0)' }}>
                  {t('ingresoGrupo.modos.titulo')}
                </Typography>
                <Modo marcado={delegado} titulo={t('ingresoGrupo.modos.Delegado')} ayuda={t('ingresoGrupo.modos.DelegadoAyuda')} onCambiar={setDelegado} />
                <Modo marcado={directo} titulo={t('ingresoGrupo.modos.Directo')} ayuda={t('ingresoGrupo.modos.DirectoAyuda')} onCambiar={setDirecto} />
              </Stack>
              {sinModo && (
                <Alert severity="error" sx={{ mt: 1.5 }}>
                  {t('errores.modos-de-ingreso-invalidos')}
                </Alert>
              )}
              {inicial.modos.includes('Directo') && !directo && !sinModo && (
                <Alert severity="warning" sx={{ mt: 1.5 }}>
                  {t('ingresoGrupo.advertenciaApagarDirecto')}
                </Alert>
              )}
              {inicial.modos.includes('Delegado') && !delegado && !sinModo && (
                <Alert severity="warning" sx={{ mt: 1.5 }}>
                  {t('ingresoGrupo.advertenciaSoloDirecto')}
                </Alert>
              )}
            </Tarjeta>
            <Tarjeta titulo={t('ingresoGrupo.passkey.titulo')}>
              <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1 }}>
                {t('ingresoGrupo.passkey.ayuda')}
              </Typography>
              <FormControlLabel
                control={<Switch checked={passkeyParaTodos} onChange={(e) => setPasskeyParaTodos(e.target.checked)} />}
                label={t('ingresoGrupo.passkey.todos')}
              />
              <Typography variant="body2" sx={{ color: 'text.secondary', ml: 6 }}>
                {t('ingresoGrupo.passkey.todosAyuda')}
              </Typography>
            </Tarjeta>
          </>
        }
        derecha={
          <Tarjeta titulo={t('ingresoGrupo.sesion.titulo')}>
            <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
              {t('ingresoGrupo.sesion.ayuda')}
            </Typography>
            <Stack spacing={2.5}>
              <TextField
                select
                label={t('ingresoGrupo.sesion.horas')}
                helperText={t('ingresoGrupo.sesion.horasAyuda')}
                value={horas}
                onChange={(e) => setHoras(Number(e.target.value))}
                disabled={!directo}
              >
                {HORAS_DE_SESION.map((h) => (
                  <MenuItem key={h} value={h}>
                    {t('ingresoGrupo.sesion.hora', { count: h })}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label={t('ingresoGrupo.sesion.inactividad')}
                helperText={t('ingresoGrupo.sesion.inactividadAyuda')}
                value={Math.min(inactividad, horas * 60)}
                onChange={(e) => setInactividad(Number(e.target.value))}
                disabled={!directo}
              >
                {opcionesInactividad.map((m) => (
                  <MenuItem key={m} value={m}>
                    {m >= 60 && m % 60 === 0 ? t('ingresoGrupo.sesion.hora', { count: m / 60 }) : t('ingresoGrupo.sesion.minutos', { count: m })}
                  </MenuItem>
                ))}
              </TextField>
            </Stack>
          </Tarjeta>
        }
      />
    </Box>
  );
}

/** Configuración › Ingreso (ING-1): cómo entran las personas del grupo, cuánto dura su sesión directa y si todas necesitan passkey. */
export function PaginaIngreso() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.usuariosAdministrar);
  const consulta = useQuery({ ...getObtenerConfiguracionIngresoQueryOptions({ request: { empresa: null } }), enabled: puede });

  if (!puede) return <SinPermiso seccion={t('nav.ingreso')} />;
  if (consulta.isError) return <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />;
  if (!consulta.data)
    return (
      <Stack spacing={2} role="status" aria-busy aria-label={t('comun.cargando')}>
        <Skeleton variant="rounded" width={220} height={28} />
        <Skeleton variant="rounded" height={180} />
        <Skeleton variant="rounded" height={140} />
      </Stack>
    );

  // La clave reinicia el formulario cuando la política cambia en el servidor (tras guardar).
  return <Formulario key={JSON.stringify(consulta.data)} inicial={consulta.data} />;
}
