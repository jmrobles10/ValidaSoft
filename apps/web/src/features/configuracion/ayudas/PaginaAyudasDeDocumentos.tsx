import { Button, Chip, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type AyudaDeDocumentoDto } from '@validasoft/api-client';
import { BarraFiltros, EsqueletoTabla, EstadoVacio, FiltroMenu, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { BookOpenText } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useAyudasDeDocumentos } from './api';
import { DrawerAyuda } from './DrawerAyuda';
import { ORIGENES_AYUDA, origenDeAyuda, resumirAyuda, type OrigenAyuda } from './modelo';

/** Chip del origen de la ayuda: «Propia» (la escribió la empresa), «Del grupo» o «Sin ayuda». */
export function ChipOrigenAyuda({ origen }: { origen: OrigenAyuda }) {
  const { t } = useTranslation();
  return (
    <Chip
      size="small"
      variant={origen === 'propia' ? 'filled' : 'outlined'}
      color={origen === 'propia' ? 'primary' : 'default'}
      label={t(`ayudas.origen.${origen}`)}
      data-origen={origen}
      sx={origen === 'ninguna' ? { color: 'text.secondary' } : undefined}
    />
  );
}

/**
 * Ayuda por documento y empresa (OP-15, OP-16): lo que el tercero lee al entregar cada tipo de documento. El grupo define una
 * ayuda para todas sus empresas y cada empresa puede escribir la suya; sin ayuda propia, el tercero ve la del grupo.
 */
export function PaginaAyudasDeDocumentos() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = useAyudasDeDocumentos(empresa.id, puedeLeer);
  const [origen, setOrigen] = useState<OrigenAyuda | ''>('');
  const [elegida, setElegida] = useState<string | null>(null);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.ayuda-de-documentos')} />;

  const todas = consulta.data ?? [];
  const filas = origen ? todas.filter((a) => origenDeAyuda(a) === origen) : todas;
  const abierta: AyudaDeDocumentoDto | undefined = todas.find((a) => a.tipoDocumentoId === elegida);

  return (
    <>
      <EncabezadoPagina titulo={t('ayudas.titulo')} subtitulo={t('ayudas.subtitulo', { empresa: empresa.nombre })} />
      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('ayudas.filtroOrigen')}
          textoTodos={t('comun.todas')}
          valor={origen}
          onCambiar={(valor) => setOrigen(valor as OrigenAyuda | '')}
          opciones={ORIGENES_AYUDA.map((o) => ({ valor: o, etiqueta: t(`ayudas.origen.${o}`) }))}
        />
        {origen && (
          <Button variant="text" onClick={() => setOrigen('')}>
            {t('bandeja.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>

      {consulta.isError && <ErrorRecuperable mensaje={`${t('ayudas.errorLista')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && <EsqueletoTabla etiqueta={t('ayudas.titulo')} columnas={4} filas={5} />}
      {consulta.data && (
        <MarcoTabla>
          {filas.length === 0 ? (
            <EstadoVacio compacto icono={BookOpenText} titulo={origen ? t('ayudas.vacioFiltro') : t('ayudas.vacio')} texto={origen ? undefined : t('ayudas.vacioAyuda')} />
          ) : (
            <Table size="small" aria-label={t('ayudas.titulo')}>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ width: '28%' }}>{t('ayudas.columnas.documento')}</TableCell>
                  <TableCell>{t('ayudas.columnas.ayuda')}</TableCell>
                  <TableCell sx={{ width: 120 }}>{t('ayudas.columnas.origen')}</TableCell>
                  <TableCell sx={{ width: 90 }}>
                    <Typography component="span" sx={visualmenteOculto}>
                      {t('comun.acciones')}
                    </Typography>
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filas.map((a) => {
                  const origenFila = origenDeAyuda(a);
                  return (
                    <TableRow key={a.tipoDocumentoId}>
                      <TableCell>
                        <Typography component="span" sx={{ display: 'block', fontWeight: 500 }}>
                          {a.nombre}
                        </Typography>
                        <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
                          {a.codigo}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        {origenFila === 'ninguna' ? (
                          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                            {t('ayudas.sinAyuda')}
                          </Typography>
                        ) : (
                          <Typography variant="body2" sx={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>
                            {resumirAyuda(a.ayudaEfectiva)}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>
                        <ChipOrigenAyuda origen={origenFila} />
                      </TableCell>
                      <TableCell align="right">
                        <Button variant="text" aria-label={t(puedeEscribir ? 'ayudas.editarDe' : 'ayudas.verDe', { nombre: a.nombre })} onClick={() => setElegida(a.tipoDocumentoId)}>
                          {puedeEscribir ? t('comun.editar') : t('ayudas.ver')}
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </MarcoTabla>
      )}

      {abierta && <DrawerAyuda empresaId={empresa.id} empresaNombre={empresa.nombre} ayuda={abierta} puedeEscribir={puedeEscribir} onCerrar={() => setElegida(null)} />}
    </>
  );
}
