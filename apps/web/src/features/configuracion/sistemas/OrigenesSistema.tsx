import { Alert, Box, Button, IconButton, Stack, TextField, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarOrigenesSistema, type SistemaIntegradoDto } from '@validasoft/api-client';
import { AlineadoConCampo, FaltaParaEnviar } from '@validasoft/ui';
import { Globe, Plus, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { agregarOrigen, MAXIMO_ORIGENES, mismosOrigenes, type ErrorOrigen } from './origenes';

/**
 * Sitios desde los que se puede incrustar Validasoft para este sistema (`PUT /sistemas/{id}/origenes`, reemplaza la
 * lista). Se edita en la página y se guarda de una vez; el padre la monta con `key={sistema.id}` para empezar de nuevo
 * al cambiar de sistema.
 */
export function OrigenesSistema({ sistema }: { sistema: SistemaIntegradoDto }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [lista, setLista] = useState<string[]>(sistema.origenesPermitidos);
  const [texto, setTexto] = useState('');
  const [error, setError] = useState<ErrorOrigen | null>(null);
  const cambiada = !mismosOrigenes(lista, sistema.origenesPermitidos);
  const soloLectura = !sistema.activo;

  const guardar = useMutation({
    mutationFn: () => actualizarOrigenesSistema(sistema.id, { origenes: lista }, { empresa: null }),
    onSuccess: (actualizado) => {
      setLista(actualizado.origenesPermitidos);
      mostrar(t('sistemas.origenes.guardados'));
      return invalidarRuta(cliente, '/api/v1/sistemas');
    },
  });

  const agregar = (evento: FormEvent) => {
    evento.preventDefault();
    const paso = agregarOrigen(lista, texto);
    if ('error' in paso) {
      setError(paso.error);
      return;
    }
    setLista(paso.lista);
    setTexto('');
    setError(null);
  };

  return (
    <Box component="section" aria-labelledby={`origenes-${sistema.id}`}>
      <Typography id={`origenes-${sistema.id}`} component="h3" sx={{ fontSize: 14, fontWeight: 600 }}>
        {t('sistemas.origenes.titulo')}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1.5, maxWidth: 720 }}>
        {t('sistemas.origenes.ayuda', { nombre: sistema.nombre })}
      </Typography>

      {guardar.isError && (
        <Alert severity="error" sx={{ mb: 1.5 }}>
          {mensajeDeError(guardar.error, t)}
        </Alert>
      )}

      <Box sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, mb: 1.5 }}>
        {lista.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ px: 1.5, py: 1.25 }}>
            {t('sistemas.origenes.vacio', { codigo: sistema.codigo })}
          </Typography>
        ) : (
          <Box component="ul" aria-label={t('sistemas.origenes.titulo')} sx={{ m: 0, p: 0, listStyle: 'none' }}>
            {lista.map((origen, i) => (
              <Stack
                key={origen}
                component="li"
                direction="row"
                alignItems="center"
                spacing={1}
                sx={{ px: 1.5, minHeight: 40, borderTop: i === 0 ? 0 : 1, borderColor: 'divider' }}
              >
                <Globe size={14} strokeWidth={1.75} aria-hidden />
                <Typography variant="mono" sx={{ flex: 1, minWidth: 0, overflowWrap: 'anywhere' }}>
                  {origen}
                </Typography>
                {!soloLectura && (
                  <IconButton size="small" aria-label={t('sistemas.origenes.quitar', { origen })} onClick={() => setLista(lista.filter((o) => o !== origen))} disabled={guardar.isPending}>
                    <X size={16} aria-hidden />
                  </IconButton>
                )}
              </Stack>
            ))}
          </Box>
        )}
      </Box>

      {soloLectura ? (
        <Typography variant="body2" color="text.secondary">
          {t('sistemas.origenes.inactivo')}
        </Typography>
      ) : (
        <>
          <Stack component="form" noValidate onSubmit={agregar} direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ sm: 'flex-start' }}>
            <TextField
              size="small"
              label={t('sistemas.origenes.nuevo')}
              placeholder="https://srm.cliente.co"
              value={texto}
              onChange={(e) => {
                setTexto(e.target.value);
                setError(null);
              }}
              error={error !== null}
              helperText={error ? t(`sistemas.origenes.errores.${error}`) : t('sistemas.origenes.formato')}
              disabled={guardar.isPending}
              slotProps={{ htmlInput: { spellCheck: false, autoCapitalize: 'none', inputMode: 'url' } }}
              sx={{ flex: 1, maxWidth: { sm: 420 }, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
            />
            <AlineadoConCampo desde="sm">
              <Button type="submit" variant="outlined" startIcon={<Plus size={16} aria-hidden />} disabled={!texto.trim() || guardar.isPending}>
                {t('sistemas.origenes.agregar')}
              </Button>
            </AlineadoConCampo>
          </Stack>
          <Stack direction="row" spacing={1} alignItems="center" justifyContent="flex-end" sx={{ mt: 1.5 }}>
            <FaltaParaEnviar>
              {cambiada ? t('sistemas.origenes.sinGuardar') : t('sistemas.origenes.cuenta', { n: String(lista.length), maximo: String(MAXIMO_ORIGENES) })}
            </FaltaParaEnviar>
            {cambiada && (
              <Button variant="text" onClick={() => setLista(sistema.origenesPermitidos)} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
                {t('sistemas.origenes.descartar')}
              </Button>
            )}
            <Button variant="contained" onClick={() => guardar.mutate()} disabled={!cambiada || guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
              {t('sistemas.origenes.guardar')}
            </Button>
          </Stack>
        </>
      )}
    </Box>
  );
}
