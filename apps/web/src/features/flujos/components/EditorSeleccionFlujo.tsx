import { Box, Typography } from '@mui/material';
import { CampoNumero } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { atajoDe, describirCondicion } from '../condiciones';
import type { BorradorFlujo, ErrorFlujo } from '../editor';
import { PRIORIDAD_MAXIMA } from '../seleccion';
import { SelectorDeCondicion } from './SelectorDeCondicion';

interface Props {
  borrador: BorradorFlujo;
  onCambiar: (borrador: BorradorFlujo) => void;
  errores: readonly ErrorFlujo[];
  soloLectura: boolean;
}

/**
 * Selección automática del flujo (FR-1): al abrir un expediente sin flujo explícito, el sistema evalúa los flujos publicados del tipo
 * de vínculo por prioridad (la menor primero) y gana el primero cuya condición cumplen el tercero y el vínculo; si ninguno la cumple, va el
 * flujo por defecto. Es el mismo editor de condiciones de los requisitos y la condición se congela con la versión publicada. Sin
 * condición el flujo solo se usa si lo eligen a mano o si es el flujo por defecto.
 */
export function EditorSeleccionFlujo({ borrador, onCambiar, errores, soloLectura }: Props) {
  const { t } = useTranslation();
  const errorCondicion = errores.find((e) => e.campo === 'seleccion.condicion');
  const errorPrioridad = errores.find((e) => e.campo === 'seleccion.prioridad');
  const condicion = borrador.seleccionCondicion;
  const atajo = atajoDe(condicion);

  return (
    <Box component="fieldset" disabled={soloLectura} sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, mb: 1.5 }}>{t('flujoSeleccion.ayuda')}</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'minmax(260px,360px) 160px' }, gap: 2, alignItems: 'start' }}>
        <SelectorDeCondicion
          etiqueta={t('flujoSeleccion.seEscogeCuando')}
          condicion={condicion}
          onCambiar={(nueva) => onCambiar({ ...borrador, seleccionCondicion: nueva, seleccionPrioridad: nueva === null ? '' : borrador.seleccionPrioridad })}
          deshabilitado={soloLectura}
          textoDe={(a) => t(`flujoSeleccion.atajos.${a}`)}
          ayuda={errorCondicion ? t(`flujos.editor.errores.${errorCondicion.codigo}`) : undefined}
        />
        {condicion !== null && (
          <CampoNumero
            etiqueta={t('flujoSeleccion.prioridad')}
            valor={borrador.seleccionPrioridad}
            maxDigitos={String(PRIORIDAD_MAXIMA).length}
            deshabilitado={soloLectura}
            onCambiar={(texto) => onCambiar({ ...borrador, seleccionPrioridad: texto })}
            error={Boolean(errorPrioridad)}
            ayuda={errorPrioridad ? t(`flujos.editor.errores.${errorPrioridad.codigo}`) : t('flujoSeleccion.prioridadAyuda')}
          />
        )}
      </Box>
      {condicion !== null && atajo === 'personalizada' && (
        <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 1 }}>{t('flujoSeleccion.personalizada', { condicion: describirCondicion(condicion, t) })}</Typography>
      )}
      {condicion === null && <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 1 }}>{t('flujoSeleccion.sinCondicion')}</Typography>}
    </Box>
  );
}
