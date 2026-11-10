import { Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { FilaVistaPreviaDto } from '@validasoft/api-client';
import { EstadoVacio, MarcoTabla } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { conProblemas, identificacionDeFila } from './modelo';

/** Filas que se muestran a la vez: un lote puede tener miles; las que tienen problemas van primero con el filtro. */
export const MAXIMO_VISIBLE = 200;

const TONO_FILA = { Valida: 'success', Invalida: 'error', Duplicada: 'warning' } as const;

/** Vista previa del lote: cada fila con su estado, sus errores por campo o lo que se hará con ella. */
export function TablaVistaPrevia({ filas, soloErrores }: { filas: readonly FilaVistaPreviaDto[]; soloErrores: boolean }) {
  const { t } = useTranslation();
  const visibles = soloErrores ? conProblemas(filas) : filas;
  if (visibles.length === 0) return <EstadoVacio compacto titulo={t('lotes.nuevoLote.vacio')} />;

  return (
    <>
      <MarcoTabla>
        <TableContainer sx={{ maxHeight: 480 }}>
          <Table size="small" stickyHeader aria-label={t('lotes.nuevoLote.pasoRevision')}>
            <TableHead>
              <TableRow>
                <TableCell sx={{ width: 64 }}>{t('lotes.nuevoLote.columnas.fila')}</TableCell>
                <TableCell>{t('lotes.nuevoLote.columnas.identificacion')}</TableCell>
                <TableCell>{t('lotes.nuevoLote.columnas.nombre')}</TableCell>
                <TableCell>{t('lotes.nuevoLote.columnas.correo')}</TableCell>
                <TableCell>{t('lotes.nuevoLote.columnas.tipo')}</TableCell>
                <TableCell>{t('lotes.nuevoLote.columnas.resultado')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {visibles.slice(0, MAXIMO_VISIBLE).map((f) => (
                <TableRow key={f.n} data-estado={f.estado}>
                  <TableCell>
                    <Typography variant="monoTabla">{f.n}</Typography>
                  </TableCell>
                  <TableCell>{identificacionDeFila(f)}</TableCell>
                  <TableCell>{f.datos.nombre ?? '—'}</TableCell>
                  <TableCell>{f.datos.correo ?? '—'}</TableCell>
                  <TableCell>{f.datos.tipoVinculo ?? '—'}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      variant="outlined"
                      color={TONO_FILA[f.estado as keyof typeof TONO_FILA]}
                      label={t(`lotes.nuevoLote.estadoFila.${f.estado as keyof typeof TONO_FILA}`)}
                      sx={{ mr: 1 }}
                    />
                    <Typography component="span" variant="body2" sx={{ color: f.errores.length > 0 ? 'estado.error.texto' : 'text.secondary' }}>
                      {f.errores.length > 0
                        ? f.errores.map((e) => e.mensaje).join(' ')
                        : f.duplicadaDe
                          ? t('lotes.nuevoLote.duplicadaDe', { n: String(f.duplicadaDe) })
                          : f.accionPrevista
                            ? t(`lotes.nuevoLote.acciones.${f.accionPrevista as 'Invitado' | 'Reinvitado' | 'SinCambios'}`)
                            : ''}
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>
      {visibles.length > MAXIMO_VISIBLE && (
        <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 1 }}>
          {`${String(MAXIMO_VISIBLE)} / ${String(visibles.length)}`}
        </Typography>
      )}
    </>
  );
}
