import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from '@mui/material';
import type { UseMutationResult } from '@tanstack/react-query';
import type { EmpresaDto } from '@validasoft/api-client';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useCrearEmpresa, usePaises } from './api';
import { borradorEmpresaNuevo, ejemploFolio, validarEmpresa, type BorradorEmpresa, type ErrorEmpresa } from './modelo';

interface Props {
  claveIdempotencia: string;
  onCerrar: () => void;
  onCreada: (empresa: EmpresaDto) => void;
  /** Quién crea la empresa: por omisión, el grupo desde su propia sesión; la consola de Plataforma pasa la suya (con el grupo ya elegido, PI-4). */
  mutacion?: UseMutationResult<EmpresaDto, Error, BorradorEmpresa>;
  /** Para qué grupo es (la consola de Plataforma lo dice debajo del título). */
  subtitulo?: string;
}

/** Agregar una razón social al grupo: identificación, razón social y prefijo de folio (no se puede cambiar después). */
export function DialogoEmpresa({ claveIdempotencia, onCerrar, onCreada, mutacion, subtitulo }: Props) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [b, setB] = useState<BorradorEmpresa>(borradorEmpresaNuevo);
  const [tocados, setTocados] = useState<ReadonlySet<keyof BorradorEmpresa>>(new Set());
  const paises = usePaises(true);
  const propia = useCrearEmpresa(claveIdempotencia);
  const crear = mutacion ?? propia;

  const tipos = paises.data?.find((p) => p.codigo === b.pais)?.tiposIdentificacion ?? [];
  const tipo = tipos.find((x) => x.codigo === b.tipo);
  const errores = validarEmpresa(b, tipo);
  const primero = errores[0];
  const mensaje = (e: ErrorEmpresa) => t(`empresasGrupo.errores.${e}`, { tipo: tipo?.nombre ?? b.tipo });
  const errorDe = (campo: keyof BorradorEmpresa, codigos: readonly ErrorEmpresa[]) => {
    const e = errores.find((x) => codigos.includes(x));
    return e && tocados.has(campo) ? mensaje(e) : null;
  };
  const cambiar = (parcial: Partial<BorradorEmpresa>) => setB((previo) => ({ ...previo, ...parcial }));
  const tocar = (campo: keyof BorradorEmpresa) => setTocados((previos) => new Set([...previos, campo]));

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (primero) return;
    crear.mutate(b, {
      onSuccess: (empresa) => {
        mostrar(t('empresasGrupo.creada', { nombre: empresa.nombre }));
        onCreada(empresa);
      },
    });
  };

  const errorNumero = errorDe('numero', ['numero-requerido', 'numero-invalido']);
  const errorNombre = errorDe('nombre', ['nombre-requerido']);
  const errorPrefijo = errorDe('prefijo', ['prefijo-invalido']);

  return (
    <Dialog open onClose={crear.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 520, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-empresa">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-empresa">
          {t('empresasGrupo.nueva')}
          {subtitulo && (
            <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
              {subtitulo}
            </Typography>
          )}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {crear.isError && <Alert severity="error">{mensajeDeError(crear.error, t)}</Alert>}
            {paises.isError && <Alert severity="error">{mensajeDeError(paises.error, t)}</Alert>}
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
              <TextField
                select
                required
                label={t('empresasGrupo.campos.pais')}
                value={paises.data ? b.pais : ''}
                onChange={(e) => cambiar({ pais: e.target.value, tipo: '' })}
                sx={{ flex: 1, minWidth: 0 }}
              >
                {(paises.data ?? []).map((p) => (
                  <MenuItem key={p.codigo} value={p.codigo}>
                    {p.nombre}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                required
                label={t('empresasGrupo.campos.tipo')}
                value={tipo ? b.tipo : ''}
                onChange={(e) => cambiar({ tipo: e.target.value })}
                sx={{ flex: 1.4, minWidth: 0 }}
              >
                {tipos.map((x) => (
                  <MenuItem key={x.codigo} value={x.codigo}>
                    {x.codigo} · {x.nombre}
                  </MenuItem>
                ))}
              </TextField>
            </Stack>
            <TextField
              required
              label={t('empresasGrupo.campos.numero')}
              value={b.numero}
              onChange={(e) => cambiar({ numero: e.target.value })}
              onBlur={() => tocar('numero')}
              error={errorNumero !== null}
              helperText={errorNumero ?? (tipo?.tieneDv ? t('empresasGrupo.campos.numeroAyudaDv') : undefined)}
              slotProps={{ htmlInput: { spellCheck: false, autoComplete: 'off' } }}
              sx={{ maxWidth: 280, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
            />
            <TextField
              required
              label={t('empresasGrupo.campos.razonSocial')}
              value={b.nombre}
              onChange={(e) => cambiar({ nombre: e.target.value })}
              onBlur={() => tocar('nombre')}
              error={errorNombre !== null}
              helperText={errorNombre}
            />
            <TextField
              required
              label={t('empresasGrupo.campos.prefijo')}
              value={b.prefijo}
              onChange={(e) => cambiar({ prefijo: e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '') })}
              onBlur={() => tocar('prefijo')}
              error={errorPrefijo !== null}
              helperText={errorPrefijo ?? t('empresasGrupo.campos.prefijoAyuda', { ejemplo: ejemploFolio(b.prefijo, new Date().getFullYear()) })}
              slotProps={{ htmlInput: { maxLength: 10, spellCheck: false, autoComplete: 'off' } }}
              sx={{ maxWidth: 240, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
            />
            <Alert severity="info">{t('empresasGrupo.prefijoNoCambia')}</Alert>
          </Stack>
        </DialogContent>
        <DialogActions>
          {primero && <FaltaParaEnviar>{mensaje(primero)}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={crear.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={crear.isPending || primero !== undefined} startIcon={crear.isPending ? <IconoCarga /> : undefined}>
            {t('empresasGrupo.crear')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
