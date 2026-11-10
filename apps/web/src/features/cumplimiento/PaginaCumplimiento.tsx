import { Box, Button, Chip, Stack, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, Typography } from '@mui/material';
import { SCOPES, type FilaCumplimientoDto } from '@validasoft/api-client';
import { ChipEstado, EstadoVacio, formatearFecha } from '@validasoft/ui';
import { ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useSearchParams } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useBandejaCumplimiento } from './api';
import { InformeCumplimiento } from './components/InformeCumplimiento';

type Pestana = 'bandeja' | 'informe';

/**
 * Pantalla del oficial de cumplimiento (H7): su bandeja (expedientes con listas por consultar, coincidencias por analizar,
 * estudios pendientes o vencidos, y vínculos activos cuyo monitoreo encontró algo nuevo) y el informe SAGRILAFT.
 */
export function PaginaCumplimiento() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const [parametros, setParametros] = useSearchParams();
  const puedeBandeja = permisos.puede(SCOPES.expedientesLeer);
  const puedeInforme = permisos.puede(SCOPES.auditoriaLeer);
  const pedida = parametros.get('pestana') === 'informe' ? 'informe' : 'bandeja';
  const pestana: Pestana = pedida === 'informe' && puedeInforme ? 'informe' : puedeBandeja ? 'bandeja' : 'informe';

  if (!puedeBandeja && !puedeInforme) return <SinPermiso seccion={t('nav.cumplimiento')} />;

  return (
    <>
      <EncabezadoPagina titulo={t('cumplimiento.titulo')} subtitulo={t('cumplimiento.subtitulo', { empresa: empresa.nombre })} />
      <Tabs
        value={pestana}
        onChange={(_, valor: Pestana) => setParametros(valor === 'bandeja' ? {} : { pestana: valor }, { replace: true })}
        aria-label={t('cumplimiento.titulo')}
        sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}
      >
        {puedeBandeja && <Tab value="bandeja" label={t('cumplimiento.pestanas.bandeja')} />}
        {puedeInforme && <Tab value="informe" label={t('cumplimiento.pestanas.informe')} />}
      </Tabs>
      {pestana === 'bandeja' ? <BandejaCumplimiento empresaId={empresa.id} /> : <InformeCumplimiento empresaId={empresa.id} />}
    </>
  );
}

function BandejaCumplimiento({ empresaId }: { empresaId: string }) {
  const { t } = useTranslation();
  const consulta = useBandejaCumplimiento(empresaId);
  if (consulta.isError) return <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />;
  const filas = consulta.data ?? [];

  return (
    <Tarjeta>
      {!consulta.isPending && filas.length === 0 ? (
        <EstadoVacio icono={ShieldCheck} titulo={t('cumplimiento.bandeja.vacia')} texto={t('cumplimiento.bandeja.vaciaAyuda')} />
      ) : (
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small" aria-busy={consulta.isPending} aria-label={t('cumplimiento.pestanas.bandeja')}>
            <TableHead>
              <TableRow>
                <TableCell>{t('cumplimiento.bandeja.columnas.tercero')}</TableCell>
                <TableCell>{t('cumplimiento.bandeja.columnas.folio')}</TableCell>
                <TableCell>{t('cumplimiento.bandeja.columnas.pendientes')}</TableCell>
                <TableCell align="right">{t('cumplimiento.bandeja.columnas.coincidencias')}</TableCell>
                <TableCell>{t('cumplimiento.bandeja.columnas.desde')}</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {filas.map((f) => (
                <FilaBandeja key={`${f.origen}-${f.expedienteId ?? f.vinculoId}`} fila={f} empresaId={empresaId} />
              ))}
            </TableBody>
          </Table>
        </Box>
      )}
    </Tarjeta>
  );
}

function FilaBandeja({ fila: f, empresaId }: { fila: FilaCumplimientoDto; empresaId: string }) {
  const { t } = useTranslation();
  const destino = f.expedienteId ? `/e/${empresaId}/expedientes/${f.expedienteId}` : `/e/${empresaId}/vinculos/${f.vinculoId}`;
  return (
    <TableRow hover>
      <TableCell>
        <Typography sx={{ fontWeight: 500, fontSize: 14 }}>{f.tercero.nombre}</Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>
          {t(`cumplimiento.bandeja.origenes.${f.origen}`)}
          {f.tipoVinculo ? ` · ${f.tipoVinculo}` : ''}
        </Typography>
      </TableCell>
      <TableCell>
        <Typography component="span" variant="mono">
          {f.folio}
        </Typography>
      </TableCell>
      <TableCell>
        <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}>
          {f.pendientes.map((p) => (
            <Box key={p.tipo} sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
              <Typography component="span" sx={{ fontSize: 13 }}>
                {t(`cumplimiento.tipos.${p.tipo}`)}
              </Typography>
              <ChipEstado estado={p.estado} etiqueta={t(`cumplimiento.estados.${p.estado}`)} />
            </Box>
          ))}
          {f.esperandoSoloVerificaciones && <Chip size="small" color="warning" variant="outlined" label={t('cumplimiento.bandeja.soloFalta')} />}
        </Stack>
      </TableCell>
      <TableCell align="right">{String(f.coincidenciasPorAnalizar)}</TableCell>
      <TableCell>{formatearFecha(f.desde)}</TableCell>
      <TableCell align="right">
        <Button component={RouterLink} to={destino} size="small" variant="outlined">
          {t('cumplimiento.bandeja.abrir')}
        </Button>
      </TableCell>
    </TableRow>
  );
}
