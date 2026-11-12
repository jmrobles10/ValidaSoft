import { useTranslation } from 'react-i18next';
import { CampoSiNo, CampoTexto, Rejilla } from '../campos';
import type { VinculoConPep } from '../tipos';
import { CampoPeriodo, SeccionDeclaracion, type PropsSeccion } from './CamposPep';
import { MAXIMO } from './declaracion';

/** Sección 3: vínculo familiar o asociados cercanos a otra PEP (Sí/No; con «Sí», sus datos y su periodo). */
export function SeccionVinculo({ d, cambiar, mensaje, hoy }: PropsSeccion) {
  const { t } = useTranslation();
  const v = d.vinculo_con_pep ?? {};
  const cambiarVinculo = (cambio: VinculoConPep) => cambiar({ vinculo_con_pep: { ...v, ...cambio } });
  return (
    <SeccionDeclaracion titulo={t('ficha.pep.secciones.vinculo')}>
      <CampoSiNo etiqueta={t('ficha.pep.vinculo.pregunta')} valor={v.tiene} onCambiar={(tiene) => cambiarVinculo({ tiene })} mensaje={mensaje('vinculo_con_pep.tiene')} />
      {v.tiene === true && (
        <>
          <Rejilla>
            <CampoTexto etiqueta={t('ficha.pep.vinculo.nombre')} valor={v.nombre} onCambiar={(x) => cambiarVinculo({ nombre: x })} mensaje={mensaje('vinculo_con_pep.nombre')} maxLength={MAXIMO.nombre} />
            <CampoTexto etiqueta={t('ficha.pep.vinculo.parentesco')} valor={v.parentesco} onCambiar={(x) => cambiarVinculo({ parentesco: x })} mensaje={mensaje('vinculo_con_pep.parentesco')} maxLength={MAXIMO.parentesco} />
          </Rejilla>
          <CampoTexto etiqueta={t('ficha.pep.vinculo.cargoInstitucion')} valor={v.cargo_institucion} onCambiar={(x) => cambiarVinculo({ cargo_institucion: x })} mensaje={mensaje('vinculo_con_pep.cargo_institucion')} maxLength={MAXIMO.cargoInstitucion} />
          <CampoPeriodo valor={v} onCambiar={cambiarVinculo} mensaje={(campo) => mensaje(`vinculo_con_pep.${campo}`)} hoy={hoy} />
        </>
      )}
    </SeccionDeclaracion>
  );
}
