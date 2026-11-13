import { Alert, AlertTitle, Button } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';

/**
 * Todas las áreas aprobaron, pero el tipo de vínculo exige una cuenta bancaria designada y aprobada y aún no la hay (CB-4): el
 * expediente espera en revisión, con el mismo estilo de aviso que las verificaciones de cumplimiento pendientes. La acción lleva al
 * vínculo, donde está la tarjeta para aprobar o rechazar la cuenta.
 */
export function AvisoEsperandoCuenta({ empresaId, vinculoId }: { empresaId: string; vinculoId: string }) {
  const { t } = useTranslation();
  return (
    <Alert
      severity="info"
      sx={{ mb: 2 }}
      action={
        <Button component={RouterLink} to={`/e/${empresaId}/vinculos/${vinculoId}`} color="inherit" size="small">
          {t('expedientes.avisos.esperandoCuentaAccion')}
        </Button>
      }
    >
      <AlertTitle>{t('expedientes.avisos.esperandoCuentaTitulo')}</AlertTitle>
      {t('expedientes.avisos.esperandoCuenta')}
    </Alert>
  );
}
