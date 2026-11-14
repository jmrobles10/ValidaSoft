import { Box, Button, Card, Chip, Stack, Typography } from '@mui/material';
import type { SesionActivaDto } from '@validasoft/api-client';
import { EsqueletoTarjetas, EstadoVacio, formatearFechaHora } from '@validasoft/ui';
import { MonitorSmartphone } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useSesionTercero } from '../sesion/sesionTercero';
import { ErrorRecuperable } from '../shared/componentes';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { useCerrarSesionDelTercero, useSesionesDelTercero } from './api';
import { ordenarSesiones, reconocerDispositivo } from './modelo';

/**
 * «Tus sesiones abiertas»: una por navegador y correo del tercero. Cerrar una la revoca de inmediato —su sesión deja de funcionar al instante, no al vencer—, así que
 * sirve cuando no se reconoce una o se perdió un dispositivo. Cerrar la de este navegador es «Salir».
 */
export function SeccionSesiones() {
  const { t } = useTranslation();
  const consulta = useSesionesDelTercero();
  const sesiones = consulta.data ? ordenarSesiones(consulta.data) : [];

  return (
    <Box component="section" aria-labelledby="privacidad-sesiones">
      <Typography id="privacidad-sesiones" variant="h3" component="h2">
        {t('sesiones.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px', mb: 1.5 }}>{t('sesiones.texto')}</Typography>
      {consulta.isPending ? (
        <EsqueletoTarjetas cantidad={2} lineas={2} />
      ) : consulta.isError ? (
        <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />
      ) : sesiones.length === 0 ? (
        <EstadoVacio icono={MonitorSmartphone} titulo={t('sesiones.vacio')} />
      ) : (
        <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {sesiones.map((s) => (
            <TarjetaSesion key={s.id} sesion={s} />
          ))}
        </Stack>
      )}
    </Box>
  );
}

function TarjetaSesion({ sesion }: { sesion: SesionActivaDto }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const cerrar = useCerrarSesionDelTercero();
  const salir = useSesionTercero((s) => s.salir);
  const { navegador, sistema } = reconocerDispositivo(sesion.dispositivo);
  const dispositivo =
    navegador && sistema ? t('sesiones.dispositivo', { navegador, sistema }) : (navegador ?? sistema ?? t('sesiones.desconocido'));

  const alCerrar = () => {
    // La de este navegador es «Salir»: además de revocarla borra la cookie y la sesión de la pantalla.
    if (sesion.esActual) {
      void salir();
      return;
    }
    cerrar.mutate(sesion.id, {
      onSuccess: () => mostrar(t('sesiones.cerrada')),
      onError: (error) => mostrar(`${t('sesiones.errorCerrar')} ${mensajeDeError(error, t)}`, 'error'),
    });
  };

  return (
    <Card component="li" sx={{ p: 2 }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography component="h3" sx={{ fontSize: 15, lineHeight: '22px' }}>
            {dispositivo}
            {sesion.esActual && <Chip size="small" label={t('sesiones.esta')} sx={{ ml: 1 }} />}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px', overflowWrap: 'anywhere' }}>
            {sesion.correo}
            {sesion.ip ? ` · ${sesion.ip}` : ''}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
            {t('sesiones.abierta', { fecha: formatearFechaHora(sesion.creadaEn) })}
            {sesion.ultimoUsoEn ? ` · ${t('sesiones.ultimoUso', { fecha: formatearFechaHora(sesion.ultimoUsoEn) })}` : ''}
          </Typography>
        </Box>
        <Button
          variant="outlined"
          onClick={alCerrar}
          disabled={cerrar.isPending}
          aria-label={sesion.esActual ? t('sesiones.cerrarEsta') : t('sesiones.cerrarEnDispositivo', { dispositivo, correo: sesion.correo })}
          sx={{ flex: 'none' }}
        >
          {t('sesiones.cerrar')}
        </Button>
      </Stack>
    </Card>
  );
}
