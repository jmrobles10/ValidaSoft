import { Alert, Box, Button, Drawer, FormControlLabel, IconButton, Stack, Switch, TextField, Typography } from '@mui/material';
import type { TipoVinculoDto } from '@validasoft/api-client';
import { FaltaParaEnviar } from '@validasoft/ui';
import { X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useGuardarTipoVinculo } from './api';
import { EditorAtributos } from './EditorAtributos';
import { borradorDeTipo, borradorTipoNuevo, validarTipoVinculo, type BorradorTipoVinculo, type ErrorTipoVinculo } from './modelo';

/**
 * Crear o editar un tipo de vínculo: código (fijo al editar), nombre, exigencias y atributos permitidos. El botón de
 * guardar queda deshabilitado mientras falte algo (dice qué); el error de cada campo sale cuando ya se tocó.
 */
export function DrawerTipoVinculo({ tipo, claveIdempotencia, onCerrar }: { tipo: TipoVinculoDto | null; claveIdempotencia: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const creando = tipo === null;
  const [b, setB] = useState<BorradorTipoVinculo>(() => (tipo ? borradorDeTipo(tipo) : borradorTipoNuevo()));
  const [tocados, setTocados] = useState<ReadonlySet<string>>(new Set());
  const guardar = useGuardarTipoVinculo(tipo, claveIdempotencia);
  const errores = validarTipoVinculo(b);
  const primero = errores[0];

  const cambiar = (parcial: Partial<BorradorTipoVinculo>) => {
    setB((previo) => ({ ...previo, ...parcial }));
    setTocados((previos) => new Set([...previos, ...Object.keys(parcial)]));
  };
  const tocar = (campo: string) => setTocados((previos) => new Set([...previos, campo]));
  const mensaje = (e: ErrorTipoVinculo) => {
    if (e.campo !== 'atributo') return t(`tiposVinculo.errores.${e.codigo}`);
    const clave = (b.atributos[e.indice]?.clave ?? '').trim() || String(e.indice + 1);
    return t('tiposVinculo.errores.enAtributo', { clave, error: t(`tiposVinculo.errores.${e.codigo}`, { clave }) });
  };
  const errorCampo = (campo: 'codigo' | 'nombre') => {
    const e = errores.find((x) => x.campo === campo);
    return e && tocados.has(campo) ? mensaje(e) : null;
  };
  const errorAtributo = (indice: number) => (tocados.has('atributos') ? errores.find((e) => e.campo === 'atributo' && e.indice === indice) : undefined);

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (primero) return;
    guardar.mutate(b, {
      onSuccess: (guardado) => {
        mostrar(creando ? t('tiposVinculo.creado', { nombre: guardado.nombre }) : t('tiposVinculo.guardado'));
        onCerrar();
      },
    });
  };
  const errorCodigo = errorCampo('codigo');
  const errorNombre = errorCampo('nombre');

  return (
    <Drawer anchor="right" open onClose={guardar.isPending ? undefined : onCerrar} slotProps={{ paper: { sx: { width: 600, maxWidth: '100%' } } }} aria-labelledby="titulo-tipo-vinculo">
      <Box component="form" onSubmit={enviar} noValidate sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Typography id="titulo-tipo-vinculo" variant="h4" component="h2">
            {creando ? t('tiposVinculo.nuevo') : t('tiposVinculo.editarNombre', { nombre: tipo.nombre })}
          </Typography>
          <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar} disabled={guardar.isPending}>
            <X size={18} strokeWidth={1.75} />
          </IconButton>
        </Stack>
        <Stack spacing={2.5} sx={{ p: 2.5, flex: 1, overflowY: 'auto' }}>
          {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
            <TextField
              required
              label={t('tiposVinculo.campos.codigo')}
              value={b.codigo}
              onChange={(e) => cambiar({ codigo: e.target.value.toUpperCase().replace(/[^A-Z0-9_]/g, '') })}
              onBlur={() => tocar('codigo')}
              disabled={!creando}
              error={errorCodigo !== null}
              helperText={errorCodigo ?? t(creando ? 'tiposVinculo.campos.codigoAyuda' : 'tiposVinculo.campos.codigoFijo')}
              slotProps={{ htmlInput: { maxLength: 40, spellCheck: false } }}
              sx={{ flex: 1, minWidth: 0, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
            />
            <TextField
              required
              label={t('tiposVinculo.campos.nombre')}
              value={b.nombre}
              onChange={(e) => cambiar({ nombre: e.target.value })}
              onBlur={() => tocar('nombre')}
              error={errorNombre !== null}
              helperText={errorNombre}
              sx={{ flex: 1.4, minWidth: 0 }}
            />
          </Stack>
          <Stack spacing={1}>
            <FormControlLabel
              sx={{ alignItems: 'flex-start', mx: 0, '& .MuiSwitch-root': { mt: -0.75 } }}
              control={<Switch checked={b.exigeCuentaDesignada} onChange={(e) => cambiar({ exigeCuentaDesignada: e.target.checked })} />}
              label={<TextoInterruptor titulo={t('tiposVinculo.campos.exigeCuenta')} ayuda={t('tiposVinculo.campos.exigeCuentaAyuda')} />}
            />
            <FormControlLabel
              sx={{ alignItems: 'flex-start', mx: 0, '& .MuiSwitch-root': { mt: -0.75 } }}
              control={<Switch checked={b.exigeOtpEnEnlace} onChange={(e) => cambiar({ exigeOtpEnEnlace: e.target.checked })} />}
              label={<TextoInterruptor titulo={t('tiposVinculo.campos.exigeOtp')} ayuda={t('tiposVinculo.campos.exigeOtpAyuda')} />}
            />
          </Stack>
          <EditorAtributos atributos={b.atributos} onCambiar={(atributos) => cambiar({ atributos })} errorDe={errorAtributo} editando={!creando} />
        </Stack>
        <Stack direction="row" spacing={1} alignItems="center" justifyContent="flex-end" sx={{ px: 2.5, py: 1.5, borderTop: 1, borderColor: 'divider' }}>
          {primero && <FaltaParaEnviar>{mensaje(primero)}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending || primero !== undefined} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
            {creando ? t('tiposVinculo.crear') : t('comun.guardar')}
          </Button>
        </Stack>
      </Box>
    </Drawer>
  );
}

function TextoInterruptor({ titulo, ayuda }: { titulo: string; ayuda: string }) {
  return (
    <Box component="span" sx={{ display: 'block' }}>
      <Typography component="span" variant="body2" sx={{ display: 'block' }}>
        {titulo}
      </Typography>
      <Typography component="span" variant="caption" color="text.secondary" sx={{ display: 'block' }}>
        {ayuda}
      </Typography>
    </Box>
  );
}
