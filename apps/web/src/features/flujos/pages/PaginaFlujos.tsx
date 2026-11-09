import { Box, Button, Chip, MenuItem, Skeleton, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { SCOPES, type FlujoDto } from '@validasoft/api-client';
import { EstadoVacio, MarcoTabla } from '@validasoft/ui';
import { Plus, Star } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useTiposVinculo } from '../../vinculos/api';
import { useDefinirPorDefecto, useFlujos } from '../api';

/** Flujos aprobatorios de la empresa activa y el flujo por defecto de cada tipo de vínculo (FL-2, FL-4). */
export function PaginaFlujos() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.flujosLeer);
  const puedeEscribir = permisos.puede(SCOPES.flujosEscribir);
  const consulta = useFlujos(empresa.id, undefined, puedeLeer);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.flujos')} />;

  const flujos = consulta.data ?? [];
  const abrir = (f: FlujoDto) => void navegar(`/e/${empresa.id}/flujos/${f.id}`);

  return (
    <>
      <EncabezadoPagina
        titulo={t('flujos.titulo', { empresa: empresa.nombre })}
        subtitulo={t('flujos.ayuda')}
        acciones={
          puedeEscribir ? (
            <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => void navegar(`/e/${empresa.id}/flujos/nuevo`)}>
              {t('flujos.nuevo')}
            </Button>
          ) : undefined
        }
      />
      {consulta.isError && <ErrorRecuperable mensaje={`${t('flujos.errorLista')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      <Stack spacing={2}>
        <MarcoTabla>
          <Table size="small" aria-label={t('nav.flujos')}>
            <TableHead>
              <TableRow>
                <TableCell>{t('flujos.columnas.flujo')}</TableCell>
                <TableCell sx={{ width: 150 }}>{t('flujos.columnas.tipo')}</TableCell>
                <TableCell sx={{ width: 120 }}>{t('flujos.columnas.etapas')}</TableCell>
                <TableCell sx={{ width: 130 }}>{t('flujos.columnas.publicada')}</TableCell>
                <TableCell sx={{ width: 110 }}>{t('flujos.columnas.borrador')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending &&
                [0, 1, 2].map((i) => (
                  <TableRow key={i}>
                    {[0, 1, 2, 3, 4].map((j) => (
                      <TableCell key={j}>
                        <Skeleton variant="rounded" height={12} />
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              {flujos.map((f) => {
                const vigente = f.publicada ?? f.borrador;
                return (
                  <TableRow
                    key={f.id}
                    hover
                    tabIndex={0}
                    aria-label={t('flujos.abrir', { nombre: f.nombre })}
                    onClick={() => abrir(f)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') abrir(f);
                    }}
                    sx={{ cursor: 'pointer' }}
                  >
                    <TableCell>
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <Typography component="span" sx={{ fontWeight: 500 }}>
                          {f.nombre}
                        </Typography>
                        <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
                          {f.codigo}
                        </Typography>
                        {f.esPorDefecto && <Chip size="small" variant="outlined" icon={<Star size={12} aria-hidden />} label={t('flujos.porDefecto')} />}
                      </Stack>
                    </TableCell>
                    <TableCell>{f.tipoVinculo.nombre}</TableCell>
                    <TableCell>{vigente ? t('flujos.etapas', { count: vigente.etapas.length }) : '—'}</TableCell>
                    <TableCell>
                      {f.publicada ? (
                        <Typography component="span" variant="monoTabla">
                          {t('flujos.version', { version: String(f.publicada.version) })}
                        </Typography>
                      ) : (
                        <Typography component="span" sx={{ color: 'text.secondary' }}>
                          {t('flujos.sinPublicar')}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography component="span" variant="monoTabla" color={f.borrador ? 'text.primary' : 'text.secondary'}>
                        {f.borrador ? t('flujos.version', { version: String(f.borrador.version) }) : '—'}
                      </Typography>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
          {consulta.isSuccess && flujos.length === 0 && (
            <EstadoVacio compacto titulo={t('flujos.vacio', { empresa: empresa.nombre })} texto={t('flujos.vacioAyuda')} />
          )}
        </MarcoTabla>
        {consulta.isSuccess && <PorDefecto empresaId={empresa.id} flujos={flujos} editable={puedeEscribir} />}
      </Stack>
    </>
  );
}

/** Un selector por tipo de vínculo con sus flujos publicados. */
function PorDefecto({ empresaId, flujos, editable }: { empresaId: string; flujos: readonly FlujoDto[]; editable: boolean }) {
  const { t } = useTranslation();
  const permisos = usePermisos(empresaId);
  const tipos = useTiposVinculo(empresaId, permisos.puede(SCOPES.configuracionLeer));
  const mostrar = useAvisos((s) => s.mostrar);
  const mutacion = useDefinirPorDefecto(empresaId);

  if (!tipos.data) return null;

  return (
    <Tarjeta titulo={t('flujos.porDefectoTitulo')}>
      <Typography sx={{ color: 'text.secondary', mb: 2 }}>
        {t('flujos.porDefectoAyuda')}
      </Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', md: 'repeat(2, minmax(0,1fr))' }, gap: 2 }}>
        {tipos.data
          .filter((tipo) => tipo.activo)
          .map((tipo) => {
            const publicados = flujos.filter((f) => f.tipoVinculo.id === tipo.id && f.publicada);
            const actual = publicados.find((f) => f.esPorDefecto)?.id ?? '';
            return (
              <TextField
                key={tipo.id}
                select
                label={tipo.nombre}
                value={actual}
                disabled={!editable || publicados.length === 0 || mutacion.isPending}
                onChange={(e) =>
                  mutacion.mutate(
                    { tipoVinculoId: tipo.id, flujoId: e.target.value },
                    {
                      onSuccess: () => mostrar(t('flujos.porDefectoGuardado', { tipo: tipo.nombre })),
                      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
                    },
                  )
                }
              >
                <MenuItem value="" disabled>
                  {publicados.length === 0 ? t('flujos.sinPublicados') : t('flujos.sinPorDefecto')}
                </MenuItem>
                {publicados.map((f) => (
                  <MenuItem key={f.id} value={f.id}>
                    {f.nombre} · v{f.publicada?.version}
                  </MenuItem>
                ))}
              </TextField>
            );
          })}
      </Box>
    </Tarjeta>
  );
}
