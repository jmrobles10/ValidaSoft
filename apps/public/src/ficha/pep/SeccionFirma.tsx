import { MenuItem, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CampoTexto, Rejilla } from '../campos';
import { FIRMAS_EN_CALIDAD, type FirmaEnCalidad } from '../tipos';
import { CampoTipoDocumento, SeccionDeclaracion, type PropsSeccion } from './CamposPep';
import { MAXIMO } from './declaracion';
import { FirmaPad } from './FirmaPad';

const esCalidad = (valor: string): valor is FirmaEnCalidad => FIRMAS_EN_CALIDAD.some((c) => c === valor);

/**
 * Sección 4 (sin el check del juramento, que va fijo al pie): en una persona de la información legal se elige si firma a
 * nombre propio o como representante legal (y, en ese caso, quién firma); la persona natural firma siempre a nombre propio.
 */
export function SeccionFirma({ d, cambiar, mensaje, basicos }: PropsSeccion) {
  const { t } = useTranslation();
  const representante = !basicos.soloNombrePropio && d.firma_en_calidad === 'RepresentanteLegal';
  return (
    <SeccionDeclaracion titulo={t('ficha.pep.secciones.juramento')}>
      {!basicos.soloNombrePropio && (
        <TextField
          select
          label={t('ficha.pep.firmante.calidad')}
          value={d.firma_en_calidad ?? 'NombrePropio'}
          onChange={(e) => cambiar({ firma_en_calidad: esCalidad(e.target.value) ? e.target.value : 'NombrePropio' })}
          fullWidth
        >
          {FIRMAS_EN_CALIDAD.map((c) => (
            <MenuItem key={c} value={c}>
              {t(`ficha.pep.firmante.calidades.${c}`)}
            </MenuItem>
          ))}
        </TextField>
      )}
      {representante && (
        <Rejilla>
          <CampoTexto etiqueta={t('ficha.pep.firmante.nombre')} valor={d.firmante_nombre} onCambiar={(v) => cambiar({ firmante_nombre: v })} mensaje={mensaje('firmante_nombre')} maxLength={MAXIMO.nombre} />
          <CampoTipoDocumento etiqueta={t('ficha.pep.firmante.tipoDocumento')} valor={d.firmante_tipo_identificacion} onCambiar={(v) => cambiar({ firmante_tipo_identificacion: v })} mensaje={mensaje('firmante_tipo_identificacion')} />
          <CampoTexto etiqueta={t('ficha.pep.firmante.numero')} valor={d.firmante_numero} onCambiar={(v) => cambiar({ firmante_numero: v })} mensaje={mensaje('firmante_numero')} maxLength={MAXIMO.numero} />
        </Rejilla>
      )}
      <FirmaPad valor={d.firma_png} onCambiar={(png) => cambiar({ firma_png: png })} error={mensaje('firma_png')} />
    </SeccionDeclaracion>
  );
}
