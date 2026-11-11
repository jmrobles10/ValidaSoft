import { Alert, Button } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { useSesion } from '../../sesion/sesionStore';

/**
 * Tras entrar solo con el código (un factor), invita a registrar una passkey: un aviso compacto y descartable bajo la barra, con la acción a Mi cuenta.
 * No aparece si la cuenta ya la exigió (no pudo entrar sin ella) ni con la identidad delegada.
 */
export function AvisoDePasskey({ rutaMiCuenta }: { rutaMiCuenta: string }) {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const sugerir = useSesion((s) => s.sugerirPasskey);
  const descartar = useSesion((s) => s.descartarSugerenciaDePasskey);
  if (!sugerir) return null;

  return (
    <Alert
      severity="info"
      sx={{ borderRadius: 0, py: 0, alignItems: 'center' }}
      action={
        <>
          <Button
            size="small"
            color="inherit"
            onClick={() => {
              descartar();
              void navegar(rutaMiCuenta);
            }}
          >
            {t('ingreso.avisoPasskeyRegistrar')}
          </Button>
          <Button size="small" color="inherit" onClick={descartar}>
            {t('ingreso.avisoPasskeyDespues')}
          </Button>
        </>
      }
    >
      <strong>{t('ingreso.avisoPasskeyTitulo')}.</strong> {t('ingreso.avisoPasskeyTexto')}
    </Alert>
  );
}
