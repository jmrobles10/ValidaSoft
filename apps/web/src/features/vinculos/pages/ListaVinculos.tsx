import { Button, Chip, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material';
import { EstadoVinculo, SCOPES, type VinculoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha, TablaConCursor, usePaginacionCursor, type ColumnaTabla } from '@validasoft/ui';
import { Search } from 'lucide-react';
import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { useSesion } from '../../../sesion/sesionStore';
import { useAvisos } from '../../../shared/useAvisos';
import { EncabezadoPagina, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { buscarPorReferencia, useTiposVinculo, useVinculos } from '../api';
import { NombreTercero } from '../components/NombreTercero';

const TAMANO_PAGINA = 20;
const ESTADOS = Object.values(EstadoVinculo);

/** Bandeja de vínculos de la empresa activa; los filtros viven en la URL (docs/06 §2.2, `Bandeja`). */
export function ListaVinculos() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const sistemaSesion = useSesion((s) => s.sesion?.sistema ?? '');
  const [filtros, setFiltros] = useSearchParams();
  const estado = filtros.get('estado') ?? '';
  const tipoVinculo = filtros.get('tipo') ?? '';
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;

  useEffect(() => {
    reiniciar();
  }, [estado, tipoVinculo, empresa.id, reiniciar]);

  const consulta = useVinculos(empresa.id, {
    estado: estado || undefined,
    tipoVinculo: tipoVinculo || undefined,
    limite: TAMANO_PAGINA,
    cursor: paginacion.cursor ?? undefined,
  });
  const tipos = useTiposVinculo(empresa.id, permisos.puede(SCOPES.configuracionLeer));

  const columnas = useMemo<ColumnaTabla<VinculoDto>[]>(
    () => [
      { id: 'tercero', encabezado: t('vinculos.columnas.tercero'), celda: (v) => <NombreTercero terceroId={v.terceroId} /> },
      { id: 'tipo', encabezado: t('vinculos.columnas.tipo'), celda: (v) => v.tipoVinculo.nombre },
      {
        id: 'estado',
        encabezado: t('vinculos.columnas.estado'),
        celda: (v) => <ChipEstado estado={v.estado} fecha={v.estado === 'Bloqueado' ? v.bloqueo?.desde : v.actualizadoEn} />,
      },
      {
        id: 'referencias',
        encabezado: t('vinculos.columnas.referencias'),
        celda: (v) => (
          <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
            {v.referencias.map((r) => (
              <Chip key={`${r.sistema}|${r.id}`} size="small" variant="outlined" label={`${r.sistema} · ${r.id}`} sx={{ fontFamily: 'monospace' }} />
            ))}
          </Stack>
        ),
      },
      {
        id: 'creado',
        encabezado: t('vinculos.columnas.creado'),
        alinear: 'right',
        celda: (v) => <Typography variant="mono">{formatearFecha(v.creadoEn)}</Typography>,
      },
    ],
    [t],
  );

  if (!permisos.puede(SCOPES.vinculosLeer)) return <SinPermiso seccion={t('nav.vinculos')} />;

  const cambiarFiltro = (clave: 'estado' | 'tipo', valor: string) => {
    setFiltros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );
  };

  return (
    <>
      <EncabezadoPagina titulo={t('vinculos.titulo')} subtitulo={t('vinculos.subtitulo', { empresa: empresa.nombre })} />

      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }} alignItems={{ md: 'flex-end' }}>
        <TextField
          select
          size="small"
          label={t('vinculos.filtros.estado')}
          value={estado}
          onChange={(e) => cambiarFiltro('estado', e.target.value)}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="">{t('comun.todos')}</MenuItem>
          {ESTADOS.map((e) => (
            <MenuItem key={e} value={e}>
              <ChipEstado estado={e} />
            </MenuItem>
          ))}
        </TextField>
        {tipos.data && (
          <TextField
            select
            size="small"
            label={t('vinculos.filtros.tipo')}
            value={tipoVinculo}
            onChange={(e) => cambiarFiltro('tipo', e.target.value)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">{t('comun.todos')}</MenuItem>
            {tipos.data.map((tipo) => (
              <MenuItem key={tipo.id} value={tipo.codigo}>
                {tipo.nombre}
              </MenuItem>
            ))}
          </TextField>
        )}
        {(estado || tipoVinculo) && (
          <Button size="small" onClick={() => setFiltros(new URLSearchParams(), { replace: true })}>
            {t('vinculos.filtros.limpiar')}
          </Button>
        )}
        <BuscarPorReferencia empresaId={empresa.id} sistemaPorDefecto={sistemaSesion} onEncontrado={(v) => void navegar(v.id)} />
      </Stack>

      <TablaConCursor
        etiqueta={t('vinculos.titulo')}
        columnas={columnas}
        filas={consulta.data?.datos}
        claveFila={(v) => v.id}
        cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
        error={consulta.isError ? `${t('vinculos.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={consulta.data?.siguienteCursor}
        onFila={(v) => void navegar(v.id)}
        etiquetaFila={(v) => t('vinculos.abrir', { tercero: v.correoTercero })}
        vacio={t('vinculos.vacio')}
      />
    </>
  );
}

function BuscarPorReferencia({
  empresaId,
  sistemaPorDefecto,
  onEncontrado,
}: {
  empresaId: string;
  sistemaPorDefecto: string;
  onEncontrado: (v: VinculoDto) => void;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [sistema, setSistema] = useState(sistemaPorDefecto);
  const [id, setId] = useState('');
  const [buscando, setBuscando] = useState(false);

  const buscar = async (evento: FormEvent) => {
    evento.preventDefault();
    if (!sistema.trim() || !id.trim()) return;
    setBuscando(true);
    try {
      onEncontrado(await buscarPorReferencia(empresaId, sistema.trim(), id.trim()));
    } catch (error) {
      mostrar(mensajeDeError(error, t), 'warning');
    } finally {
      setBuscando(false);
    }
  };

  return (
    <Paper
      component="form"
      variant="outlined"
      onSubmit={(e) => void buscar(e)}
      aria-label={t('vinculos.referencia.titulo')}
      sx={{ display: 'flex', gap: 1, p: 1, ml: { md: 'auto' }, alignItems: 'center' }}
    >
      <TextField size="small" label={t('vinculos.referencia.sistema')} value={sistema} onChange={(e) => setSistema(e.target.value)} sx={{ width: 110 }} />
      <TextField size="small" label={t('vinculos.referencia.id')} value={id} onChange={(e) => setId(e.target.value)} sx={{ width: 150 }} />
      <Button type="submit" variant="outlined" disabled={buscando} startIcon={<Search size={16} />}>
        {t('vinculos.referencia.buscar')}
      </Button>
    </Paper>
  );
}
