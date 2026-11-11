import { Box, Button, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import type { FilaTerceroSagrilafDto } from '@validasoft/api-client';
import { EstadoVacio } from '@validasoft/ui';
import { Users } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { ESTADOS_DE_ESTUDIO, esEstadoDeEstudio, filtrarFilas, hayFiltros, SIN_FILTROS, TAMANO_DE_PAGINA, type FiltrosSagrilaft } from '../informeSagrilaft';
import { DialogoPersonasTercero } from './DialogoPersonasTercero';
import { FilaSagrilaft } from './FilaSagrilaft';

const NIVELES = ['1', '2', '3'] as const;

/** Tabla por tercero con filtros por estado del estudio y por nivel de riesgo; de a 25 y «Mostrar más». */
export function TablaSagrilaft({ filas, empresaId }: { filas: readonly FilaTerceroSagrilafDto[]; empresaId: string }) {
  const { t } = useTranslation();
  const [filtros, setFiltros] = useState<FiltrosSagrilaft>(SIN_FILTROS);
  const [visibles, setVisibles] = useState(TAMANO_DE_PAGINA);
  const [personas, setPersonas] = useState<{ terceroId: string; tercero: string } | null>(null);
  const filtradas = filtrarFilas(filas, filtros);
  const cambiar = (cambio: Partial<FiltrosSagrilaft>) => {
    setFiltros((f) => ({ ...f, ...cambio }));
    setVisibles(TAMANO_DE_PAGINA);
  };

  return (
    <Tarjeta titulo={t('cumplimiento.sagrilaft.tabla.titulo')} etiqueta={t('cumplimiento.sagrilaft.tabla.titulo')} nivel="h3">
      {filas.length === 0 ? (
        <EstadoVacio icono={Users} titulo={t('cumplimiento.sagrilaft.tabla.vacio')} texto={t('cumplimiento.sagrilaft.tabla.vacioAyuda')} compacto />
      ) : (
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <TextField
              select
              label={t('cumplimiento.sagrilaft.tabla.filtros.estudio')}
              value={filtros.estudio}
              onChange={(e) => cambiar({ estudio: esEstadoDeEstudio(e.target.value) ? e.target.value : '' })}
              slotProps={{ select: { displayEmpty: true } }}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="">{t('cumplimiento.sagrilaft.tabla.filtros.todos')}</MenuItem>
              {ESTADOS_DE_ESTUDIO.map((e) => (
                <MenuItem key={e} value={e}>
                  {t(`cumplimiento.sagrilaft.tabla.estudios.${e}`)}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              label={t('cumplimiento.sagrilaft.tabla.filtros.riesgo')}
              value={filtros.riesgo}
              onChange={(e) => cambiar({ riesgo: NIVELES.find((n) => n === e.target.value) ?? '' })}
              slotProps={{ select: { displayEmpty: true } }}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="">{t('cumplimiento.sagrilaft.tabla.filtros.todos')}</MenuItem>
              {NIVELES.map((n) => (
                <MenuItem key={n} value={n}>
                  {t(`cumplimiento.riesgos.r${n}`)}
                </MenuItem>
              ))}
            </TextField>
            {hayFiltros(filtros) && (
              <Button size="small" variant="text" onClick={() => cambiar(SIN_FILTROS)}>
                {t('cumplimiento.sagrilaft.tabla.limpiar')}
              </Button>
            )}
            <Box sx={{ flex: 1 }} />
            <Typography role="status" sx={{ color: 'text.secondary', fontSize: 12 }}>
              {t('cumplimiento.sagrilaft.tabla.cuenta', { n: String(Math.min(visibles, filtradas.length)), total: String(filtradas.length) })}
            </Typography>
          </Stack>
          {filtradas.length === 0 ? (
            <EstadoVacio icono={Users} titulo={t('cumplimiento.sagrilaft.tabla.sinCoincidencias')} compacto />
          ) : (
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small" aria-label={t('cumplimiento.sagrilaft.tabla.aria')}>
                <TableHead>
                  <TableRow>
                    {(['tercero', 'listas', 'estudio', 'riesgo', 'proxima', 'personas'] as const).map((c) => (
                      <TableCell key={c} align={c === 'personas' ? 'right' : 'left'}>
                        {t(`cumplimiento.sagrilaft.tabla.columnas.${c}`)}
                      </TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filtradas.slice(0, visibles).map((f) => (
                    <FilaSagrilaft key={f.vinculoId} fila={f} empresaId={empresaId} onVerPersonas={() => setPersonas({ terceroId: f.terceroId, tercero: f.tercero })} />
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
          {filtradas.length > visibles && (
            <Box sx={{ textAlign: 'center' }}>
              <Button size="small" variant="outlined" onClick={() => setVisibles((v) => v + TAMANO_DE_PAGINA)}>
                {t('cumplimiento.sagrilaft.tabla.mostrarMas')}
              </Button>
            </Box>
          )}
        </Stack>
      )}
      {personas && <DialogoPersonasTercero empresaId={empresaId} terceroId={personas.terceroId} tercero={personas.tercero} onCerrar={() => setPersonas(null)} />}
    </Tarjeta>
  );
}
