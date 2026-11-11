import { Button, Stack } from '@mui/material';
import type { ExpedienteDto } from '@validasoft/api-client';
import { OctagonX } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionVerificacion } from '../api';
import { accionesDeVerificacion } from '../modelo';
import { DialogoRechazoCumplimiento } from './DialogoRechazoCumplimiento';
import { EstudioDeCumplimiento } from './EstudioDeCumplimiento';
import { ListasRestrictivas } from './ListasRestrictivas';

export interface PanelVerificacionesProps {
  expediente: ExpedienteDto;
  empresaId: string;
  scopes: ReadonlySet<string>;
  nombres: ReadonlyMap<string, string>;
}

/**
 * Verificaciones del expediente (H7): listas restrictivas (estado, personas consultadas, coincidencias con su decisión,
 * evidencia, reconsulta y consulta hecha por fuera) y el estudio del oficial de cumplimiento. Con un hallazgo grave, el oficial
 * puede rechazar el expediente desde aquí. Solo la empresa lo ve; el tercero no.
 */
export function PanelVerificaciones({ expediente, empresaId, scopes, nombres }: PanelVerificacionesProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const accion = useAccionVerificacion(empresaId, expediente.id);
  const [rechazando, setRechazando] = useState(false);
  const verificaciones = expediente.verificaciones ?? [];
  if (verificaciones.length === 0) return null;
  const listas = verificaciones.find((v) => v.tipo === 'ListasRestrictivas');
  const estudio = verificaciones.find((v) => v.tipo === 'EstudioDeCumplimiento');
  const acciones = accionesDeVerificacion(expediente, scopes);

  return (
    <Tarjeta
      titulo={t('cumplimiento.panel.titulo')}
      etiqueta={t('cumplimiento.panel.titulo')}
      acciones={
        acciones.rechazar && (
          <Button
            size="small"
            variant="outlined"
            color="error"
            startIcon={<OctagonX size={16} aria-hidden />}
            onClick={() => {
              accion.reset();
              setRechazando(true);
            }}
          >
            {t('cumplimiento.rechazo.boton')}
          </Button>
        )
      }
    >
      <Stack spacing={2}>
        {listas && <ListasRestrictivas expediente={expediente} empresaId={empresaId} verificacion={listas} acciones={acciones} nombres={nombres} />}
        {estudio && (
          <EstudioDeCumplimiento
            expediente={expediente}
            empresaId={empresaId}
            verificacion={estudio}
            puedeDecidir={acciones.decidir}
            puedeDescargar={acciones.descargar}
            nombres={nombres}
          />
        )}
      </Stack>
      {rechazando && (
        <DialogoRechazoCumplimiento
          expediente={expediente}
          empresaId={empresaId}
          ocupado={accion.isPending}
          error={accion.error}
          onCerrar={() => setRechazando(false)}
          onConfirmar={(rechazo) =>
            accion.mutate(
              { tipo: 'rechazar', rechazo },
              {
                onSuccess: () => {
                  mostrar(t('cumplimiento.rechazo.exito'));
                  setRechazando(false);
                },
              },
            )
          }
        />
      )}
    </Tarjeta>
  );
}
