import { Alert, AlertTitle, Button, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { reabrirExpediente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { FolderOpen } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { diasRestantes } from '../plazo';

function useReabrir(empresaId: string, expediente: ExpedienteDto) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (justificacion: string) =>
      reabrirExpediente(expediente.id, { justificacion: justificacion.trim() || null }, { empresa: empresaId, ifMatch: expediente.version }),
    onSettled: async () => {
      await invalidarRuta(cliente, '/api/v1/expedientes');
      await invalidarRuta(cliente, '/api/v1/vinculos');
    },
  });
}

/**
 * Plazo de cargue del expediente inicial (P-4-2, LT-2): mientras está en manos del tercero, hasta cuándo tiene y los
 * recordatorios enviados; caducado, desde cuándo y el botón para reabrirlo (administrador) con un plazo nuevo.
 */
export function AvisoPlazoCargue({ expediente, empresaId, scopes }: { expediente: ExpedienteDto; empresaId: string; scopes: ReadonlySet<string> }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const reabrir = useReabrir(empresaId, expediente);
  const [confirmando, setConfirmando] = useState(false);
  const [justificacion, setJustificacion] = useState('');
  const plazo = expediente.plazoCargue;
  if (!plazo) return null;

  if (expediente.estado === 'Caducado') {
    const destino = (plazo.estadoAlReabrir ?? 'Abierto') as 'Abierto' | 'Observado';
    return (
      <>
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            scopes.has(SCOPES.expedientesAdministrar) && (
              <Button color="inherit" size="small" startIcon={<FolderOpen size={16} aria-hidden />} onClick={() => setConfirmando(true)}>
                {t('plazoCargue.reabrir')}
              </Button>
            )
          }
        >
          <AlertTitle>{t('plazoCargue.caducadoTitulo')}</AlertTitle>
          {t('plazoCargue.caducado', { fecha: formatearFecha(plazo.hasta ?? plazo.caducadoEn ?? expediente.actualizadoEn) })}
        </Alert>
        {confirmando && (
          <DialogoConfirmacion
            tono="primary"
            titulo={t('plazoCargue.reabrirTitulo', { folio: expediente.folio })}
            mensaje={
              <>
                {t('plazoCargue.reabrirTexto', { estado: t(`plazoCargue.estados.${destino}`), dias: String(plazo.dias) })}
                <TextField
                  fullWidth
                  size="small"
                  label={t('plazoCargue.justificacion')}
                  value={justificacion}
                  onChange={(e) => setJustificacion(e.target.value)}
                  sx={{ mt: 2 }}
                />
              </>
            }
            confirmar={t('plazoCargue.reabrir')}
            icono={<FolderOpen size={16} aria-hidden />}
            ocupado={reabrir.isPending}
            onConfirmar={() =>
              reabrir.mutate(justificacion, {
                onSuccess: () => mostrar(t('plazoCargue.reabierto'), 'success'),
                onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
                onSettled: () => setConfirmando(false),
              })
            }
            onCerrar={() => setConfirmando(false)}
          />
        )}
      </>
    );
  }

  if (!plazo.hasta || !(expediente.estado === 'Abierto' || expediente.estado === 'Observado')) return null;
  const dias = diasRestantes(plazo.hasta, Date.now());
  return (
    <Alert severity={dias <= 1 ? 'warning' : 'info'} sx={{ mb: 2 }}>
      {t('plazoCargue.enCurso', { fecha: formatearFecha(plazo.hasta) })} {t('plazoCargue.enCursoDias', { count: dias })}
      {plazo.recordatoriosEnviados.length > 0 && ` ${t('plazoCargue.recordatorios', { dias: [...plazo.recordatoriosEnviados].sort((a, b) => b - a).join(', ') })}`}
    </Alert>
  );
}
