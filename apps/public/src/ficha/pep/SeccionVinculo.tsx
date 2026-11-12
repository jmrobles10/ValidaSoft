import { useTranslation } from 'react-i18next';
import { CampoSiNo } from '../campos';
import type { PepRelacionada, VinculoConPep } from '../tipos';
import { SeccionDeclaracion, type PropsSeccion } from './CamposPep';
import { FilasPepRelacionadas } from './FilasPepRelacionadas';

/** Sección 3: vínculo familiar o de asociado cercano con otras PEP (Sí/No; con «Sí», al menos una y hasta 30, cada una completa). */
export function SeccionVinculo({ d, cambiar, mensaje, hoy }: PropsSeccion) {
  const { t } = useTranslation();
  const v = d.vinculo_con_pep ?? {};
  const cambiarVinculo = (cambio: VinculoConPep) => cambiar({ vinculo_con_pep: { ...v, ...cambio } });
  const cambiarFilas = (vinculos: PepRelacionada[]) => cambiarVinculo({ vinculos });
  return (
    <SeccionDeclaracion titulo={t('ficha.pep.secciones.vinculo')}>
      <CampoSiNo
        etiqueta={t('ficha.pep.vinculo.pregunta')}
        valor={v.tiene}
        onCambiar={(tiene) => cambiarVinculo(tiene ? { tiene, vinculos: v.vinculos?.length ? v.vinculos : [{}] } : { tiene })}
        mensaje={mensaje('vinculo_con_pep.tiene')}
      />
      {v.tiene === true && <FilasPepRelacionadas filas={v.vinculos ?? []} onCambiar={cambiarFilas} mensaje={mensaje} hoy={hoy} />}
    </SeccionDeclaracion>
  );
}
