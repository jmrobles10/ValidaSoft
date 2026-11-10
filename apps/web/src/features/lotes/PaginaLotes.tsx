import { Button, Typography } from '@mui/material';
import { SCOPES, type LoteDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFecha, TablaConCursor, usePaginacionCursor, type ColumnaTabla } from '@validasoft/ui';
import { FileUp } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useLotes } from './api';
import { AvanceLote, ChipLote } from './componentes';

const TAMANO_PAGINA = 20;

/** Lotes de invitación masiva de la empresa (H8): estado, avance y resultado; se refresca solo mientras alguno avanza. */
export function PaginaLotes() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const paginacion = usePaginacionCursor();
  const puede = permisos.puede(SCOPES.vinculosLeer);
  const consulta = useLotes(empresa.id, { limite: TAMANO_PAGINA, cursor: paginacion.cursor ?? undefined }, puede);

  const columnas = useMemo<ColumnaTabla<LoteDto>[]>(
    () => [
      {
        id: 'nombre',
        encabezado: t('lotes.columnas.nombre'),
        celda: (l) => (
          <>
            <Typography component="span" sx={{ display: 'block', fontWeight: 500 }}>
              {l.nombre}
            </Typography>
            {l.archivo && l.archivo !== l.nombre && (
              <Typography component="span" variant="caption" sx={{ color: 'text.secondary' }}>
                {l.archivo}
              </Typography>
            )}
          </>
        ),
      },
      { id: 'estado', encabezado: t('lotes.columnas.estado'), ancho: 140, celda: (l) => <ChipLote estado={l.estado} /> },
      { id: 'avance', encabezado: t('lotes.columnas.avance'), ancho: 260, celda: (l) => <AvanceLote lote={l} ancho={100} /> },
      {
        id: 'resultado',
        encabezado: t('lotes.columnas.resultado'),
        celda: (l) =>
          t('lotes.resumen', {
            exitosas: String(l.totales.exitosas),
            fallidas: String(l.totales.fallidas),
            descartadas: String(l.totales.invalidas + l.totales.duplicadas),
          }),
      },
      {
        id: 'creado',
        encabezado: t('lotes.columnas.creado'),
        ancho: 110,
        celda: (l) => <Typography variant="monoTabla">{formatearFecha(l.creadoEn)}</Typography>,
      },
    ],
    [t],
  );

  if (!puede) return <SinPermiso seccion={t('lotes.titulo')} />;
  const puedeCrear = permisos.puede(SCOPES.vinculosEscribir);

  return (
    <>
      <EncabezadoPagina
        antes={<EnlaceVolver a={`/e/${empresa.id}/vinculos`}>{t('lotes.volver')}</EnlaceVolver>}
        titulo={t('lotes.titulo')}
        subtitulo={t('lotes.subtitulo', { empresa: empresa.nombre })}
        acciones={
          puedeCrear && (
            <Button variant="contained" startIcon={<FileUp size={16} aria-hidden />} onClick={() => void navegar('nuevo')}>
              {t('lotes.nuevo')}
            </Button>
          )
        }
      />
      <TablaConCursor
        etiqueta={t('lotes.titulo')}
        columnas={columnas}
        filas={consulta.data?.datos}
        claveFila={(l) => l.id}
        filasAltas
        cargando={consulta.isPending}
        error={consulta.isError ? `${t('lotes.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={consulta.data?.siguienteCursor}
        onFila={(l) => void navegar(l.id)}
        etiquetaFila={(l) => t('lotes.abrir', { nombre: l.nombre })}
        vacio={<EstadoVacio compacto titulo={t('lotes.vacio')} texto={puedeCrear ? t('lotes.vacioAyuda') : undefined} />}
      />
    </>
  );
}
