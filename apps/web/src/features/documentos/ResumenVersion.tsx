import { Stack } from '@mui/material';
import type { DatoDeclaradoDto, HechosDto, VigenciaDto } from '@validasoft/api-client';
import { estadoRevision } from './revisionIa';
import { DatosDeclarados } from './DatosDeclarados';
import { RevisionAutomatica, type TipoEsperado } from './RevisionAutomatica';
import { AvisoVigencia, VigenciaDocumento } from './VigenciaDocumento';

export interface ResumenVersionProps {
  datos: readonly DatoDeclaradoDto[];
  vigencia?: VigenciaDto | null;
  /** Cuántos datos se muestran antes de «Ver n datos más» (sin límite, todos). */
  limite?: number;
  conTitulo?: boolean;
  /** Hechos de la versión con archivo: la revisión automática (IA-11). Una versión de solo formulario no los pasa. */
  hechos?: HechosDto | null;
  tipoEsperado?: TipoEsperado | null;
}

/**
 * Lo que se revisa de una versión además del archivo (o en lugar de él, si es un formulario): el aviso y el estado de
 * su vigencia, los datos declarados y la revisión automática de la IA.
 */
export function ResumenVersion({ datos, vigencia, limite, conTitulo, hechos, tipoEsperado }: ResumenVersionProps) {
  const conRevision = estadoRevision(hechos) !== 'nada';
  if (datos.length === 0 && !vigencia && !conRevision) return null;
  return (
    <Stack spacing={1}>
      {vigencia && <AvisoVigencia vigencia={vigencia} />}
      {vigencia && <VigenciaDocumento vigencia={vigencia} />}
      <DatosDeclarados datos={datos} limite={limite} conTitulo={conTitulo} />
      {conRevision && <RevisionAutomatica hechos={hechos} tipoEsperado={tipoEsperado} limite={limite} />}
    </Stack>
  );
}
