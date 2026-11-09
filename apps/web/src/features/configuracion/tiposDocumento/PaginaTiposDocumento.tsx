import { Box, Button, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type TipoDocumentoTenantDto } from '@validasoft/api-client';
import { EstadoVacio, FilasEsqueleto, MarcoTabla } from '@validasoft/ui';
import { Ban, CircleCheck, FileText, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useTiposDocumento } from '../../flujos/api';
import { DrawerTipoDocumento } from './DrawerTipoDocumento';
import { resumenTipo, resumenVigencia } from './resumen';

type Edicion = { tipo: 'crear'; clave: string } | { tipo: 'editar'; tipoDocumento: TipoDocumentoTenantDto };

/** Tipos de documento del grupo: qué archivo o datos recibe cada uno, su vigencia y su plantilla (TD-1…TD-9). */
export function PaginaTiposDocumento() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = useTiposDocumento(puedeLeer);
  const [edicion, setEdicion] = useState<Edicion | null>(null);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.tipos-documento')} />;

  const tipos = [...(consulta.data ?? [])].sort((a, b) => Number(b.activo) - Number(a.activo) || a.nombre.localeCompare(b.nombre, 'es'));
  const abrir = (tipoDocumento: TipoDocumentoTenantDto) => setEdicion({ tipo: 'editar', tipoDocumento });

  return (
    <>
      <EncabezadoPagina
        titulo={t('tiposDocumento.titulo')}
        subtitulo={t('tiposDocumento.ayuda')}
        acciones={
          puedeEscribir ? (
            <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setEdicion({ tipo: 'crear', clave: crypto.randomUUID() })}>
              {t('tiposDocumento.nuevo')}
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
          <Table size="small" aria-label={t('tiposDocumento.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('tiposDocumento.columnas.tipo')}</TableCell>
                <TableCell sx={{ minWidth: 200, width: '34%' }}>{t('tiposDocumento.columnas.recibe')}</TableCell>
                <TableCell sx={{ minWidth: 160, width: 200 }}>{t('tiposDocumento.columnas.vigencia')}</TableCell>
                <TableCell sx={{ width: 110 }}>{t('tiposDocumento.columnas.estado')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={4} filas={4} />}
              {consulta.data && tipos.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={FileText} titulo={t('tiposDocumento.vacio')} texto={t('tiposDocumento.vacioAyuda')} />
                  </TableCell>
                </TableRow>
              )}
              {tipos.map((tipo) => (
                <TableRow
                  key={tipo.id}
                  hover={puedeEscribir}
                  tabIndex={puedeEscribir ? 0 : undefined}
                  aria-label={puedeEscribir ? t('tiposDocumento.editarNombre', { nombre: tipo.nombre }) : undefined}
                  onClick={puedeEscribir ? () => abrir(tipo) : undefined}
                  onKeyDown={(e) => {
                    if (puedeEscribir && (e.key === 'Enter' || e.key === ' ')) {
                      e.preventDefault();
                      abrir(tipo);
                    }
                  }}
                  sx={{ cursor: puedeEscribir ? 'pointer' : 'default', opacity: tipo.activo ? 1 : 0.65 }}
                >
                  <TableCell>
                    <Typography component="span" sx={{ display: 'block', fontWeight: 500, fontSize: 14 }}>
                      {tipo.nombre}
                    </Typography>
                    <Stack direction="row" useFlexGap sx={{ columnGap: 0.75, flexWrap: 'wrap', color: 'text.secondary', fontSize: 12, '& > *': { whiteSpace: 'nowrap' } }}>
                      <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
                        {tipo.codigo}
                      </Typography>
                      <span>· {t(`tiposDocumento.ambitos.${tipo.ambito === 'Global' ? 'Global' : 'PorVinculo'}`)}</span>
                      {tipo.tipoCanonico && <span>· {t('tiposDocumento.dePlataforma')}</span>}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{resumenTipo(tipo, t)}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{resumenVigencia(tipo, t)}</Typography>
                  </TableCell>
                  <TableCell>
                    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 13, '& svg': { color: tipo.activo ? 'estado.exito.texto' : 'text.secondary' } }}>
                      {tipo.activo ? <CircleCheck size={14} strokeWidth={1.75} aria-hidden /> : <Ban size={14} strokeWidth={1.75} aria-hidden />}
                      {tipo.activo ? t('comun.activo') : t('tiposDocumento.inactivo')}
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>
      {edicion && (
        <DrawerTipoDocumento
          tipoDocumento={edicion.tipo === 'editar' ? edicion.tipoDocumento : null}
          claveIdempotencia={edicion.tipo === 'crear' ? edicion.clave : ''}
          onCerrar={() => setEdicion(null)}
        />
      )}
    </>
  );
}
