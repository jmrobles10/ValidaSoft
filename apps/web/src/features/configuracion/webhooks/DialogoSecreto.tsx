import { Typography } from '@mui/material';
import { formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { DialogoValorUnaVez } from '../../../shared/DialogoValorUnaVez';

/** El secreto de firma se muestra **una sola vez** (al crear y al rotar): se copia aquí y se guarda en el sistema que recibe. */
export function DialogoSecreto({ secreto, anteriorVence, onCerrar }: { secreto: string; anteriorVence: string | null; onCerrar: () => void }) {
  const { t } = useTranslation();
  return (
    <DialogoValorUnaVez
      titulo={t('webhooks.secreto.titulo')}
      aviso={t('webhooks.secreto.unaVez')}
      valor={secreto}
      textoCopiar={t('webhooks.secreto.copiar')}
      textoCopiado={t('webhooks.secreto.copiado')}
      textoListo={t('webhooks.secreto.listo')}
      onCerrar={onCerrar}
    >
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('webhooks.secreto.comoVerificar')}
      </Typography>
      {anteriorVence && (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('webhooks.secreto.anteriorVence', { fecha: formatearFecha(anteriorVence) })}
        </Typography>
      )}
    </DialogoValorUnaVez>
  );
}
