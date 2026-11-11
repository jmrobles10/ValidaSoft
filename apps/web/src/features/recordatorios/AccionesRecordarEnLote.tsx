import { Button } from '@mui/material';
import { esErrorApi } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { BellRing } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useRecordarEnLote } from './api';
import { mensajeDelLote } from './modelo';

/** Los códigos de lote se comparten con otras acciones en lote: aquí se explican en términos de recordatorios. */
function errorAlRecordar(error: unknown, t: TFunction): string {
  if (esErrorApi(error) && error.codigo === 'lote-vacio') return t('recordatorios.lote.errores.lote-vacio');
  if (esErrorApi(error) && error.codigo === 'lote-demasiado-grande') return t('recordatorios.lote.errores.lote-demasiado-grande');
  return mensajeDeError(error, t);
}

/**
 * Recordatorio masivo de observaciones pendientes desde la bandeja (NT-2): a los expedientes seleccionados o a «todos los
 * observados», con el filtro de área de la pantalla. Un correo y un aviso en el portal por expediente; a quien ya se le recordó
 * hoy no se le repite. Termina con el resumen de a quiénes se les recordó y por qué se omitió el resto.
 */
export function AccionesRecordarEnLote({
  empresaId,
  empresaNombre,
  seleccion,
  areaId,
  areaNombre,
  onTerminado,
}: {
  empresaId: string;
  empresaNombre: string;
  seleccion: ReadonlySet<string>;
  areaId: string;
  areaNombre: string | undefined;
  onTerminado: () => void;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const recordar = useRecordarEnLote(empresaId);
  const [confirmando, setConfirmando] = useState<'seleccion' | 'todos' | null>(null);

  const confirmar = () =>
    recordar.mutate(confirmando === 'todos' ? { todos: true, area: areaId || null } : { expedientes: [...seleccion] }, {
      onSuccess: (resultado) => {
        const { mensaje, severidad } = mensajeDelLote(resultado, t);
        mostrar(mensaje, severidad);
        onTerminado();
      },
      onError: (error) => mostrar(errorAlRecordar(error, t), 'error'),
      onSettled: () => setConfirmando(null),
    });

  return (
    <>
      {seleccion.size > 0 && (
        <Button variant="contained" startIcon={<BellRing size={16} aria-hidden />} onClick={() => setConfirmando('seleccion')}>
          {t('recordatorios.lote.recordarSeleccionados', { count: seleccion.size })}
        </Button>
      )}
      <Button variant="outlined" startIcon={<BellRing size={16} aria-hidden />} onClick={() => setConfirmando('todos')}>
        {t('recordatorios.lote.recordarTodos')}
      </Button>
      {confirmando && (
        <DialogoConfirmacion
          tono="primary"
          titulo={confirmando === 'todos' ? t('recordatorios.lote.confirmarTituloTodos') : t('recordatorios.lote.confirmarTituloSeleccion', { count: seleccion.size })}
          mensaje={
            <>
              {confirmando === 'todos'
                ? areaNombre
                  ? t('recordatorios.lote.confirmarTextoTodosArea', { area: areaNombre })
                  : t('recordatorios.lote.confirmarTextoTodos', { empresa: empresaNombre })
                : null}{' '}
              {t('recordatorios.lote.confirmarTextoComun')}
            </>
          }
          confirmar={t('recordatorios.lote.confirmarSeleccion')}
          ocupado={recordar.isPending}
          onConfirmar={confirmar}
          onCerrar={() => setConfirmando(null)}
        />
      )}
    </>
  );
}
