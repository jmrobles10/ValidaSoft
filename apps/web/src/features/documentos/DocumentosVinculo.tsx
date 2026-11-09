import { Button, Divider, Skeleton, Stack, Typography } from '@mui/material';
import { SCOPES, type VinculoDto } from '@validasoft/api-client';
import { ChipEstado, EstadoVacio, formatearFecha } from '@validasoft/ui';
import { ClipboardList, Eye, FileText, FolderOpen } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePermisos } from '../../shared/usePermisos';
import { hashCorto, useDocumentosVinculo, versionVisible } from './api';
import { datosDe, esVersionSinArchivo } from './formatoDatos';
import { ChipVigencia } from './VigenciaDocumento';
import { VisorDocumento, type DocumentoEnVisor } from './VisorDocumento';

/**
 * Documentos que el tercero compartió con este vínculo (`GET /vinculos/{id}/documentos`): tipo, versión, fecha y hash.
 * «Ver o descargar» pide la URL prefirmada (auditada) y abre PDF e imágenes en el visor. Solo con `documentos:leer`;
 * la acción, con `documentos:descargar`.
 */
export function DocumentosVinculo({ vinculo }: { vinculo: VinculoDto }) {
  const { t } = useTranslation();
  const permisos = usePermisos(vinculo.empresaId);
  const puedeLeer = permisos.puede(SCOPES.documentosLeer);
  const puedeDescargar = permisos.puede(SCOPES.documentosDescargar);
  const consulta = useDocumentosVinculo(vinculo.empresaId, vinculo.id, puedeLeer);
  const [abierto, setAbierto] = useState<DocumentoEnVisor | null>(null);

  if (!puedeLeer) return null;

  return (
    <Tarjeta titulo={t('documentos.titulo')}>
      {consulta.isPending ? (
        <Stack spacing={1}>
          <Skeleton variant="rounded" height={44} />
          <Skeleton variant="rounded" height={44} />
        </Stack>
      ) : consulta.isError ? (
        <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
      ) : consulta.data.length === 0 ? (
        <EstadoVacio compacto icono={FolderOpen} titulo={t('documentos.vacio')} texto={vinculo.invitacionAceptadaEn ? t('documentos.vacioAceptada') : t('documentos.vacioSinAceptar')} />
      ) : (
        <Stack component="ul" divider={<Divider component="li" aria-hidden />} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {consulta.data.map((documento) => {
            const version = versionVisible(documento);
            // Una versión de solo formulario no se descarga (409 `version-sin-archivo`): se ven sus datos.
            const sinArchivo = version ? esVersionSinArchivo(version) : false;
            return (
              <Stack component="li" key={documento.documentoId} direction="row" spacing={1.25} sx={{ alignItems: 'center', py: 0.5, minHeight: 44 }}>
                <FileText size={18} strokeWidth={1.75} aria-hidden style={{ flex: 'none', opacity: 0.7 }} />
                <Stack sx={{ minWidth: 0, flex: 1 }}>
                  <Typography sx={{ fontWeight: 500 }} noWrap>
                    {documento.tipo.nombre}
                  </Typography>
                  {version && (
                    <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
                      {t('documentos.version', { n: String(version.n), fecha: formatearFecha(version.cargadaEn) })}
                      {' · '}
                      <Typography component="span" variant="mono" title={version.sha256} sx={{ fontSize: 12 }}>
                        {t('documentos.hash', { hash: hashCorto(version.sha256) })}
                      </Typography>
                    </Typography>
                  )}
                </Stack>
                {version?.vigencia && <ChipVigencia vigencia={version.vigencia} />}
                {version && version.n !== documento.versionActual && <ChipEstado estado="Superada" />}
                {version && (sinArchivo || puedeDescargar) && (
                  <Button
                    variant="text"
                    size="small"
                    startIcon={sinArchivo ? <ClipboardList size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
                    onClick={() =>
                      setAbierto({
                        documentoId: documento.documentoId,
                        tipo: documento.tipo.nombre,
                        n: version.n,
                        cargadaEn: version.cargadaEn,
                        datosDeclarados: datosDe(version),
                        vigencia: version.vigencia ?? null,
                        sinArchivo,
                        hechos: version.hechos,
                        tipoEsperado: { codigo: documento.tipo.codigo || null, nombre: documento.tipo.nombre },
                      })
                    }
                    aria-label={
                      sinArchivo
                        ? t('expedientes.datosDeclarados.verDatosAccesible', { documento: documento.tipo.nombre, n: String(version.n) })
                        : t('documentos.verAccesible', { tipo: documento.tipo.nombre, n: String(version.n) })
                    }
                  >
                    {sinArchivo ? t('expedientes.datosDeclarados.verDatos') : t('documentos.ver')}
                  </Button>
                )}
              </Stack>
            );
          })}
        </Stack>
      )}
      {abierto && <VisorDocumento empresaId={vinculo.empresaId} vinculoId={vinculo.id} documento={abierto} onCerrar={() => setAbierto(null)} />}
    </Tarjeta>
  );
}
