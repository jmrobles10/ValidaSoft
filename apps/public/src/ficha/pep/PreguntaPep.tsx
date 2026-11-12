import { Alert, Button, Chip, Stack, Typography } from '@mui/material';
import { formatearDiaCivil } from '@validasoft/ui';
import { FilePen } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CampoSiNo } from '../campos';
import type { DeclaracionPep } from '../tipos';
import { datosBasicos, faltasBasicas, type FaltaBasico, type OrigenDeclaracion } from './declaracion';
import { DialogoDeclaracionPep } from './DialogoDeclaracionPep';

interface Props {
  /** «¿Eres una persona expuesta políticamente (PEP)?» o su versión para una persona de la información legal. */
  etiqueta: string;
  pep: boolean | null | undefined;
  declaracion: DeclaracionPep | null | undefined;
  origen: OrigenDeclaracion;
  /** Lo que cambia en los datos de quien declara: la respuesta y/o su declaración. */
  onCambiar: (cambio: { pep?: boolean; declaracion_pep?: DeclaracionPep | null }) => void;
  /** Mensaje del servidor de una ruta de quien declara (`pep`, `declaracion_pep`, `declaracion_pep.cargo`). */
  mensaje: (ruta: string) => string | null;
  hayEnRuta?: (prefijo: string) => boolean;
  deshabilitado?: boolean;
}

/** Cómo se abrió el diálogo: al responder «Sí» (salir sin declarar devuelve la respuesta a «No») o con el botón de una respuesta que ya era «Sí». */
type Apertura = 'respuesta' | 'boton';

/**
 * La pregunta «¿Es PEP?» con su declaración jurada (HU 49093). Solo esa pregunta abre la declaración (la de vínculo con una PEP
 * no): al responder «Sí» se abre el diálogo; «No» conserva la declaración como inactiva y al volver a «Sí» abre precargada. Con «Sí»
 * muestra si la declaración está firmada o falta, y no se puede guardar la sección sin ella (lo exige el servidor).
 */
export function PreguntaPep({ etiqueta, pep, declaracion, origen, onCambiar, mensaje, hayEnRuta, deshabilitado = false }: Props) {
  const { t } = useTranslation();
  const [apertura, setApertura] = useState<Apertura | null>(null);
  const [faltas, setFaltas] = useState<FaltaBasico[]>([]);

  const abrir = (como: Apertura): boolean => {
    const falta = faltasBasicas(origen);
    setFaltas(falta);
    if (falta.length > 0) return false;
    setApertura(como);
    return true;
  };

  const responder = (si: boolean) => {
    if (!si) {
      setFaltas([]);
      onCambiar({ pep: false });
      return;
    }
    // Sin lo básico (nombre, dirección…) no se abre ni cambia la respuesta: se avisa qué falta.
    if (abrir('respuesta')) onCambiar({ pep: true });
  };

  const salir = () => {
    if (apertura === 'respuesta' && !declaracion) onCambiar({ pep: false });
    setApertura(null);
  };

  const errorDeclaracion = mensaje('declaracion_pep');
  const conErrores = !errorDeclaracion && (hayEnRuta?.('declaracion_pep') ?? false);

  return (
    <Stack spacing={1}>
      <CampoSiNo etiqueta={etiqueta} valor={pep} onCambiar={responder} mensaje={mensaje('pep')} deshabilitado={deshabilitado} />
      {faltas.length > 0 && (
        <Alert severity="warning">
          {t('ficha.pep.faltaBasico.titulo', { campos: faltas.map((f) => t(`ficha.pep.faltaBasico.${f}`)).join(', ') })}
        </Alert>
      )}
      {pep === true && declaracion && (
        <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Chip size="small" color="success" variant="outlined" label={declaracion.fecha_firma ? t('ficha.pep.estado.firmada', { fecha: formatearDiaCivil(declaracion.fecha_firma) }) : t('ficha.pep.estado.lista')} />
          <Button variant="outlined" startIcon={<FilePen size={16} aria-hidden />} onClick={() => abrir('boton')} disabled={deshabilitado}>
            {t('ficha.pep.verEditar')}
          </Button>
        </Stack>
      )}
      {pep === true && !declaracion && (
        <Alert
          severity={errorDeclaracion ? 'error' : 'warning'}
          action={
            <Button color="inherit" size="small" onClick={() => abrir('boton')} disabled={deshabilitado}>
              {t('ficha.pep.diligenciar')}
            </Button>
          }
        >
          {errorDeclaracion ?? t('ficha.pep.estado.falta')}
          {!errorDeclaracion && <Typography component="span" sx={{ display: 'block', fontSize: 14 }}>{t('ficha.pep.estado.faltaAyuda')}</Typography>}
        </Alert>
      )}
      {pep === true && conErrores && <Alert severity="error">{t('ficha.pep.estado.conErrores')}</Alert>}
      {apertura !== null && (
        <DialogoDeclaracionPep
          basicos={datosBasicos(origen)}
          previa={declaracion}
          mensajeServidor={(ruta) => mensaje(`declaracion_pep.${ruta}`)}
          onConfirmar={(nueva) => {
            onCambiar({ pep: true, declaracion_pep: nueva });
            setApertura(null);
          }}
          onSalir={salir}
        />
      )}
    </Stack>
  );
}
