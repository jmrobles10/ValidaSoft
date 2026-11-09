import { Stack } from '@mui/material';
import type { DatoDeclaradoDto, VigenciaDto } from '@validasoft/api-client';
import { DatosDeclarados } from './DatosDeclarados';
import { AvisoVigencia, VigenciaDocumento } from './VigenciaDocumento';

export interface ResumenVersionProps {
  datos: readonly DatoDeclaradoDto[];
  vigencia?: VigenciaDto | null;
  /** Cuántos datos se muestran antes de «Ver n datos más» (sin límite, todos). */
  limite?: number;
  conTitulo?: boolean;
}

/**
 * Lo que se revisa de una versión además del archivo (o en lugar de él, si es un formulario): el aviso y el estado de
 * su vigencia y los datos declarados.
 */
export function ResumenVersion({ datos, vigencia, limite, conTitulo }: ResumenVersionProps) {
  if (datos.length === 0 && !vigencia) return null;
  return (
    <Stack spacing={1}>
      {vigencia && <AvisoVigencia vigencia={vigencia} />}
      {vigencia && <VigenciaDocumento vigencia={vigencia} />}
      <DatosDeclarados datos={datos} limite={limite} conTitulo={conTitulo} />
    </Stack>
  );
}
