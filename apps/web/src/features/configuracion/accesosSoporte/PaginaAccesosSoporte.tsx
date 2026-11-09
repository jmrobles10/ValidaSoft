import { Box, Button, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getListarAccesosSoporteQueryOptions, revocarAccesoSoporte, SCOPES, type AccesoSoporteDto } from '@validasoft/api-client';
import { EstadoVacio, FilasEsqueleto, formatearFechaHora, MarcoTabla } from '@validasoft/ui';
import { LockKeyhole, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva, useEmpresas } from '../../empresas/useEmpresas';
import { EstadoActivo } from '../EstadoActivo';
import { DialogoConcederAcceso } from './DialogoConcederAcceso';
import { estadoDe, restante } from './modelo';

const COLUMNAS = 5;

/**
 * Accesos temporales de soporte de la Plataforma (B-6, CF-2): nunca hay uno por defecto; el administrador del grupo lo
 * concede para una empresa, con duración y justificación, y lo puede revocar antes de que expire.
 */
export function PaginaAccesosSoporte() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.empresasAdministrar);
  const consulta = useQuery({ ...getListarAccesosSoporteQueryOptions({ request: { empresa: null } }), enabled: puede, refetchInterval: 60_000 });
  const empresas = useEmpresas();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [concediendo, setConcediendo] = useState(false);
  const [porRevocar, setPorRevocar] = useState<AccesoSoporteDto | null>(null);
  const revocar = useMutation({
    mutationFn: (a: AccesoSoporteDto) => revocarAccesoSoporte(a.id, { empresa: null }),
    onSuccess: () => mostrar(t('accesosSoporte.revocado')),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => {
      setPorRevocar(null);
      return invalidarRuta(cliente, '/api/v1/accesos-soporte');
    },
  });

  if (!puede) return <SinPermiso seccion={t('nav.accesos-soporte')} />;

  const ahora = new Date();
  const accesos = consulta.data ?? [];
  const textoEstado = (a: AccesoSoporteDto) => {
    const estado = estadoDe(a);
    const queda = estado === 'vigente' ? restante(a.expiraEn, ahora) : null;
    return queda ? t('accesosSoporte.vigenteQueda', { horas: String(queda.horas), minutos: String(queda.minutos) }) : t(`accesosSoporte.estados.${estado}`);
  };

  return (
    <>
      <EncabezadoPagina
        titulo={t('accesosSoporte.titulo')}
        subtitulo={t('accesosSoporte.ayuda')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setConcediendo(true)} disabled={!empresas.data}>
            {t('accesosSoporte.conceder')}
          </Button>
        }
      />
      {consulta.isError && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('accesosSoporte.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('accesosSoporte.columnas.empresa')}</TableCell>
                <TableCell>{t('accesosSoporte.columnas.justificacion')}</TableCell>
                <TableCell sx={{ width: 170 }}>{t('accesosSoporte.columnas.expira')}</TableCell>
                <TableCell sx={{ width: 170 }}>{t('accesosSoporte.columnas.estado')}</TableCell>
                <TableCell sx={{ width: 110 }}>
                  <Box component="span" sx={{ position: 'absolute', width: '1px', height: '1px', overflow: 'hidden', clip: 'rect(0 0 0 0)' }}>
                    {t('accesosSoporte.columnas.acciones')}
                  </Box>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={COLUMNAS} filas={2} />}
              {consulta.data && accesos.length === 0 && (
                <TableRow>
                  <TableCell colSpan={COLUMNAS} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={LockKeyhole} titulo={t('accesosSoporte.vacio')} texto={t('accesosSoporte.vacioAyuda')} />
                  </TableCell>
                </TableRow>
              )}
              {accesos.map((a) => (
                <TableRow key={a.id}>
                  <TableCell sx={{ fontWeight: 500 }}>{a.empresa}</TableCell>
                  <TableCell>
                    <Typography variant="body2">{a.justificacion}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{formatearFechaHora(a.expiraEn)}</Typography>
                  </TableCell>
                  <TableCell>
                    <EstadoActivo activo={a.vigente} texto={textoEstado(a)} />
                  </TableCell>
                  <TableCell align="right">
                    {a.vigente && (
                      <Button variant="text" color="error" size="small" onClick={() => setPorRevocar(a)} aria-label={t('accesosSoporte.revocarAccesible', { empresa: a.empresa })}>
                        {t('accesosSoporte.revocar')}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {concediendo && empresas.data && <DialogoConcederAcceso empresas={empresas.data} empresaInicial={empresa.id} onCerrar={() => setConcediendo(false)} />}
      {porRevocar && (
        <DialogoConfirmacion
          titulo={t('accesosSoporte.revocarTitulo', { empresa: porRevocar.empresa })}
          mensaje={t('accesosSoporte.revocarTexto')}
          confirmar={t('accesosSoporte.revocar')}
          ocupado={revocar.isPending}
          onConfirmar={() => revocar.mutate(porRevocar)}
          onCerrar={() => setPorRevocar(null)}
        />
      )}
    </>
  );
}
