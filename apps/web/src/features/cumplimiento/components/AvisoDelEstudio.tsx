import { Alert, AlertTitle } from '@mui/material';
import type { ConceptoCumplimiento, ExpedienteDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { efectoDelEstudio } from '../modelo';

/** Antes de registrar: qué provoca el estudio con ese concepto (aprobar el expediente, esperar otra verificación o fallar) y a quién le llega. */
export function AvisoDelEstudio({ expediente, concepto, vigenciaDias }: { expediente: ExpedienteDto; concepto: ConceptoCumplimiento | null; vigenciaDias: number | null }) {
  const { t } = useTranslation();
  const efecto = efectoDelEstudio(expediente, concepto);
  const texto =
    efecto.tipo === 'aprueba'
      ? t('cumplimiento.estudio.avisoAprueba')
      : efecto.tipo === 'desfavorable'
        ? t('cumplimiento.estudio.avisoDesfavorable')
        : efecto.tipo === 'espera-verificaciones'
          ? t('cumplimiento.estudio.avisoEspera', {
              faltan: efecto.faltan.map((v) => `${t(`cumplimiento.tipos.${v.tipo}`)} (${t(`cumplimiento.estados.${v.estado}`).toLowerCase()})`).join(', '),
            })
          : efecto.tipo === 'espera-cuenta'
            ? t('cumplimiento.estudio.avisoCuenta')
            : t('cumplimiento.estudio.avisoAreas');

  return (
    <Alert severity={efecto.tipo === 'desfavorable' ? 'warning' : 'info'} aria-label={t('cumplimiento.estudio.avisoTitulo')}>
      <AlertTitle>{t('cumplimiento.estudio.avisoTitulo')}</AlertTitle>
      {texto} {vigenciaDias ? t('cumplimiento.estudio.avisoVigencia', { dias: String(vigenciaDias) }) : t('cumplimiento.estudio.avisoVigenciaSinDias')}
    </Alert>
  );
}
