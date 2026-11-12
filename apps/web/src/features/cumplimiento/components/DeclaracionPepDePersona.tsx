import { Button, Chip, Tooltip } from '@mui/material';
import type { PersonaInformeDto } from '@validasoft/api-client';
import { FileDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useDescargarDeclaracionPep } from '../../terceros/ficha/descargaDeclaracionPep';
import { guardarArchivo } from '../../vencimientos/api';

/** Posición de la persona natural (el propio tercero): su declaración está en la General, no en `personas[]` de la legal. */
const INDICE_DEL_PROPIO_TERCERO = -1;

/**
 * La declaración jurada PEP de una persona del informe SAGRILAFT: «Descargar PDF» si la tiene y «Sin declaración» si responde «Sí» a PEP y
 * no la tiene. Quien no es PEP no lleva nada (la declaración solo existe con «Sí»).
 */
export function DeclaracionPepDePersona({ empresaId, terceroId, persona }: { empresaId: string; terceroId: string; persona: PersonaInformeDto }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const pdf = useDescargarDeclaracionPep(empresaId);
  if (!persona.esPep) return '—';
  if (!persona.declaracionPep) {
    return (
      <Tooltip title={t('cumplimiento.sagrilaft.personas.sinDeclaracionAyuda')}>
        <Chip size="small" variant="outlined" color="error" label={t('cumplimiento.sagrilaft.personas.sinDeclaracion')} />
      </Tooltip>
    );
  }

  const indice = persona.indice ?? 0;
  const propio = indice === INDICE_DEL_PROPIO_TERCERO;
  const descargar = () =>
    pdf.mutate(
      { descarga: { empresaId, terceroId, seccion: propio ? 'FICHA_GENERAL' : 'FICHA_LEGAL', version: null }, persona: propio ? null : indice },
      {
        onSuccess: (archivo) => guardarArchivo(archivo, t('fichaMaestra.pep.archivo')),
        onError: (error) => mostrar(`${t('cumplimiento.sagrilaft.personas.errorPdf')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );

  return (
    <Button
      size="small"
      variant="text"
      startIcon={<FileDown size={14} aria-hidden />}
      onClick={descargar}
      disabled={pdf.isPending}
      aria-label={t('cumplimiento.sagrilaft.personas.declaracionPdfAccesible', { nombre: persona.nombre })}
    >
      {t('cumplimiento.sagrilaft.personas.declaracionPdf')}
    </Button>
  );
}
