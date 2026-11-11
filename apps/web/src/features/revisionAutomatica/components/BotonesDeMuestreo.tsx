import { Button } from '@mui/material';
import { Check, Undo2 } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useResolverRevision } from '../api';
import type { AccionesDeMuestreo, DestinoDeMuestreo } from '../modelo';
import { DialogoRevertirAprobacion } from './DialogoRevertirAprobacion';

export interface BotonesDeMuestreoProps {
  empresaId: string;
  destino: DestinoDeMuestreo;
  acciones: AccionesDeMuestreo;
}

/**
 * «Confirmar» y «Revertir» de una aprobación por muestreo pendiente (en la bandeja de excepciones y en el documento del
 * expediente). Confirmar es un clic; revertir abre el diálogo que avisa qué pasa y pide la justificación.
 */
export function BotonesDeMuestreo({ empresaId, destino, acciones }: BotonesDeMuestreoProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const resolver = useResolverRevision(empresaId);
  const [revirtiendo, setRevirtiendo] = useState(false);
  if (!acciones.confirmar) return null;

  const confirmar = () =>
    resolver.mutate(
      { expedienteId: destino.expedienteId, requisitoId: destino.requisitoId, areaId: destino.areaId, decision: 'Confirmar' },
      {
        onSuccess: () => mostrar(t('revisionAutomatica.excepciones.confirmada', { documento: destino.documento })),
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      },
    );

  return (
    <>
      <Button
        size="small"
        variant="contained"
        startIcon={resolver.isPending ? <IconoCarga /> : <Check size={16} strokeWidth={1.75} aria-hidden />}
        disabled={resolver.isPending}
        onClick={confirmar}
        aria-label={t('revisionAutomatica.excepciones.confirmarAccesible', { documento: destino.documento, tercero: destino.tercero })}
      >
        {t('revisionAutomatica.excepciones.confirmar')}
      </Button>
      {acciones.revertir && (
        <Button
          size="small"
          variant="outlined"
          startIcon={<Undo2 size={16} strokeWidth={1.75} aria-hidden />}
          disabled={resolver.isPending}
          onClick={() => setRevirtiendo(true)}
          aria-label={t('revisionAutomatica.excepciones.revertirAccesible', { documento: destino.documento, tercero: destino.tercero })}
        >
          {t('revisionAutomatica.excepciones.revertir')}
        </Button>
      )}
      {revirtiendo && <DialogoRevertirAprobacion empresaId={empresaId} destino={destino} onCerrar={() => setRevirtiendo(false)} />}
    </>
  );
}
