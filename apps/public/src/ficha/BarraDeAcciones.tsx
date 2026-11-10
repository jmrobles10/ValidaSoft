import { Box, Button, CircularProgress, Stack, Tooltip, Typography } from '@mui/material';
import type { CompletitudSeccionDto } from '@validasoft/api-client';
import { CheckCircle2, CircleAlert } from 'lucide-react';
import { useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../shared/errores';
import { aparicion } from './movimiento';

interface Props {
  /** Completitud que tendría la sección con lo escrito (FM-46); `null` mientras llega la primera. */
  prevista: CompletitudSeccionDto | null;
  /** 422 de formato de la evaluación: hay un dato mal escrito. */
  errorPrevista: unknown;
  /** La evaluación no respondió (red, límite…): no se bloquea; el guardado valida igual. */
  sinRevision: boolean;
  pendiente: boolean;
  sinGuardar: boolean;
  guardando: boolean;
  onGuardar: () => void;
  onContinuar: () => void;
}

/**
 * Barra fija abajo con «Guardar» y «Guardar y continuar», siempre a la vista. «Guardar» se habilita cuando hay cambios;
 * «Guardar y continuar», solo si la API dice que la sección quedaría completa, y a su lado se ve qué falta. La
 * completitud nunca se calcula aquí.
 */
export function BarraDeAcciones({ prevista, errorPrevista, sinRevision, pendiente, sinGuardar, guardando, onGuardar, onContinuar }: Props) {
  const { t } = useTranslation();
  const lista = !errorPrevista && (sinRevision || prevista?.completa === true);
  const faltantes = errorPrevista ? [] : (prevista?.faltantes ?? []);
  // En el celular la barra es fija (como la navegación inferior, para no depender de cómo cada navegador mide lo
  // «pegajoso» con su barra de direcciones): se reserva su alto al final del formulario para no tapar el último campo.
  const ref = useRef<HTMLDivElement>(null);
  const [alto, setAlto] = useState(0);
  useLayoutEffect(() => {
    const barra = ref.current;
    if (!barra || typeof ResizeObserver === 'undefined') return;
    const observador = new ResizeObserver(() => setAlto(barra.getBoundingClientRect().height));
    observador.observe(barra);
    return () => observador.disconnect();
  }, []);

  const [icono, color, texto] = errorPrevista
    ? [<CircleAlert key="i" size={18} aria-hidden />, 'error.main', t('ficha.barra.revisar', { detalle: mensajeDeError(errorPrevista, t) })]
    : sinRevision
      ? [<CircleAlert key="i" size={18} aria-hidden />, 'text.secondary', t('ficha.barra.sinRevision')]
      : !prevista
      ? [<CircularProgress key="i" size={16} aria-hidden />, 'text.secondary', t('ficha.barra.revisando')]
      : lista
        ? [<CheckCircle2 key="i" size={18} aria-hidden />, 'success.main', t('ficha.barra.lista')]
        : [<CircleAlert key="i" size={18} aria-hidden />, 'warning.dark', t('ficha.barra.faltan', { count: faltantes.length })];

  return (
    <>
      <Box aria-hidden sx={{ display: { xs: 'block', md: 'none' }, height: alto, flex: 'none' }} />
      <Box
        ref={ref}
        role="group"
        aria-label={t('ficha.barra.titulo')}
        sx={{
          position: { xs: 'fixed', md: 'sticky' },
          // En el celular, justo encima de la navegación inferior del Shell (64 px más su borde).
          bottom: { xs: 'calc(65px + env(safe-area-inset-bottom))', md: 0 },
          mt: { md: 2.5 },
          insetInline: { xs: 0, md: 'auto' },
          zIndex: 3,
          px: { xs: 1.5, sm: 3, md: 0 },
          py: 1.5,
          bgcolor: 'background.default',
          borderTop: 1,
          borderColor: 'divider',
          display: 'flex',
          flexDirection: { xs: 'column', sm: 'row' },
          alignItems: { xs: 'stretch', sm: 'center' },
          gap: 1.5,
        }}
      >
        <Stack direction="row" spacing={1} aria-live="polite" sx={{ flex: 1, minWidth: 0, alignItems: 'center', color, opacity: pendiente && prevista ? 0.6 : 1, transition: 'opacity 200ms' }}>
          <Box key={texto} component="span" sx={{ display: 'inline-flex', flex: 'none', animation: `${aparicion} 200ms ease-out` }}>
            {icono}
          </Box>
          <Box sx={{ minWidth: 0 }}>
            <Typography key={texto} sx={{ fontSize: 14, lineHeight: '20px', fontWeight: 500, animation: `${aparicion} 200ms ease-out` }}>
              {texto}
            </Typography>
            {sinGuardar && <Typography sx={{ fontSize: 12, lineHeight: '16px', color: 'text.secondary' }}>{t('ficha.barra.sinGuardar')}</Typography>}
          </Box>
        </Stack>
        <Stack direction="row" spacing={1.5} sx={{ flex: 'none', justifyContent: 'flex-end' }}>
          <Button variant="outlined" size="large" disabled={guardando || !sinGuardar} onClick={onGuardar} sx={{ minHeight: 48, flex: { xs: 1, sm: 'none' } }}>
            {guardando ? t('ficha.guardando') : t('ficha.guardar')}
          </Button>
          <Tooltip
            title={!lista && faltantes.length > 0 ? <ListaFaltantes mensajes={faltantes.map((f) => f.mensaje)} /> : ''}
            placement="top-end"
            describeChild
          >
            <Box component="span" sx={{ display: 'inline-flex', flex: { xs: 1, sm: 'none' } }}>
              <Button variant="contained" size="large" disabled={guardando || !lista} onClick={onContinuar} sx={{ minHeight: 48, flex: 1 }}>
                {t('ficha.guardarContinuar')}
              </Button>
            </Box>
          </Tooltip>
        </Stack>
      </Box>
    </>
  );
}

function ListaFaltantes({ mensajes }: { mensajes: string[] }) {
  return (
    <Box component="ul" sx={{ m: 0, pl: 2 }}>
      {mensajes.map((m, i) => (
        <li key={`${m}-${String(i)}`}>{m}</li>
      ))}
    </Box>
  );
}
