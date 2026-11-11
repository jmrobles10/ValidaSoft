import { Box, Button, Skeleton, Stack } from '@mui/material';
import { EsqueletoFormulario, PaginaEstado } from '@validasoft/ui';
import { Clock, Link2, LogOut, ShieldAlert } from 'lucide-react';
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../shared/componentes';
import { PantallaIngreso } from '../ingreso/PantallaIngreso';
import { MarcoMinimo } from './MarcoMinimo';
import { marcarFallo, renovarSesionDirecta } from './sesionDirecta';
import type { EstadoSesion } from './sesionStore';
import { useMutation } from '@tanstack/react-query';
import { useSesion } from './sesionStore';

/** Esperando la sesión: esqueleto con la forma de la pantalla y qué hacer si tarda. Nunca un spinner a pantalla completa. */
function Esperando() {
  const { t } = useTranslation();
  return (
    <Stack spacing={1.5} sx={{ p: 3 }} aria-busy>
      <Stack direction="row" spacing={1} role="status" sx={{ alignItems: 'center', color: 'text.secondary', fontSize: 13, mb: 1 }}>
        <Link2 size={16} strokeWidth={1.75} aria-hidden />
        <span>{t('sesion.conectando')}</span>
      </Stack>
      <Skeleton variant="rounded" width={180} height={24} />
      <Skeleton variant="rounded" width={320} height={14} />
      <Stack spacing={1.75} sx={{ mt: 1.5, p: 2, border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.md)}px`, bgcolor: 'background.paper' }}>
        {[70, 55, 82, 64, 48].map((ancho) => (
          <Skeleton key={ancho} variant="rounded" width={`${String(ancho)}%`} height={12} />
        ))}
      </Stack>
    </Stack>
  );
}

/** Buscando la sesión de la cookie al abrir la app: esqueleto con la forma de la tarjeta de ingreso. */
function Restaurando() {
  const { t } = useTranslation();
  return (
    <MarcoMinimo>
      <Box sx={{ display: 'flex', justifyContent: 'center', px: 2, pt: { xs: 4, sm: 10 } }}>
        <EsqueletoFormulario campos={2} conTarjeta etiqueta={t('ingreso.restaurando')} sx={{ width: '100%', maxWidth: 420 }} />
      </Box>
    </MarcoMinimo>
  );
}

/**
 * El token de acceso venció mientras la persona usaba la app (la API respondió 401): se intenta renovar una vez con la cookie y, si ya no sirve, se
 * muestra el ingreso con el motivo.
 */
function RenovandoSesion() {
  const intentada = useRef(false);
  useEffect(() => {
    if (intentada.current) return;
    intentada.current = true;
    void renovarSesionDirecta().then((resultado) => {
      if (resultado !== 'renovada') marcarFallo(resultado);
    });
  }, []);
  return <Restaurando />;
}

/** La sesión quedó en pausa por inactividad: «Continuar» la reanuda mientras el servidor la siga aceptando. */
function SesionInactiva() {
  const { t } = useTranslation();
  const continuar = useMutation({
    mutationFn: renovarSesionDirecta,
    onSuccess: (resultado) => {
      if (resultado !== 'renovada') marcarFallo(resultado === 'sin-sesion' ? 'terminada' : resultado);
    },
  });
  return (
    <MarcoMinimo>
      <PaginaEstado
        icono={Clock}
        titulo={t('ingreso.inactivaTitulo')}
        texto={t('ingreso.inactivaTexto')}
        rol="alert"
        acciones={
          <Button variant="contained" size="large" onClick={() => continuar.mutate()} disabled={continuar.isPending} startIcon={continuar.isPending ? <IconoCarga /> : undefined}>
            {t('ingreso.continuarSesion')}
          </Button>
        }
      />
    </MarcoMinimo>
  );
}

/** Lo que se ve mientras no hay una sesión válida. */
export function PantallaSesion({ estado, embebida, restaurando = false }: { estado: EstadoSesion; embebida: boolean; restaurando?: boolean }) {
  const { t } = useTranslation();
  const sesion = useSesion((s) => s.sesion);

  if (!embebida) {
    if (restaurando) return <Restaurando />;
    if (estado === 'inactiva') return <SesionInactiva />;
    if (estado === 'vencida' && sesion === null) return <RenovandoSesion />;
    return <PantallaIngreso estado={estado} />;
  }

  return (
    <MarcoMinimo>
      {estado === 'cerrada' && <PaginaEstado icono={LogOut} titulo={t('sesion.cerrada')} texto={t('sesion.cerradaAyuda')} rol="status" />}
      {estado === 'vencida' && <PaginaEstado icono={Clock} titulo={t('sesion.vencida')} texto={t('sesion.vencidaAyuda')} rol="alert" />}
      {estado === 'rechazada' && <PaginaEstado icono={ShieldAlert} titulo={t('sesion.rechazada')} texto={t('sesion.rechazadaAyuda')} rol="alert" />}
      {(estado === 'sin-sesion' || estado === 'activa') && <Esperando />}
    </MarcoMinimo>
  );
}
