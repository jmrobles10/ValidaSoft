import { Button, Chip, IconButton, Skeleton, Stack, Table, TableBody, TableCell, TableHead, TableRow, Tooltip, Typography } from '@mui/material';
import { SCOPES, type MotivoTipificadoDto } from '@validasoft/api-client';
import { MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Lock, Pencil, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useMotivos } from '../../expedientes/api';
import { DrawerMotivo } from './DrawerMotivo';

type Edicion = { tipo: 'crear' } | { tipo: 'editar'; motivo: MotivoTipificadoDto };

/** Catálogo de motivos tipificados: los 8 de la plataforma (fijos) y los propios del grupo (docs/03 §13, FL-10). */
export function PaginaMotivos() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = useMotivos(empresa.id, puedeLeer);
  const [edicion, setEdicion] = useState<Edicion | null>(null);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.motivos')} />;

  // Primero los propios (los que el grupo administra), luego los de la plataforma.
  const motivos = [...(consulta.data ?? [])].sort((a, b) => Number(a.base) - Number(b.base) || a.nombre.localeCompare(b.nombre, 'es'));

  return (
    <>
      <EncabezadoPagina
        titulo={t('motivos.titulo')}
        subtitulo={t('motivos.ayuda')}
        acciones={
          puedeEscribir ? (
            <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setEdicion({ tipo: 'crear' })}>
              {t('motivos.nuevo')}
            </Button>
          ) : undefined
        }
      />
      {consulta.isError && <ErrorRecuperable mensaje={`${t('motivos.errorLista')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      <MarcoTabla>
        <Table size="small" aria-label={t('motivos.titulo')}>
          <TableHead>
            <TableRow>
              <TableCell>{t('motivos.columnas.motivo')}</TableCell>
              <TableCell sx={{ width: 120 }}>{t('motivos.columnas.categoria')}</TableCell>
              <TableCell sx={{ width: 160 }}>{t('motivos.columnas.aplicaA')}</TableCell>
              <TableCell>{t('motivos.columnas.texto')}</TableCell>
              <TableCell sx={{ width: 100 }}>{t('motivos.columnas.estado')}</TableCell>
              <TableCell sx={{ width: 48 }}>
                <Typography component="span" sx={visualmenteOculto}>
                  {t('comun.acciones')}
                </Typography>
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {consulta.isPending &&
              [0, 1, 2, 3].map((i) => (
                <TableRow key={i}>
                  {[0, 1, 2, 3, 4, 5].map((j) => (
                    <TableCell key={j}>
                      <Skeleton variant="rounded" height={12} />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            {motivos.map((m) => (
              <TableRow key={m.id}>
                <TableCell>
                  <Stack spacing={0.25}>
                    <Typography component="span" sx={{ fontWeight: 500 }}>
                      {m.nombre}
                    </Typography>
                    <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
                      <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
                        {m.codigo}
                      </Typography>
                      {m.base && <Chip size="small" variant="outlined" icon={<Lock size={12} aria-hidden />} label={t('motivos.base')} />}
                    </Stack>
                  </Stack>
                </TableCell>
                <TableCell>{t(`motivos.categorias.${m.categoria}`)}</TableCell>
                <TableCell>{t(`motivos.aplica.${m.aplicaA}`)}</TableCell>
                <TableCell>
                  <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
                    {m.textoAlTercero}
                  </Typography>
                </TableCell>
                <TableCell>{m.activo ? t('comun.activo') : t('comun.inactivo')}</TableCell>
                <TableCell align="right">
                  {puedeEscribir && !m.base ? (
                    <Tooltip title={t('motivos.editar')}>
                      <IconButton size="small" aria-label={t('motivos.editarTitulo', { nombre: m.nombre })} onClick={() => setEdicion({ tipo: 'editar', motivo: m })}>
                        <Pencil size={16} strokeWidth={1.75} />
                      </IconButton>
                    </Tooltip>
                  ) : null}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </MarcoTabla>
      {edicion && <DrawerMotivo empresaId={empresa.id} motivo={edicion.tipo === 'editar' ? edicion.motivo : null} onCerrar={() => setEdicion(null)} />}
    </>
  );
}
