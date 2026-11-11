import { Alert, AlertTitle, Button, Tooltip } from '@mui/material';
import { SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { BellRing } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useRecordarObservaciones } from './api';
import { recordadoHoy } from './modelo';

/**
 * Aviso de un expediente devuelto al tercero (docs/06 «Observado»): quiénes observaron, quiénes quedan frenadas y, con permiso
 * de vínculos, «Recordar al tercero» (NT-2): correo con lo que falta corregir y aviso en su portal, una vez al día. Muestra
 * el último recordatorio para no repetirlo sin saberlo.
 */
export function AvisoObservado({
  expediente,
  empresaId,
  scopes,
  texto,
}: {
  expediente: ExpedienteDto;
  empresaId: string;
  scopes: ReadonlySet<string>;
  texto: string;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const recordar = useRecordarObservaciones(empresaId, expediente.id);
  const [confirmando, setConfirmando] = useState(false);
  const ultimo = expediente.recordatorioObservaciones;
  const yaHoy = recordadoHoy(ultimo?.ultimoEn);
  const puede = scopes.has(SCOPES.vinculosEscribir);

  const enviar = () =>
    recordar.mutate(undefined, {
      onSuccess: () => mostrar(t('recordatorios.enviado')),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setConfirmando(false),
    });

  return (
    <>
      <Alert
        severity="warning"
        sx={{ mb: 2 }}
        action={
          puede && (
            <Tooltip title={yaHoy ? t('recordatorios.yaHoy') : ''}>
              <span>
                <Button color="inherit" size="small" startIcon={<BellRing size={16} aria-hidden />} disabled={yaHoy} onClick={() => setConfirmando(true)}>
                  {t('recordatorios.recordar')}
                </Button>
              </span>
            </Tooltip>
          )
        }
      >
        <AlertTitle>{t('expedientes.avisos.observadoTitulo')}</AlertTitle>
        {texto}
        {ultimo && (
          <>
            {' '}
            {ultimo.total > 1
              ? t('recordatorios.ultimoVarios', { fecha: formatearFecha(ultimo.ultimoEn), total: String(ultimo.total) })
              : t('recordatorios.ultimo', { fecha: formatearFecha(ultimo.ultimoEn) })}
          </>
        )}
      </Alert>
      {confirmando && (
        <DialogoConfirmacion
          tono="primary"
          titulo={t('recordatorios.confirmarTitulo')}
          subtitulo={t('recordatorios.confirmarSubtitulo', { tercero: expediente.tercero.nombre, folio: expediente.folio })}
          mensaje={t('recordatorios.confirmarTexto')}
          confirmar={t('recordatorios.confirmar')}
          ocupado={recordar.isPending}
          onConfirmar={enviar}
          onCerrar={() => setConfirmando(false)}
        />
      )}
    </>
  );
}
