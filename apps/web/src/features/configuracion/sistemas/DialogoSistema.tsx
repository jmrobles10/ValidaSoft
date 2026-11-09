import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { crearSistema, rotarClavePublicaSistema, type SistemaIntegradoDto } from '@validasoft/api-client';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { origenesDeTexto } from './origenes';

export type EdicionSistema = { tipo: 'crear'; clave: string } | { tipo: 'rotar'; sistema: SistemaIntegradoDto };

/** Registrar un sistema (código, nombre, clave pública y, si ya se saben, los sitios que lo incrustan) o rotar su clave. */
export function DialogoSistema({ edicion, onCerrar, onListo }: { edicion: EdicionSistema; onCerrar: () => void; onListo: (id: string) => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [codigo, setCodigo] = useState('');
  const [nombre, setNombre] = useState('');
  const [pem, setPem] = useState('');
  const [textoOrigenes, setTextoOrigenes] = useState('');
  const creando = edicion.tipo === 'crear';
  const { origenes, error: errorOrigenes } = origenesDeTexto(textoOrigenes);

  const pendiente = creando && !codigo.trim()
    ? t('sistemas.faltaCodigo')
    : creando && !nombre.trim()
      ? t('sistemas.faltaNombre')
      : !pem.trim()
        ? t('sistemas.faltaClave')
        : errorOrigenes
          ? t('sistemas.origenes.revisaLinea', { linea: errorOrigenes.linea })
          : null;

  const guardar = useMutation({
    mutationFn: async () => {
      if (edicion.tipo === 'rotar') {
        await rotarClavePublicaSistema(edicion.sistema.id, { clavePublicaPem: pem }, { empresa: null });
        return edicion.sistema.id;
      }
      const creado = await crearSistema(
        { codigo: codigo.trim(), nombre: nombre.trim(), clavePublicaPem: pem, origenesPermitidos: origenes },
        { empresa: null, idempotencyKey: edicion.clave },
      );
      return creado.id;
    },
    onSuccess: (id) => {
      mostrar(edicion.tipo === 'rotar' ? t('sistemas.rotado') : t('sistemas.creado'));
      onListo(id);
      onCerrar();
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/sistemas'),
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (pendiente) return;
    guardar.mutate();
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 520, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-sistema">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-sistema">
          {edicion.tipo === 'rotar' ? t('sistemas.rotarTitulo', { codigo: edicion.sistema.codigo }) : t('sistemas.nuevo')}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {edicion.tipo === 'rotar' && <Alert severity="warning">{t('sistemas.rotarAyuda')}</Alert>}
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            {creando && (
              <Stack direction="row" spacing={1.5}>
                <TextField
                  required
                  label={t('sistemas.codigo')}
                  helperText={t('sistemas.codigoAyuda')}
                  value={codigo}
                  onChange={(e) => setCodigo(e.target.value.toUpperCase())}
                  sx={{ width: 160, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
                />
                <TextField required label={t('sistemas.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} sx={{ flex: 1 }} />
              </Stack>
            )}
            <TextField
              required
              multiline
              minRows={6}
              label={t('sistemas.clavePublica')}
              helperText={t('sistemas.clavePublicaAyuda')}
              value={pem}
              onChange={(e) => setPem(e.target.value)}
              sx={{ '& textarea': { fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 12 } }}
            />
            {creando && (
              <TextField
                multiline
                minRows={2}
                label={t('sistemas.origenes.alRegistrar')}
                placeholder="https://srm.cliente.co"
                value={textoOrigenes}
                onChange={(e) => setTextoOrigenes(e.target.value)}
                error={errorOrigenes !== null}
                helperText={
                  errorOrigenes
                    ? `${errorOrigenes.linea}: ${t(`sistemas.origenes.errores.${errorOrigenes.error}`)}`
                    : t('sistemas.origenes.alRegistrarAyuda')
                }
                slotProps={{ htmlInput: { spellCheck: false, autoCapitalize: 'none' } }}
                sx={{ '& textarea': { fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 12 } }}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          {pendiente && <FaltaParaEnviar>{pendiente}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending || pendiente !== null} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
            {edicion.tipo === 'rotar' ? t('sistemas.rotar') : t('sistemas.registrar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
