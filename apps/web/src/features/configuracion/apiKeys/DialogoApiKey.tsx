import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField } from '@mui/material';
import type { ApiKeyDto, EmpresaDto, SistemaIntegradoDto } from '@validasoft/api-client';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useCrearApiKey } from './api';
import { borradorApiKeyNuevo, validarApiKey, type BorradorApiKey } from './modelo';
import { SelectorAlcance } from './SelectorAlcance';
import { SelectorPermisos } from './SelectorPermisos';

export interface DialogoApiKeyProps {
  claveIdempotencia: string;
  empresas: readonly EmpresaDto[];
  /** Sistemas activos; `null` si la sesión no puede verlos (sin `sistemas:administrar`). */
  sistemas: readonly SistemaIntegradoDto[] | null;
  onCerrar: () => void;
  /** Recibe la llave con `valor` (la clave completa): el padre la muestra una sola vez. */
  onCreada: (llave: ApiKeyDto) => void;
}

/** Crear una API key: nombre, permisos, empresas de alcance y, si aplica, expiración y sistema integrado. */
export function DialogoApiKey({ claveIdempotencia, empresas, sistemas, onCerrar, onCreada }: DialogoApiKeyProps) {
  const { t } = useTranslation();
  const [b, setB] = useState<BorradorApiKey>(borradorApiKeyNuevo);
  const [nombreTocado, setNombreTocado] = useState(false);
  const crear = useCrearApiKey(claveIdempotencia);
  const errores = validarApiKey(b, new Date());
  const primero = errores[0];
  const cambiar = (parcial: Partial<BorradorApiKey>) => setB((previo) => ({ ...previo, ...parcial }));
  const errorNombre = nombreTocado && errores.includes('nombre-requerido');
  const errorExpira = errores.includes('expiracion-invalida');

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (primero) return;
    crear.mutate(b, { onSuccess: onCreada });
  };

  return (
    <Dialog open onClose={crear.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 600, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-api-key">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-api-key">{t('apiKeys.nueva')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2.5}>
            {crear.isError && <Alert severity="error">{mensajeDeError(crear.error, t)}</Alert>}
            <TextField
              required
              label={t('apiKeys.campos.nombre')}
              value={b.nombre}
              onChange={(e) => cambiar({ nombre: e.target.value })}
              onBlur={() => setNombreTocado(true)}
              error={errorNombre}
              helperText={errorNombre ? t('apiKeys.errores.nombre-requerido') : t('apiKeys.campos.nombreAyuda')}
            />
            <SelectorPermisos valor={b.scopes} onCambiar={(scopes) => cambiar({ scopes })} />
            <SelectorAlcance valor={b.empresas} empresas={empresas} onCambiar={(seleccion) => cambiar({ empresas: seleccion })} />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
              <TextField
                type="date"
                label={t('apiKeys.campos.expira')}
                value={b.expira}
                onChange={(e) => cambiar({ expira: e.target.value })}
                error={errorExpira}
                helperText={errorExpira ? t('apiKeys.errores.expiracion-invalida') : t('apiKeys.campos.expiraAyuda')}
                slotProps={{ inputLabel: { shrink: true } }}
                sx={{ flex: 1, minWidth: 0 }}
              />
              {sistemas && (
                <TextField
                  select
                  label={t('apiKeys.campos.sistema')}
                  value={b.sistemaId}
                  onChange={(e) => cambiar({ sistemaId: e.target.value })}
                  helperText={t('apiKeys.campos.sistemaAyuda')}
                  slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
                  sx={{ flex: 1, minWidth: 0 }}
                >
                  <MenuItem value="">{t('apiKeys.campos.sinSistema')}</MenuItem>
                  {sistemas.map((s) => (
                    <MenuItem key={s.id} value={s.id}>
                      {s.nombre} · {s.codigo}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          {primero && <FaltaParaEnviar>{t(`apiKeys.errores.${primero}`)}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={crear.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={crear.isPending || primero !== undefined} startIcon={crear.isPending ? <IconoCarga /> : undefined}>
            {t('apiKeys.crear')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
