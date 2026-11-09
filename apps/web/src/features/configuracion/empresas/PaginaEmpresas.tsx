import { Alert, Box, Button, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type EmpresaDto } from '@validasoft/api-client';
import { Dato, EstadoVacio, FilasEsqueleto, MarcoTabla, RejillaDatos, visualmenteOculto } from '@validasoft/ui';
import { Ban, Building2, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { MenuFila } from '../../../shared/MenuFila';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva, useEmpresas } from '../../empresas/useEmpresas';
import { EstadoActivo } from '../EstadoActivo';
import { useDesactivarEmpresa } from './api';
import { DialogoEmpresa } from './DialogoEmpresa';
import { ejemploFolio, identificacionVisible } from './modelo';
import { TextosLegalesEmpresa } from './TextosLegalesEmpresa';

const COLUMNAS = 5;

/** Razones sociales del grupo (ADR-0008): alta, desactivación y sus textos legales versionados. */
export function PaginaEmpresas() {
  const { t } = useTranslation();
  const { empresa: activa } = useEmpresaActiva();
  const permisos = usePermisos(activa.id);
  const puede = permisos.puede(SCOPES.empresasAdministrar);
  const consulta = useEmpresas();
  const desactivar = useDesactivarEmpresa();
  const mostrar = useAvisos((s) => s.mostrar);
  const [elegidaId, setElegidaId] = useState<string | null>(null);
  const [creando, setCreando] = useState<string | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<EmpresaDto | null>(null);

  if (!puede) return <SinPermiso seccion={t('nav.empresas')} />;

  const empresas = [...(consulta.data ?? [])].sort((a, b) => Number(b.activa) - Number(a.activa) || a.nombre.localeCompare(b.nombre, 'es'));
  const elegida = empresas.find((e) => e.id === elegidaId) ?? empresas[0] ?? null;
  const estado = (e: EmpresaDto) => <EstadoActivo activo={e.activa} texto={e.activa ? t('empresasGrupo.activa') : t('empresasGrupo.inactiva')} />;

  const confirmarDesactivar = (e: EmpresaDto) =>
    desactivar.mutate(e.id, {
      onSuccess: () => mostrar(t('empresasGrupo.desactivada', { nombre: e.nombre })),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setPorDesactivar(null),
    });

  return (
    <>
      <EncabezadoPagina
        titulo={t('empresasGrupo.titulo')}
        subtitulo={t('empresasGrupo.ayuda')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setCreando(crypto.randomUUID())}>
            {t('empresasGrupo.nueva')}
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
          <Table size="small" aria-label={t('empresasGrupo.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('empresasGrupo.columnas.razonSocial')}</TableCell>
                <TableCell sx={{ width: 200 }}>{t('empresasGrupo.columnas.identificacion')}</TableCell>
                <TableCell sx={{ width: 130 }}>{t('empresasGrupo.columnas.prefijo')}</TableCell>
                <TableCell sx={{ width: 110 }}>{t('empresasGrupo.columnas.estado')}</TableCell>
                <TableCell sx={{ width: 48 }}>
                  <Box component="span" sx={visualmenteOculto}>
                    {t('comun.acciones')}
                  </Box>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={COLUMNAS} filas={3} />}
              {consulta.data && empresas.length === 0 && (
                <TableRow>
                  <TableCell colSpan={COLUMNAS} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={Building2} titulo={t('empresasGrupo.vacio')} texto={t('empresasGrupo.vacioAyuda')} />
                  </TableCell>
                </TableRow>
              )}
              {empresas.map((e) => (
                <TableRow
                  key={e.id}
                  selected={e.id === elegida?.id}
                  tabIndex={0}
                  aria-label={t('empresasGrupo.ver', { nombre: e.nombre })}
                  onClick={() => setElegidaId(e.id)}
                  onKeyDown={(evento) => {
                    if (evento.target === evento.currentTarget && (evento.key === 'Enter' || evento.key === ' ')) {
                      evento.preventDefault();
                      setElegidaId(e.id);
                    }
                  }}
                  sx={{ cursor: 'pointer', opacity: e.activa ? 1 : 0.65, '&:focus-visible': { outlineOffset: -2 } }}
                >
                  <TableCell>
                    <Typography component="span" noWrap sx={{ display: 'block', fontWeight: 500, fontSize: 14 }}>
                      {e.nombre}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{identificacionVisible(e.identificacion)}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{e.prefijoFolio}</Typography>
                  </TableCell>
                  <TableCell>{estado(e)}</TableCell>
                  <TableCell align="right">
                    <MenuFila
                      etiqueta={t('comun.accionesDe', { nombre: e.nombre })}
                      acciones={e.activa ? [{ etiqueta: t('empresasGrupo.desactivar'), icono: Ban, destructiva: true, onClick: () => setPorDesactivar(e) }] : []}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {elegida && (
        <Box sx={{ mt: 1.5 }}>
          <Tarjeta etiqueta={elegida.nombre} titulo={elegida.nombre}>
            <Stack spacing={2}>
              <RejillaDatos>
                <Dato etiqueta={t('empresasGrupo.columnas.identificacion')}>
                  <Typography component="span" variant="mono">
                    {identificacionVisible(elegida.identificacion)} · {elegida.identificacion.pais}
                  </Typography>
                </Dato>
                <Dato etiqueta={t('empresasGrupo.columnas.prefijo')}>
                  <Typography component="span" variant="mono">
                    {ejemploFolio(elegida.prefijoFolio, new Date().getFullYear())}
                  </Typography>
                </Dato>
                <Dato etiqueta={t('empresasGrupo.columnas.estado')}>{estado(elegida)}</Dato>
              </RejillaDatos>
              {!elegida.activa && <Alert severity="info">{t('empresasGrupo.inactivaAyuda')}</Alert>}
              <TextosLegalesEmpresa
                key={elegida.id}
                empresa={elegida}
                puedeLeer={permisos.puede(SCOPES.configuracionLeer)}
                puedeEscribir={elegida.activa && permisos.puede(SCOPES.configuracionEscribir)}
              />
            </Stack>
          </Tarjeta>
        </Box>
      )}

      {creando && (
        <DialogoEmpresa
          claveIdempotencia={creando}
          onCerrar={() => setCreando(null)}
          onCreada={(empresa) => {
            setElegidaId(empresa.id);
            setCreando(null);
          }}
        />
      )}
      {porDesactivar && (
        <DialogoConfirmacion
          titulo={t('empresasGrupo.desactivarTitulo', { nombre: porDesactivar.nombre })}
          subtitulo={identificacionVisible(porDesactivar.identificacion)}
          mensaje={porDesactivar.id === activa.id ? `${t('empresasGrupo.confirmarDesactivar')} ${t('empresasGrupo.esLaActiva')}` : t('empresasGrupo.confirmarDesactivar')}
          confirmar={t('empresasGrupo.desactivar')}
          icono={<Ban size={16} aria-hidden />}
          ocupado={desactivar.isPending}
          onConfirmar={() => confirmarDesactivar(porDesactivar)}
          onCerrar={() => setPorDesactivar(null)}
        />
      )}
    </>
  );
}
