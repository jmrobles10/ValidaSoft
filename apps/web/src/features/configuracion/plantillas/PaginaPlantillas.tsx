import { Button, Chip, Skeleton, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type PlantillaNotificacionDto } from '@validasoft/api-client';
import { MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Eye, Pencil } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { RecordatorioAutomatico } from '../../recordatorios/RecordatorioAutomatico';
import { usePlantillas } from './api';
import { DrawerPlantilla } from './DrawerPlantilla';
import { personalizada } from './modelo';

/** Plantillas de correo de la empresa activa (PL-1): los 6 correos al tercero con su estado y versión. */
export function PaginaPlantillas() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = usePlantillas(empresa.id, puedeLeer);
  const [abierta, setAbierta] = useState<PlantillaNotificacionDto | null>(null);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.plantillas')} />;

  return (
    <>
      <EncabezadoPagina titulo={t('plantillas.titulo')} subtitulo={t('plantillas.subtitulo', { empresa: empresa.nombre })} />
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
        {t('plantillas.ayuda')}
      </Typography>
      <RecordatorioAutomatico empresaId={empresa.id} puedeEscribir={puedeEscribir} />
      {consulta.isError && <ErrorRecuperable mensaje={`${t('plantillas.errorLista')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      <MarcoTabla>
        <Table size="small" aria-label={t('plantillas.titulo')}>
          <TableHead>
            <TableRow>
              <TableCell>{t('plantillas.columnas.correo')}</TableCell>
              <TableCell sx={{ width: 150 }}>{t('plantillas.columnas.estado')}</TableCell>
              <TableCell sx={{ width: 100 }}>{t('plantillas.columnas.version')}</TableCell>
              <TableCell sx={{ width: 110 }}>
                <Typography component="span" sx={visualmenteOculto}>
                  {t('comun.acciones')}
                </Typography>
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {consulta.isPending &&
              [0, 1, 2].map((i) => (
                <TableRow key={i}>
                  {[0, 1, 2, 3].map((j) => (
                    <TableCell key={j}>
                      <Skeleton variant="rounded" height={12} />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            {(consulta.data ?? []).map((p) => (
              <TableRow key={p.evento}>
                <TableCell>
                  <Stack spacing={0.25}>
                    <Typography component="span" sx={{ fontWeight: 500 }}>
                      {p.nombre}
                    </Typography>
                    <Typography component="span" sx={{ color: 'text.secondary', fontSize: 13 }}>
                      {p.descripcion}
                    </Typography>
                  </Stack>
                </TableCell>
                <TableCell>
                  {personalizada(p) ? (
                    <Chip size="small" color="primary" variant="outlined" label={t('plantillas.personalizada')} />
                  ) : (
                    <Chip size="small" variant="outlined" label={t('plantillas.predeterminada')} />
                  )}
                </TableCell>
                <TableCell>
                  <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
                    {p.version > 0 ? t('plantillas.version', { version: String(p.version) }) : t('plantillas.sinCambios')}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Button
                    size="small"
                    variant="text"
                    startIcon={puedeEscribir ? <Pencil size={14} aria-hidden /> : <Eye size={14} aria-hidden />}
                    aria-label={t(puedeEscribir ? 'plantillas.editarTitulo' : 'plantillas.verTitulo', { nombre: p.nombre })}
                    onClick={() => setAbierta(p)}
                  >
                    {t(puedeEscribir ? 'plantillas.editar' : 'plantillas.ver')}
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </MarcoTabla>
      {abierta && <DrawerPlantilla empresaId={empresa.id} plantilla={abierta} puedeEscribir={puedeEscribir} onCerrar={() => setAbierta(null)} />}
    </>
  );
}
