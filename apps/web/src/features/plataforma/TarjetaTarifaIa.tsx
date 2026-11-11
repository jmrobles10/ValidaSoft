import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, TextField, Typography } from '@mui/material';
import type { TarifaDeIaDto } from '@validasoft/api-client';
import { enfocarNodo, formatearFechaHora } from '@validasoft/ui';
import { Pencil } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, IconoCarga, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useFijarTarifaIa, useTarifaIa } from './apiIdentidad';
import { aNumero, cuerpoDeTarifa, formatearMoneda, validarTarifa } from './modeloConsumo';

function DialogoTarifa({ tarifa, onCerrar }: { tarifa: TarifaDeIaDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [precio, setPrecio] = useState(String(aNumero(tarifa.precioPorValidacionAdicional)));
  const [moneda, setMoneda] = useState(tarifa.moneda);
  const [intentado, setIntentado] = useState(false);
  const fijar = useFijarTarifaIa();
  const errores = validarTarifa(precio, moneda);

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (errores.length > 0 || fijar.isPending) return;
    fijar.mutate(cuerpoDeTarifa(precio, moneda), {
      onSuccess: () => {
        mostrar(t('consola.tarifa.guardada'));
        onCerrar();
      },
    });
  };

  return (
    <Dialog open onClose={fijar.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 440, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-tarifa-ia">
      <Box component="form" onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-tarifa-ia">{t('consola.tarifa.cambiarTitulo')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('consola.tarifa.cambiarAyuda')}
            </Typography>
            {fijar.isError && <Alert severity="error">{mensajeDeError(fijar.error, t)}</Alert>}
            <Stack direction="row" spacing={1.5}>
              <TextField
                label={t('consola.tarifa.precio')}
                value={precio}
                onChange={(e) => setPrecio(e.target.value)}
                error={intentado && errores.includes('precio-invalido')}
                helperText={intentado && errores.includes('precio-invalido') ? t('consola.tarifa.precioInvalido') : t('consola.tarifa.precioAyuda')}
                inputRef={enfocarNodo}
                slotProps={{ htmlInput: { inputMode: 'decimal', autoComplete: 'off' } }}
                sx={{ flex: 1, minWidth: 0 }}
              />
              <TextField
                label={t('consola.tarifa.moneda')}
                value={moneda}
                onChange={(e) => setMoneda(e.target.value.toUpperCase().replace(/[^A-Z]/g, ''))}
                error={intentado && errores.includes('moneda-invalida')}
                helperText={intentado && errores.includes('moneda-invalida') ? t('consola.tarifa.monedaInvalida') : t('consola.tarifa.monedaAyuda')}
                slotProps={{ htmlInput: { maxLength: 3, autoComplete: 'off' } }}
                sx={{ width: 130, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
              />
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={fijar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={fijar.isPending} startIcon={fijar.isPending ? <IconoCarga /> : undefined}>
            {t('comun.guardar')}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  );
}

/** El precio general de cada validación adicional (PI-18): con él se valora lo que una empresa consume por encima de su plan; cada plan puede tener uno propio. */
export function TarjetaTarifaIa() {
  const { t } = useTranslation();
  const consulta = useTarifaIa();
  const [editando, setEditando] = useState(false);
  const tarifa = consulta.data;

  return (
    <Tarjeta
      titulo={t('consola.tarifa.titulo')}
      acciones={
        tarifa && (
          <Button size="small" variant="outlined" startIcon={<Pencil size={14} aria-hidden />} onClick={() => setEditando(true)}>
            {t('consola.tarifa.cambiar')}
          </Button>
        )
      }
    >
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && <Skeleton variant="rounded" height={48} role="status" aria-busy aria-label={t('comun.cargando')} />}
      {tarifa && (
        <Stack spacing={0.5}>
          <Typography variant="h3" component="p">
            {formatearMoneda(tarifa.precioPorValidacionAdicional, tarifa.moneda)}{' '}
            <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
              {t('consola.tarifa.porValidacion')}
            </Typography>
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {aNumero(tarifa.precioPorValidacionAdicional) === 0 ? t('consola.tarifa.sinFijar') : t('consola.tarifa.ayuda')}
            {tarifa.actualizadaEn ? ` ${t('consola.tarifa.actualizada', { fecha: formatearFechaHora(tarifa.actualizadaEn), quien: tarifa.actualizadaPor ?? '—' })}` : ''}
          </Typography>
        </Stack>
      )}
      {editando && tarifa && <DialogoTarifa tarifa={tarifa} onCerrar={() => setEditando(false)} />}
    </Tarjeta>
  );
}
