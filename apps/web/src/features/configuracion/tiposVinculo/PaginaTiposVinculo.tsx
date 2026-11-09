import { Box, Button, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type TipoVinculoDto } from '@validasoft/api-client';
import { EstadoVacio, FilasEsqueleto, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Ban, CircleCheck, Link2, Pencil, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { MenuFila } from '../../../shared/MenuFila';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { EstadoActivo } from '../EstadoActivo';
import { useCambiarEstadoTipoVinculo, useTiposVinculoGrupo } from './api';
import { DrawerTipoVinculo } from './DrawerTipoVinculo';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'editar'; tipoVinculo: TipoVinculoDto };

const COLUMNAS = 5;

/** Tipos de vínculo del grupo (proveedor, cliente, empleado…) y los atributos que alimentan «Se pide a» (RQ-12). */
export function PaginaTiposVinculo() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = useTiposVinculoGrupo(puedeLeer);
  const cambiarEstado = useCambiarEstadoTipoVinculo();
  const mostrar = useAvisos((s) => s.mostrar);
  const [edicion, setEdicion] = useState<Edicion | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<TipoVinculoDto | null>(null);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.tipos-vinculo')} />;

  const tipos = [...(consulta.data ?? [])].sort((a, b) => Number(b.activo) - Number(a.activo) || a.nombre.localeCompare(b.nombre, 'es'));
  const abrir = (tipoVinculo: TipoVinculoDto) => setEdicion({ tipo: 'editar', tipoVinculo });
  const activar = (tipo: TipoVinculoDto, activo: boolean) =>
    cambiarEstado.mutate(
      { tipo, activo },
      {
        onSuccess: () => mostrar(activo ? t('tiposVinculo.activado', { nombre: tipo.nombre }) : t('tiposVinculo.desactivado', { nombre: tipo.nombre })),
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
        onSettled: () => setPorDesactivar(null),
      },
    );
  const exige = (tipo: TipoVinculoDto) =>
    [tipo.exigeCuentaDesignada && t('tiposVinculo.exige.cuenta'), tipo.exigeOtpEnEnlace && t('tiposVinculo.exige.otp')].filter(Boolean).join(' · ') || '—';

  return (
    <>
      <EncabezadoPagina
        titulo={t('tiposVinculo.titulo')}
        subtitulo={t('tiposVinculo.ayuda')}
        acciones={
          puedeEscribir ? (
            <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setEdicion({ tipo: 'crear', clave: crypto.randomUUID() })}>
              {t('tiposVinculo.nuevo')}
            </Button>
          ) : undefined
        }
      />
      {consulta.isError && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('tiposVinculo.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('tiposVinculo.columnas.tipo')}</TableCell>
                <TableCell>{t('tiposVinculo.columnas.atributos')}</TableCell>
                <TableCell sx={{ width: 220 }}>{t('tiposVinculo.columnas.exige')}</TableCell>
                <TableCell sx={{ width: 100 }}>{t('tiposVinculo.columnas.estado')}</TableCell>
                <TableCell sx={{ width: 48 }}>
                  <Box component="span" sx={visualmenteOculto}>
                    {t('comun.acciones')}
                  </Box>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={COLUMNAS} filas={4} />}
              {consulta.data && tipos.length === 0 && (
                <TableRow>
                  <TableCell colSpan={COLUMNAS} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={Link2} titulo={t('tiposVinculo.vacio')} texto={t('tiposVinculo.vacioAyuda')} />
                  </TableCell>
                </TableRow>
              )}
              {tipos.map((tipo) => (
                <TableRow key={tipo.id} sx={{ opacity: tipo.activo ? 1 : 0.65 }}>
                  <TableCell>
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'baseline', minWidth: 0 }}>
                      <Typography component="span" noWrap sx={{ fontWeight: 500, fontSize: 14 }}>
                        {tipo.nombre}
                      </Typography>
                      <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
                        {tipo.codigo}
                      </Typography>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    {tipo.atributosPermitidos.length === 0 ? (
                      <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                        {t('tiposVinculo.sinAtributos')}
                      </Typography>
                    ) : (
                      <Typography component="span" variant="monoTabla" sx={{ display: 'block', overflowWrap: 'anywhere' }}>
                        {tipo.atributosPermitidos.map((a) => a.clave).join(', ')}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{exige(tipo)}</Typography>
                  </TableCell>
                  <TableCell>
                    <EstadoActivo activo={tipo.activo} texto={tipo.activo ? t('comun.activo') : t('comun.inactivo')} />
                  </TableCell>
                  <TableCell align="right">
                    {puedeEscribir && (
                      <MenuFila
                        etiqueta={t('comun.accionesDe', { nombre: tipo.nombre })}
                        acciones={[
                          { etiqueta: t('comun.editar'), icono: Pencil, onClick: () => abrir(tipo) },
                          tipo.activo
                            ? { etiqueta: t('tiposVinculo.desactivar'), icono: Ban, destructiva: true, onClick: () => setPorDesactivar(tipo) }
                            : { etiqueta: t('tiposVinculo.activar'), icono: CircleCheck, onClick: () => activar(tipo, true) },
                        ]}
                      />
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {edicion && (
        <DrawerTipoVinculo
          key={edicion.tipo === 'editar' ? edicion.tipoVinculo.id : edicion.clave}
          tipo={edicion.tipo === 'editar' ? edicion.tipoVinculo : null}
          claveIdempotencia={edicion.tipo === 'crear' ? edicion.clave : ''}
          onCerrar={() => setEdicion(null)}
        />
      )}
      {porDesactivar && (
        <DialogoConfirmacion
          titulo={t('tiposVinculo.desactivarTitulo', { nombre: porDesactivar.nombre })}
          subtitulo={porDesactivar.codigo}
          mensaje={t('tiposVinculo.confirmarDesactivar')}
          confirmar={t('tiposVinculo.desactivar')}
          icono={<Ban size={16} aria-hidden />}
          ocupado={cambiarEstado.isPending}
          onConfirmar={() => activar(porDesactivar, false)}
          onCerrar={() => setPorDesactivar(null)}
        />
      )}
    </>
  );
}
