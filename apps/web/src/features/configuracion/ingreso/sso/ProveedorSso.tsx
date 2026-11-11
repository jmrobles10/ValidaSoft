import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { probarConexionSso, type ConfiguracionDeSsoDto } from '@validasoft/api-client';
import { Copy, PlugZap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { IconoCarga, Tarjeta } from '../../../../shared/componentes';
import { mensajeDeError } from '../../../../shared/errores';
import { useAvisos } from '../../../../shared/useAvisos';
import { CampoDeSecreto } from './CampoDeSecreto';
import { PROVEEDORES_SSO, type AccionDeSecreto, type FormularioSso } from './modeloSso';

interface Props {
  config: ConfiguracionDeSsoDto;
  formulario: FormularioSso;
  alCambiar: (cambios: Partial<FormularioSso>) => void;
  accionDeSecreto: AccionDeSecreto;
  secreto: string;
  alCambiarSecreto: (accion: AccionDeSecreto, valor: string) => void;
  /** Hay cambios sin guardar: la prueba usa lo guardado, así que primero se guarda. */
  sinGuardar: boolean;
}

const DOS_COLUMNAS = { display: 'grid', gap: 2, gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' } } as const;

/** El proveedor de identidad: tipo, nombre del botón, emisor, client id, secreto, URL de retorno y la prueba de conexión (PI-1). */
export function ProveedorSso({ config, formulario, alCambiar, accionDeSecreto, secreto, alCambiarSecreto, sinGuardar }: Props) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const prueba = useMutation({ mutationFn: () => probarConexionSso({ empresa: null }) });
  const oidc = formulario.proveedor === 'oidc';
  const copiar = () => void navigator.clipboard.writeText(config.urlDeRetorno).then(() => mostrar(t('sso.proveedor.urlCopiada')));

  return (
    <Tarjeta titulo={t('sso.proveedor.titulo')}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
        {t('sso.proveedor.ayuda')}
      </Typography>
      <Stack spacing={2}>
        <Box sx={DOS_COLUMNAS}>
          <TextField select size="small" label={t('sso.proveedor.tipo')} value={formulario.proveedor} onChange={(e) => alCambiar({ proveedor: e.target.value === 'workos' ? 'workos' : 'oidc' })}>
            {PROVEEDORES_SSO.map((p) => (
              <MenuItem key={p} value={p} disabled={p === 'workos' && !config.workOsDisponible}>
                {t(`sso.proveedor.tipos.${p}`)}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            size="small"
            label={t('sso.proveedor.nombre')}
            helperText={t('sso.proveedor.nombreAyuda', { nombre: formulario.nombre.trim() || '…' })}
            value={formulario.nombre}
            onChange={(e) => alCambiar({ nombre: e.target.value })}
            slotProps={{ htmlInput: { maxLength: 60 } }}
          />
        </Box>
        {oidc ? (
          <>
            <TextField
              size="small"
              label={t('sso.proveedor.autoridad')}
              placeholder="https://login.microsoftonline.com/{inquilino}/v2.0"
              helperText={t('sso.proveedor.autoridadAyuda')}
              value={formulario.autoridad}
              onChange={(e) => alCambiar({ autoridad: e.target.value })}
              slotProps={{ htmlInput: { maxLength: 500 } }}
            />
            <TextField size="small" label={t('sso.proveedor.clientId')} value={formulario.clientId} onChange={(e) => alCambiar({ clientId: e.target.value })} slotProps={{ htmlInput: { maxLength: 300 } }} />
            <CampoDeSecreto config={config} accion={accionDeSecreto} valor={secreto} alCambiar={alCambiarSecreto} />
          </>
        ) : (
          <Box sx={DOS_COLUMNAS}>
            <TextField size="small" label={t('sso.proveedor.organizacion')} value={formulario.organizacionWorkOs} onChange={(e) => alCambiar({ organizacionWorkOs: e.target.value })} slotProps={{ htmlInput: { maxLength: 100 } }} />
            <TextField size="small" label={t('sso.proveedor.conexion')} value={formulario.conexionWorkOs} onChange={(e) => alCambiar({ conexionWorkOs: e.target.value })} slotProps={{ htmlInput: { maxLength: 100 } }} />
            <Typography variant="body2" sx={{ color: 'text.secondary', gridColumn: '1 / -1' }}>
              {t('sso.proveedor.workosAyuda')}
            </Typography>
          </Box>
        )}
        <Stack spacing={0.5}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {t('sso.proveedor.urlDeRetorno')}
          </Typography>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Box component="code" sx={{ flex: 1, minWidth: 0, px: 1, py: 0.5, borderRadius: (th) => `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2', fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 12, wordBreak: 'break-all' }}>
              {config.urlDeRetorno}
            </Box>
            <Button size="small" variant="text" startIcon={<Copy size={14} aria-hidden />} onClick={copiar}>
              {t('sso.proveedor.copiar')}
            </Button>
          </Stack>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('sso.proveedor.urlDeRetornoAyuda')}
          </Typography>
        </Stack>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Button variant="outlined" onClick={() => prueba.mutate()} disabled={!config.configurado || sinGuardar || prueba.isPending} startIcon={prueba.isPending ? <IconoCarga /> : <PlugZap size={16} aria-hidden />}>
            {t('sso.proveedor.probar')}
          </Button>
          {(sinGuardar || !config.configurado) && (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('sso.proveedor.guardarAntes')}
            </Typography>
          )}
        </Stack>
        {prueba.isError && <Alert severity="error">{mensajeDeError(prueba.error, t)}</Alert>}
        {prueba.data && (
          <Alert severity={prueba.data.correcto ? 'success' : 'warning'}>
            {prueba.data.mensaje}
            {prueba.data.correcto && prueba.data.emisor ? ` ${t('sso.proveedor.emisorDetectado', { emisor: prueba.data.emisor })}` : ''}
          </Alert>
        )}
      </Stack>
    </Tarjeta>
  );
}
