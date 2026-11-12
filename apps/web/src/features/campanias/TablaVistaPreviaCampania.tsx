import { Box, Chip, Typography } from '@mui/material';
import type { FilaVistaPreviaCampaniaDto } from '@validasoft/api-client';
import { BarraFiltros, ChipEstado, EstadoVacio, FiltroMenu, TablaConCursor, type ColumnaTabla, type EstadoPaginacionCursor } from '@validasoft/ui';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ACCIONES_DE_VISTA_PREVIA, esEstadoDeCampania, filasConAccion, formatearDia, identificacionLegible, paginar, type AccionDeVistaPrevia } from './modelo';

const TONO_ACCION: Record<AccionDeVistaPrevia, 'success' | 'info' | 'default'> = { Abrir: 'success', Recordar: 'info', Omitir: 'default' };

const esAccion = (valor: string): valor is AccionDeVistaPrevia => ACCIONES_DE_VISTA_PREVIA.some((a) => a === valor);

/**
 * Los vínculos de la vista previa, de a 10 por página y filtrables por lo que pasará con cada uno. Las omitidas dicen por qué.
 * La API trae hasta 200: la paginación es de aquí, pero se ve igual que la del resto de las tablas.
 */
export function TablaVistaPreviaCampania({ filas }: { filas: readonly FilaVistaPreviaCampaniaDto[] }) {
  const { t } = useTranslation();
  const [accion, setAccion] = useState('');
  const [pagina, setPagina] = useState(0);
  const visibles = useMemo(() => filasConAccion(filas, accion), [filas, accion]);
  const { filas: enPagina, haySiguiente } = paginar(visibles, pagina);

  const paginacion: EstadoPaginacionCursor = {
    cursor: String(pagina),
    pagina: pagina + 1,
    hayAnterior: pagina > 0,
    irASiguiente: () => setPagina((p) => p + 1),
    irAAnterior: () => setPagina((p) => Math.max(0, p - 1)),
    reiniciar: () => setPagina(0),
  };

  const columnas = useMemo<ColumnaTabla<FilaVistaPreviaCampaniaDto>[]>(
    () => [
      {
        id: 'tercero',
        encabezado: t('campanias.nuevaCampania.columnas.tercero'),
        celda: (f) => (
          <>
            <Typography component="span" variant="body2" sx={{ display: 'block' }}>
              {f.tercero.nombre}
            </Typography>
            <Typography component="span" variant="caption" sx={{ color: 'text.secondary' }}>
              {identificacionLegible(f.tercero.identificacion)}
            </Typography>
          </>
        ),
      },
      { id: 'tipo', encabezado: t('campanias.nuevaCampania.columnas.tipo'), ancho: 150, celda: (f) => f.tipoVinculo.nombre },
      {
        id: 'estado',
        encabezado: t('campanias.nuevaCampania.columnas.estado'),
        ancho: 130,
        celda: (f) => (esEstadoDeCampania(f.estadoVinculo) ? <ChipEstado estado={f.estadoVinculo} /> : f.estadoVinculo),
      },
      { id: 'vence', encabezado: t('campanias.nuevaCampania.columnas.vence'), ancho: 110, celda: (f) => <Typography variant="monoTabla">{formatearDia(f.venceEn)}</Typography> },
      {
        id: 'accion',
        encabezado: t('campanias.nuevaCampania.columnas.accion'),
        celda: (f) => (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <Chip
              size="small"
              variant="outlined"
              color={esAccion(f.accion) ? TONO_ACCION[f.accion] : 'default'}
              label={esAccion(f.accion) ? t(`campanias.nuevaCampania.acciones.${f.accion}`) : f.accion}
            />
            {f.motivo && (
              <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                {f.motivo}
              </Typography>
            )}
          </Box>
        ),
      },
      {
        id: 'correo',
        encabezado: t('campanias.nuevaCampania.columnas.correo'),
        ancho: 100,
        celda: (f) =>
          f.accion === 'Omitir' ? (
            '—'
          ) : (
            <Typography component="span" variant="body2" sx={{ color: f.conCorreo ? 'text.primary' : 'estado.advertencia.texto' }}>
              {f.conCorreo ? t('campanias.nuevaCampania.conCorreo') : t('campanias.nuevaCampania.sinCorreo')}
            </Typography>
          ),
      },
    ],
    [t],
  );

  return (
    <>
      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('campanias.nuevaCampania.filtroAccion')}
          textoTodos={t('comun.todos')}
          valor={accion}
          onCambiar={(valor) => {
            setAccion(valor);
            setPagina(0);
          }}
          opciones={ACCIONES_DE_VISTA_PREVIA.map((a) => ({ valor: a, etiqueta: t(`campanias.nuevaCampania.acciones.${a}`) }))}
        />
      </BarraFiltros>
      <TablaConCursor
        etiqueta={t('campanias.nuevaCampania.pasoRevision')}
        columnas={columnas}
        filas={enPagina}
        claveFila={(f) => f.vinculoId}
        filasAltas
        cargando={false}
        paginacion={paginacion}
        siguienteCursor={haySiguiente ? String(pagina + 1) : null}
        vacio={<EstadoVacio compacto titulo={t('campanias.nuevaCampania.sinFilas')} />}
      />
    </>
  );
}
