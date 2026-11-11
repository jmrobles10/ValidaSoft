import { Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { EsqueletoTabla, EstadoVacio } from '@validasoft/ui';
import { Users } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePersonasDelTerceroSagrilaft } from '../api';
import { marcasDePersona, participacionEnTexto } from '../informeSagrilaft';

/** Personas relacionadas de un tercero (representante, suplentes, socios y PEP de su ficha) con su participación, PEP y beneficiario final. */
export function DialogoPersonasTercero({ empresaId, terceroId, tercero, onCerrar }: { empresaId: string; terceroId: string; tercero: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const consulta = usePersonasDelTerceroSagrilaft(empresaId, terceroId);
  const personas = consulta.data?.personas ?? [];

  return (
    <Dialog open onClose={onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 760, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-personas-tercero">
      <DialogTitle id="titulo-personas-tercero">
        {t('cumplimiento.sagrilaft.personas.titulo', { tercero })}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
          {t('cumplimiento.sagrilaft.personas.ayuda')}
        </Typography>
      </DialogTitle>
      <DialogContent>
        {consulta.isPending && <EsqueletoTabla columnas={5} filas={3} etiqueta={t('cumplimiento.sagrilaft.personas.titulo', { tercero })} />}
        {consulta.isError && <ErrorRecuperable mensaje={`${t('cumplimiento.sagrilaft.personas.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
        {consulta.data && personas.length === 0 && <EstadoVacio icono={Users} titulo={t('cumplimiento.sagrilaft.personas.sin')} compacto />}
        {personas.length > 0 && (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small" aria-label={t('cumplimiento.sagrilaft.personas.titulo', { tercero })}>
              <TableHead>
                <TableRow>
                  {(['persona', 'rol', 'identificacion', 'participacion', 'marcas'] as const).map((c) => (
                    <TableCell key={c} align={c === 'participacion' ? 'right' : 'left'}>
                      {t(`cumplimiento.sagrilaft.personas.columnas.${c}`)}
                    </TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {personas.map((p) => {
                  const marcas = marcasDePersona(p);
                  return (
                    <TableRow key={`${p.rol}-${p.identificacion}`}>
                      <TableCell>{p.nombre}</TableCell>
                      <TableCell>{t(`cumplimiento.roles.${p.rol as 'Tercero'}`, { defaultValue: p.rol })}</TableCell>
                      <TableCell>{`${p.tipoIdentificacion} ${p.identificacion}`}</TableCell>
                      <TableCell align="right">{participacionEnTexto(p.participacion) ?? '—'}</TableCell>
                      <TableCell>
                        {marcas.length === 0 ? (
                          t('cumplimiento.sagrilaft.personas.ninguna')
                        ) : (
                          <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}>
                            {marcas.map((m) => (
                              <Chip key={m} size="small" variant="outlined" color={m === 'beneficiarioFinal' ? 'default' : 'warning'} label={t(`cumplimiento.sagrilaft.personas.${m}`)} />
                            ))}
                          </Stack>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} sx={{ color: 'text.primary' }}>
          {t('cumplimiento.sagrilaft.personas.cerrar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
