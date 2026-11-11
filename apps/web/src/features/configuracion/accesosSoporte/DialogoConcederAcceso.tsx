import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { concederAccesoSoporte, type EmpresaDto } from '@validasoft/api-client';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { DURACIONES, validarAcceso, type BorradorAcceso } from './modelo';

/** Conceder a la Plataforma lectura temporal de una empresa (B-6): duración y justificación obligatorias. */
export function DialogoConcederAcceso({ empresas, empresaInicial, onCerrar }: { empresas: readonly EmpresaDto[]; empresaInicial: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const [borrador, setBorrador] = useState<BorradorAcceso>({ empresaId: empresaInicial, horas: 4, justificacion: '' });
  const [intentado, setIntentado] = useState(false);
  const [clave] = useState(() => crypto.randomUUID());
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const conceder = useMutation({
    mutationFn: () => concederAccesoSoporte(
      { empresaId: borrador.empresaId, horas: borrador.horas, justificacion: borrador.justificacion.trim() },
      { empresa: null, idempotencyKey: clave },
    ),
    onSuccess: () => {
      mostrar(t('accesosSoporte.concedido'));
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/accesos-soporte'),
  });
  const errores = validarAcceso(borrador);
  const ver = (e: (typeof errores)[number]) => intentado && errores.includes(e);

  return (
    <Dialog open onClose={onCerrar} maxWidth="xs" fullWidth aria-labelledby="titulo-conceder">
      <DialogTitle id="titulo-conceder">{t('accesosSoporte.conceder')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 0.5 }}>
          <Alert severity="info">{t('accesosSoporte.queVe')}</Alert>
          <TextField select size="small" label={t('accesosSoporte.empresa')} value={borrador.empresaId} onChange={(e) => setBorrador((b) => ({ ...b, empresaId: e.target.value }))} error={ver('empresa-requerida')}>
            {empresas.filter((e) => e.activa).map((e) => (
              <MenuItem key={e.id} value={e.id}>
                {e.nombre}
              </MenuItem>
            ))}
          </TextField>
          <TextField select size="small" label={t('accesosSoporte.duracion')} value={borrador.horas} onChange={(e) => setBorrador((b) => ({ ...b, horas: Number(e.target.value) }))} sx={{ maxWidth: 240 }}>
            {DURACIONES.map((h) => (
              <MenuItem key={h} value={h}>
                {t('accesosSoporte.horas', { count: h })}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label={t('accesosSoporte.justificacion')}
            value={borrador.justificacion}
            onChange={(e) => setBorrador((b) => ({ ...b, justificacion: e.target.value }))}
            multiline
            minRows={3}
            error={ver('justificacion-requerida') || ver('texto-demasiado-largo')}
            helperText={ver('texto-demasiado-largo') ? t('accesosSoporte.errores.texto-demasiado-largo') : t('accesosSoporte.justificacionAyuda')}
          />
          {conceder.isError && <Alert severity="error">{mensajeDeError(conceder.error, t)}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCerrar}>{t('comun.cancelar')}</Button>
        <Button
          variant="contained"
          disabled={conceder.isPending}
          onClick={() => {
            setIntentado(true);
            if (errores.length === 0) conceder.mutate();
          }}
        >
          {t('accesosSoporte.conceder')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
