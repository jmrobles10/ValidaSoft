import { Button, Skeleton, Stack, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cerrarMiSesion, getListarMisSesionesQueryOptions } from '@validasoft/api-client';
import { EstadoVacio } from '@validasoft/ui';
import { Laptop, LogOut } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { mensajeDeIngreso } from '../../ingreso/mensajes';
import { cerrarSesionDirecta } from '../../sesion/sesionDirecta';
import { ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { invalidarRuta } from '../../shared/consultas';
import { useAvisos } from '../../shared/useAvisos';
import { FilaDeSesion, ListaDeFilas } from './FilasDeAcceso';

/** Mis sesiones abiertas (ING-3): las que tengo en otros dispositivos se cierran desde aquí; la actual, con «Cerrar esta sesión». */
export function TarjetaSesiones() {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = useQuery({ ...getListarMisSesionesQueryOptions({ request: { empresa: null } }), refetchOnWindowFocus: true });
  const cerrar = useMutation({
    mutationFn: (id: string) => cerrarMiSesion(id, { empresa: null }),
    onSuccess: () => mostrar(t('cuenta.sesiones.cerrada')),
    onError: (error) => mostrar(mensajeDeIngreso(error, t), 'error'),
    onSettled: () => invalidarRuta(cliente, '/api/v1/mi-cuenta/sesiones'),
  });

  const sesiones = consulta.data ?? [];

  return (
    <Tarjeta
      titulo={t('cuenta.sesiones.titulo')}
      acciones={
        <Button size="small" variant="outlined" startIcon={<LogOut size={14} aria-hidden />} onClick={() => void cerrarSesionDirecta()}>
          {t('cuenta.sesiones.cerrarEsta')}
        </Button>
      }
    >
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
        {t('cuenta.sesiones.ayuda')}
      </Typography>
      {consulta.isError && <ErrorRecuperable mensaje={t('cuenta.sesiones.errorCarga')} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && (
        <Stack spacing={1.5} role="status" aria-busy aria-label={t('comun.cargando')}>
          {[0, 1].map((i) => (
            <Skeleton key={i} variant="rounded" height={44} />
          ))}
        </Stack>
      )}
      {consulta.data && sesiones.length === 0 && <EstadoVacio compacto icono={Laptop} titulo={t('cuenta.sesiones.vacio')} />}
      {sesiones.length > 0 && (
        <ListaDeFilas etiqueta={t('cuenta.sesiones.titulo')}>
          {sesiones.map((s) => (
            <FilaDeSesion
              key={s.id}
              sesion={s}
              acciones={
                s.actual ? undefined : (
                  <Button size="small" variant="text" color="error" disabled={cerrar.isPending} onClick={() => cerrar.mutate(s.id)}>
                    {t('cuenta.sesiones.cerrar')}
                  </Button>
                )
              }
            />
          ))}
        </ListaDeFilas>
      )}
    </Tarjeta>
  );
}
