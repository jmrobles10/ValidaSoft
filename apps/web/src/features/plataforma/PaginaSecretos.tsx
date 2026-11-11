import { Alert, Button, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { SecretosPorTipoDto } from '@validasoft/api-client';
import { Dato, MarcoTabla, RejillaDatos } from '@validasoft/ui';
import { RefreshCw, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useEstadoSecretos, useRecifrarSecretos } from './api';

const pendientes = (t: SecretosPorTipoDto) => t.total - t.vigentes - t.fallidos;

/** Secretos en reposo y su clave maestra (PT-3): qué falta re-cifrar tras rotarla y el botón para hacerlo. */
export function PaginaSecretos() {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const estado = useEstadoSecretos();
  const recifrar = useRecifrarSecretos();
  const [confirmando, setConfirmando] = useState(false);

  const datos = estado.data;
  const porRecifrar = datos?.porTipo.reduce((suma, x) => suma + pendientes(x), 0) ?? 0;
  const ilegibles = datos?.porTipo.some((x) => x.fallidos > 0) ?? false;

  const confirmar = () =>
    recifrar.mutate(undefined, {
      onSuccess: (r) => mostrar(t('plataforma.secretos.recifrados', { cantidad: String(r.porTipo.reduce((s, x) => s + x.recifrados, 0)) })),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setConfirmando(false),
    });

  return (
    <>
      <EncabezadoPagina
        titulo={t('plataforma.secretos.titulo')}
        subtitulo={t('plataforma.secretos.subtitulo')}
        acciones={
          <Stack direction="row" spacing={1}>
            <Button variant="text" startIcon={<RefreshCw size={16} aria-hidden />} onClick={() => void estado.refetch()} disabled={estado.isFetching}>
              {t('plataforma.secretos.revisar')}
            </Button>
            <Button variant="contained" startIcon={<ShieldCheck size={16} aria-hidden />} disabled={porRecifrar === 0 || recifrar.isPending} onClick={() => setConfirmando(true)}>
              {t('plataforma.secretos.recifrar')}
            </Button>
          </Stack>
        }
      />
      {estado.isError && <ErrorRecuperable mensaje={mensajeDeError(estado.error, t)} onReintentar={() => void estado.refetch()} />}
      {datos && (
        <Stack spacing={2}>
          <Tarjeta>
            <RejillaDatos columnas={2}>
              <Dato etiqueta={t('plataforma.secretos.claveVigente')}>
                <Chip size="small" label={datos.claveVigente} sx={{ fontFamily: (th) => th.typography.mono.fontFamily }} />
              </Dato>
              <Dato etiqueta={t('plataforma.secretos.clavesAnteriores')}>
                {datos.clavesAnteriores.length === 0 ? (
                  <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                    {t('plataforma.secretos.ninguna')}
                  </Typography>
                ) : (
                  datos.clavesAnteriores.map((k) => <Chip key={k} size="small" variant="outlined" label={k} sx={{ mr: 0.5, fontFamily: (th) => th.typography.mono.fontFamily }} />)
                )}
              </Dato>
            </RejillaDatos>
          </Tarjeta>
          {porRecifrar === 0 && !ilegibles && <Alert severity="success">{t('plataforma.secretos.todoVigente')}</Alert>}
          {ilegibles && <Alert severity="warning">{t('plataforma.secretos.ayudaIlegibles')}</Alert>}
          <MarcoTabla>
            <TableContainer>
              <Table size="small" aria-label={t('plataforma.secretos.titulo')}>
                <TableHead>
                  <TableRow>
                    <TableCell>{t('plataforma.secretos.columnas.tipo')}</TableCell>
                    <TableCell align="right">{t('plataforma.secretos.columnas.total')}</TableCell>
                    <TableCell align="right">{t('plataforma.secretos.columnas.vigentes')}</TableCell>
                    <TableCell align="right">{t('plataforma.secretos.columnas.pendientes')}</TableCell>
                    <TableCell align="right">{t('plataforma.secretos.columnas.fallidos')}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {datos.porTipo.map((x) => (
                    <TableRow key={x.tipo}>
                      <TableCell>{t(`plataforma.secretos.tipos.${x.tipo as 'llave-openai' | 'secreto-webhook' | 'cuenta-bancaria'}`)}</TableCell>
                      <TableCell align="right">{x.total}</TableCell>
                      <TableCell align="right">{x.vigentes}</TableCell>
                      <TableCell align="right">{pendientes(x)}</TableCell>
                      <TableCell align="right">{x.fallidos}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </MarcoTabla>
        </Stack>
      )}
      {confirmando && (
        <DialogoConfirmacion
          titulo={t('plataforma.secretos.confirmarTitulo')}
          mensaje={t('plataforma.secretos.confirmar')}
          confirmar={t('plataforma.secretos.recifrar')}
          tono="primary"
          ocupado={recifrar.isPending}
          onConfirmar={confirmar}
          onCerrar={() => setConfirmando(false)}
        />
      )}
    </>
  );
}
