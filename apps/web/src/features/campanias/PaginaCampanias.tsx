import { Button, Typography } from '@mui/material';
import { SCOPES, type CampaniaDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFecha, TablaConCursor, usePaginacionCursor, type ColumnaTabla } from '@validasoft/ui';
import { Megaphone } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useTiposVinculo } from '../vinculos/api';
import { useCampanias } from './api';
import { AvanceCampania, ChipCampania } from './componentes';
import { resumenDeFiltro } from './modelo';

const TAMANO_PAGINA = 20;

/** Campañas de renovación de la empresa (OP-1…OP-6): estado, avance y resultado; se refresca sola mientras alguna avanza. */
export function PaginaCampanias() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const paginacion = usePaginacionCursor();
  const puede = permisos.puede(SCOPES.expedientesLeer);
  const puedeCrear = permisos.puede(SCOPES.expedientesAdministrar);
  const consulta = useCampanias(empresa.id, { limite: TAMANO_PAGINA, cursor: paginacion.cursor ?? undefined }, puede);
  const tipos = useTiposVinculo(empresa.id, permisos.puede(SCOPES.configuracionLeer));

  const columnas = useMemo<ColumnaTabla<CampaniaDto>[]>(
    () => [
      {
        id: 'nombre',
        encabezado: t('campanias.columnas.nombre'),
        celda: (c) => (
          <>
            <Typography component="span" sx={{ display: 'block', fontWeight: 500 }}>
              {c.nombre}
            </Typography>
            <Typography component="span" variant="caption" sx={{ color: 'text.secondary' }}>
              {resumenDeFiltro(c.filtro, tipos.data?.find((x) => x.id === c.filtro.tipoVinculoId)?.nombre ?? null, t)}
            </Typography>
          </>
        ),
      },
      { id: 'estado', encabezado: t('campanias.columnas.estado'), ancho: 140, celda: (c) => <ChipCampania estado={c.estado} /> },
      { id: 'avance', encabezado: t('campanias.columnas.avance'), ancho: 260, celda: (c) => <AvanceCampania campania={c} ancho={100} /> },
      {
        id: 'resultado',
        encabezado: t('campanias.columnas.resultado'),
        celda: (c) =>
          t('campanias.resumen', { exitosas: String(c.totales.exitosas), fallidas: String(c.totales.fallidas), omitidas: String(c.totales.omitidas) }),
      },
      {
        id: 'creada',
        encabezado: t('campanias.columnas.creada'),
        ancho: 110,
        celda: (c) => <Typography variant="monoTabla">{formatearFecha(c.creadaEn)}</Typography>,
      },
    ],
    [t, tipos.data],
  );

  if (!puede) return <SinPermiso seccion={t('campanias.titulo')} />;
  const ir = (ruta: string) => void navegar(`/e/${empresa.id}/vencimientos/campanias/${ruta}`);

  return (
    <>
      <EncabezadoPagina
        antes={<EnlaceVolver a={`/e/${empresa.id}/vencimientos`}>{t('campanias.volver')}</EnlaceVolver>}
        titulo={t('campanias.titulo')}
        subtitulo={t('campanias.subtitulo', { empresa: empresa.nombre })}
        acciones={
          puedeCrear && (
            <Button variant="contained" startIcon={<Megaphone size={16} aria-hidden />} onClick={() => ir('nueva')}>
              {t('campanias.nueva')}
            </Button>
          )
        }
      />
      <TablaConCursor
        etiqueta={t('campanias.titulo')}
        columnas={columnas}
        filas={consulta.data?.datos}
        claveFila={(c) => c.id}
        filasAltas
        cargando={consulta.isPending}
        error={consulta.isError ? `${t('campanias.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={consulta.data?.siguienteCursor}
        onFila={(c) => ir(c.id)}
        etiquetaFila={(c) => t('campanias.abrir', { nombre: c.nombre })}
        vacio={<EstadoVacio compacto titulo={t('campanias.vacio')} texto={puedeCrear ? t('campanias.vacioAyuda') : undefined} />}
      />
    </>
  );
}
