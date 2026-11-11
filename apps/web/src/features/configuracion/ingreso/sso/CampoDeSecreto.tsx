import { Button, Stack, TextField, Typography } from '@mui/material';
import type { ConfiguracionDeSsoDto } from '@validasoft/api-client';
import { enfocarNodo } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { secretoValido, type AccionDeSecreto } from './modeloSso';

interface Props {
  config: ConfiguracionDeSsoDto;
  accion: AccionDeSecreto;
  valor: string;
  alCambiar: (accion: AccionDeSecreto, valor: string) => void;
}

/** El secreto del cliente solo se escribe: la API nunca lo devuelve, solo con qué cuatro caracteres termina. Se conserva, se reemplaza o se quita. */
export function CampoDeSecreto({ config, accion, valor, alCambiar }: Props) {
  const { t } = useTranslation();

  if (accion === 'cambiar') {
    const invalido = valor.length > 0 && !secretoValido(valor);
    return (
      <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start' }}>
        <TextField
          size="small"
          type="password"
          label={t('sso.proveedor.secreto')}
          value={valor}
          onChange={(e) => alCambiar('cambiar', e.target.value)}
          error={invalido}
          helperText={invalido ? t('sso.proveedor.secretoInvalido') : t('sso.proveedor.secretoAyuda')}
          autoComplete="new-password"
          inputRef={enfocarNodo}
          sx={{ flex: 1, minWidth: 0 }}
          slotProps={{ htmlInput: { maxLength: 2000 } }}
        />
        <Button size="small" variant="text" onClick={() => alCambiar('conservar', '')} sx={{ mt: 0.75 }}>
          {t('comun.cancelar')}
        </Button>
      </Stack>
    );
  }

  const quitando = accion === 'quitar';
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
      <Typography variant="body2" sx={{ flex: 1, minWidth: 0 }}>
        {quitando
          ? t('sso.proveedor.secretoSeQuitara')
          : config.secretoConfigurado
            ? t('sso.proveedor.secretoConfigurado', { ultimos: config.ultimosCuatroSecreto ?? '····' })
            : t('sso.proveedor.secretoNinguno')}
      </Typography>
      {quitando ? (
        <Button size="small" variant="text" onClick={() => alCambiar('conservar', '')}>
          {t('sso.proveedor.conservarSecreto')}
        </Button>
      ) : (
        <>
          <Button size="small" variant="outlined" onClick={() => alCambiar('cambiar', '')}>
            {config.secretoConfigurado ? t('sso.proveedor.cambiarSecreto') : t('sso.proveedor.escribirSecreto')}
          </Button>
          {config.secretoConfigurado && (
            <Button size="small" variant="text" onClick={() => alCambiar('quitar', '')} sx={{ color: 'text.secondary' }}>
              {t('sso.proveedor.quitarSecreto')}
            </Button>
          )}
        </>
      )}
    </Stack>
  );
}
