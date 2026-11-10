import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { CambioDeCampoDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { filasDeCambio, type FilaDeCambio } from '../secciones';

/** Tono de cada tipo de cambio: agregado en verde, quitado tachado, modificado en ámbar. */
const TONO = { Agregado: 'exito', Quitado: 'neutro', Modificado: 'advertencia' } as const;

/**
 * Las Actualizaciones del SRM (FM-28): solo lo que cambió entre la versión que la empresa aceptó y la nueva, en tres
 * columnas (campo, antes, ahora). Lo agregado va en verde, lo quitado tachado y lo modificado en ámbar; las filas de
 * personas e ICA se leen como «Se agregó …» o «Se quitó …».
 */
export function ComparacionSeccion({ cambios, versionAceptada }: { cambios: readonly CambioDeCampoDto[]; versionAceptada: number }) {
  const { t } = useTranslation();
  const titulo = t('fichaEmpresa.comparacion.titulo', { n: String(versionAceptada) });
  if (cambios.length === 0) {
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('fichaEmpresa.comparacion.sinCambios', { n: String(versionAceptada) })}
      </Typography>
    );
  }
  return (
    <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px` }}>
      <Table size="small" aria-label={titulo}>
        <TableHead>
          <TableRow>
            <TableCell>{t('fichaEmpresa.comparacion.campo')}</TableCell>
            <TableCell>{t('fichaEmpresa.comparacion.antes')}</TableCell>
            <TableCell>{t('fichaEmpresa.comparacion.ahora')}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {filasDeCambio(cambios, t).map((fila) => (
            <FilaCambio key={fila.clave} fila={fila} />
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

function FilaCambio({ fila }: { fila: FilaDeCambio }) {
  const { t } = useTranslation();
  const tono = TONO[fila.tipo];
  const vacio = t('fichaEmpresa.comparacion.vacio');
  return (
    <TableRow data-tipo-cambio={fila.tipo} aria-label={`${fila.campo}: ${t(`fichaEmpresa.comparacion.tipos.${fila.tipo}`)}`}>
      <TableCell sx={{ borderLeft: 3, borderLeftColor: `estado.${tono}.borde`, fontWeight: 500 }}>{fila.campo}</TableCell>
      <TableCell sx={{ color: 'text.secondary', textDecoration: fila.tipo === 'Quitado' ? 'line-through' : undefined }}>{fila.antes ?? vacio}</TableCell>
      <TableCell sx={fila.tipo === 'Quitado' ? { color: 'text.secondary' } : { color: `estado.${tono}.texto`, bgcolor: `estado.${tono}.fondo` }}>{fila.ahora ?? vacio}</TableCell>
    </TableRow>
  );
}
