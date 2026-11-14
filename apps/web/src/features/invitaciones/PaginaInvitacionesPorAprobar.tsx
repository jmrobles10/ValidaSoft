import { Box, Button, Chip, FormControlLabel, Stack, Switch, Typography } from '@mui/material';
import { SCOPES, type InvitacionPorAprobarItemDto, type LoteDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFecha, TablaConCursor, usePaginacionCursor, type ColumnaTabla } from '@validasoft/ui';
import { CircleCheck, CircleSlash } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useSesion } from '../../sesion/sesionStore';
import { useNombresUsuarios } from '../configuracion/usuarios/api';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useLotes } from '../lotes/api';
import { procesables } from '../lotes/modelo';
import { mensajeDeErrorDeDecision, useDecidirInvitacion, useDecidirLote, useInvitacionesPorAprobar } from './api';
import { DialogoRechazo } from './DialogoRechazo';

const TAMANO_PAGINA = 20;

type Rechazo = { tipo: 'invitacion'; item: InvitacionPorAprobarItemDto } | { tipo: 'lote'; lote: LoteDto };

/** El id de la persona de un actor `Usuario:{id}` (nada si es una integración). */
function idDePersona(actor: string): string | null {
  return actor.startsWith('Usuario:') ? actor.slice('Usuario:'.length) : null;
}

/**
 * Bandeja de quienes aprueban (decisión AI): las invitaciones de la empresa retenidas a la espera de aprobación —las de vínculos y los lotes completos—, con aprobar y
 * rechazar (con motivo) en cada una. Por omisión solo las que quien consulta puede decidir: el tipo la designó y no es quien la pidió. Abrir una fila lleva al vínculo o al lote.
 */
export function PaginaInvitacionesPorAprobar() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const mostrar = useAvisos((s) => s.mostrar);
  const usuarioActual = useSesion((s) => s.sesion?.usuarioId ?? null);
  const puede = permisos.puede(SCOPES.vinculosLeer);
  const puedeDecidir = permisos.puede(SCOPES.vinculosEscribir);
  const [soloLasMias, setSoloLasMias] = useState(true);
  const [rechazo, setRechazo] = useState<Rechazo | null>(null);
  const paginaVinculos = usePaginacionCursor();
  const paginaLotes = usePaginacionCursor();
  const { reiniciar: reiniciarVinculos } = paginaVinculos;
  const { reiniciar: reiniciarLotes } = paginaLotes;
  useEffect(() => {
    reiniciarVinculos();
    reiniciarLotes();
  }, [soloLasMias, empresa.id, reiniciarVinculos, reiniciarLotes]);

  const vinculos = useInvitacionesPorAprobar(empresa.id, { soloLasMias, limite: TAMANO_PAGINA, cursor: paginaVinculos.cursor ?? undefined }, puede);
  const lotes = useLotes(empresa.id, { estado: 'PorAprobar', limite: TAMANO_PAGINA, cursor: paginaLotes.cursor ?? undefined }, puede);
  const decidirInvitacion = useDecidirInvitacion(empresa.id);
  const decidirLote = useDecidirLote(empresa.id);
  // Lo que decide quien consulta (el filtro de lotes es de esta pantalla: la API de lotes lista todos los de la empresa).
  const lotesVisibles = useMemo(() => (lotes.data?.datos ?? []).filter((l) => !soloLasMias || l.puedesDecidir === true), [lotes.data, soloLasMias]);
  const nombres = useNombresUsuarios(
    empresa.id,
    useMemo(() => lotesVisibles.map((l) => idDePersona(l.creadoPor)).filter((id): id is string => id !== null), [lotesVisibles]),
  );

  const aprobarInvitacion = (item: InvitacionPorAprobarItemDto) =>
    decidirInvitacion.mutate(
      { vinculoId: item.vinculoId, decision: 'aprobar' },
      { onSuccess: () => mostrar(t('invitaciones.porAprobar.aprobada'), 'success'), onError: (e) => mostrar(mensajeDeErrorDeDecision(e, t), 'error') },
    );
  const aprobarLote = (lote: LoteDto) =>
    decidirLote.mutate(
      { loteId: lote.id, decision: 'aprobar' },
      { onSuccess: () => mostrar(t('invitaciones.porAprobar.loteAprobado'), 'success'), onError: (e) => mostrar(mensajeDeErrorDeDecision(e, t), 'error') },
    );
  const rechazar = (motivo: string) => {
    if (!rechazo) return;
    const alTerminar = (clave: 'invitaciones.porAprobar.rechazada' | 'invitaciones.porAprobar.loteRechazado') => ({
      onSuccess: () => {
        setRechazo(null);
        mostrar(t(clave), 'success');
      },
    });
    if (rechazo.tipo === 'invitacion')
      decidirInvitacion.mutate({ vinculoId: rechazo.item.vinculoId, decision: 'rechazar', motivo }, alTerminar('invitaciones.porAprobar.rechazada'));
    else decidirLote.mutate({ loteId: rechazo.lote.id, decision: 'rechazar', motivo }, alTerminar('invitaciones.porAprobar.loteRechazado'));
  };
  const errorDelRechazo = rechazo?.tipo === 'invitacion' ? decidirInvitacion.error : decidirLote.error;
  const cerrarRechazo = () => {
    decidirInvitacion.reset();
    decidirLote.reset();
    setRechazo(null);
  };

  const columnasVinculos: ColumnaTabla<InvitacionPorAprobarItemDto>[] = [
      {
        id: 'tercero',
        encabezado: t('invitaciones.porAprobar.columnas.tercero'),
        celda: (i) => (
          <>
            <Typography component="span" sx={{ display: 'block', fontWeight: 500 }}>
              {i.terceroNombre}
            </Typography>
            <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
              {`${i.terceroIdentificacion.tipo} ${i.terceroIdentificacion.numero}`}
            </Typography>
          </>
        ),
      },
      { id: 'tipo', encabezado: t('invitaciones.porAprobar.columnas.tipo'), ancho: 130, celda: (i) => i.tipoVinculo.nombre },
      { id: 'correo', encabezado: t('invitaciones.porAprobar.columnas.correo'), celda: (i) => i.correoDestino },
      {
        id: 'pidio',
        encabezado: t('invitaciones.porAprobar.columnas.pidio'),
        ancho: 170,
        celda: (i) => i.solicitadaPorNombre ?? (i.solicitadaPor.startsWith('Integracion:') ? t('invitaciones.porAprobar.pidioIntegracion') : t('invitaciones.porAprobar.sinNombre')),
      },
      { id: 'desde', encabezado: t('invitaciones.porAprobar.columnas.desde'), ancho: 110, celda: (i) => <Typography variant="monoTabla">{formatearFecha(i.solicitadaEn)}</Typography> },
      {
        id: 'acciones',
        encabezado: t('invitaciones.porAprobar.columnas.acciones'),
        ancho: 230,
        celda: (i) =>
          i.puedesDecidir && puedeDecidir ? (
            <Stack direction="row" spacing={1} onClick={(e) => e.stopPropagation()}>
              <Button
                size="small"
                variant="outlined"
                startIcon={<CircleCheck size={14} aria-hidden />}
                aria-label={t('invitaciones.porAprobar.aprobarEtiqueta', { nombre: i.terceroNombre })}
                disabled={decidirInvitacion.isPending}
                onClick={() => aprobarInvitacion(i)}
              >
                {t('invitaciones.porAprobar.aprobar')}
              </Button>
              <Button
                size="small"
                variant="outlined"
                color="error"
                startIcon={<CircleSlash size={14} aria-hidden />}
                aria-label={t('invitaciones.porAprobar.rechazarEtiqueta', { nombre: i.terceroNombre })}
                disabled={decidirInvitacion.isPending}
                onClick={() => setRechazo({ tipo: 'invitacion', item: i })}
              >
                {t('invitaciones.porAprobar.rechazar')}
              </Button>
            </Stack>
          ) : (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {i.solicitadaPor === `Usuario:${usuarioActual ?? ''}` ? t('invitaciones.porAprobar.pediste') : t('invitaciones.porAprobar.noDecides')}
            </Typography>
          ),
      },
  ];

  const columnasLotes: ColumnaTabla<LoteDto>[] = [
      {
        id: 'lote',
        encabezado: t('invitaciones.porAprobar.columnasLotes.lote'),
        celda: (l) => (
          <Typography component="span" sx={{ fontWeight: 500 }}>
            {l.nombre}
          </Typography>
        ),
      },
      {
        id: 'tipos',
        encabezado: t('invitaciones.porAprobar.columnasLotes.tipos'),
        ancho: 200,
        celda: (l) => (
          <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
            {(l.tiposPorAprobar ?? []).map((codigo) => (
              <Chip key={codigo} size="small" variant="outlined" label={codigo} />
            ))}
          </Box>
        ),
      },
      { id: 'filas', encabezado: t('invitaciones.porAprobar.columnasLotes.filas'), ancho: 100, celda: (l) => t('invitaciones.porAprobar.filasDelLote', { count: procesables(l.totales) }) },
      {
        id: 'pidio',
        encabezado: t('invitaciones.porAprobar.columnasLotes.pidio'),
        ancho: 170,
        celda: (l) => nombres.get(idDePersona(l.creadoPor) ?? '') ?? t('invitaciones.porAprobar.sinNombre'),
      },
      { id: 'creado', encabezado: t('invitaciones.porAprobar.columnasLotes.creado'), ancho: 110, celda: (l) => <Typography variant="monoTabla">{formatearFecha(l.creadoEn)}</Typography> },
      {
        id: 'acciones',
        encabezado: t('invitaciones.porAprobar.columnasLotes.acciones'),
        ancho: 230,
        celda: (l) =>
          l.puedesDecidir && puedeDecidir ? (
            <Stack direction="row" spacing={1} onClick={(e) => e.stopPropagation()}>
              <Button
                size="small"
                variant="outlined"
                startIcon={<CircleCheck size={14} aria-hidden />}
                aria-label={t('invitaciones.porAprobar.aprobarLoteEtiqueta', { nombre: l.nombre })}
                disabled={decidirLote.isPending}
                onClick={() => aprobarLote(l)}
              >
                {t('invitaciones.porAprobar.aprobar')}
              </Button>
              <Button
                size="small"
                variant="outlined"
                color="error"
                startIcon={<CircleSlash size={14} aria-hidden />}
                aria-label={t('invitaciones.porAprobar.rechazarLoteEtiqueta', { nombre: l.nombre })}
                disabled={decidirLote.isPending}
                onClick={() => setRechazo({ tipo: 'lote', lote: l })}
              >
                {t('invitaciones.porAprobar.rechazar')}
              </Button>
            </Stack>
          ) : (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {l.creadoPor === `Usuario:${usuarioActual ?? ''}` ? t('invitaciones.porAprobar.pediste') : t('invitaciones.porAprobar.noDecides')}
            </Typography>
          ),
      },
  ];

  if (!puede) return <SinPermiso seccion={t('invitaciones.porAprobar.titulo')} />;

  const vacioVinculos = soloLasMias ? (
    <EstadoVacio compacto titulo={t('invitaciones.porAprobar.vacioMias')} texto={t('invitaciones.porAprobar.vacioMiasAyuda')} />
  ) : (
    <EstadoVacio compacto titulo={t('invitaciones.porAprobar.vacio')} texto={t('invitaciones.porAprobar.vacioAyuda')} />
  );

  return (
    <>
      <EncabezadoPagina
        antes={<EnlaceVolver a={`/e/${empresa.id}/vinculos`}>{t('invitaciones.porAprobar.volver')}</EnlaceVolver>}
        titulo={t('invitaciones.porAprobar.titulo')}
        subtitulo={t('invitaciones.porAprobar.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <FormControlLabel
            control={<Switch checked={soloLasMias} onChange={(e) => setSoloLasMias(e.target.checked)} />}
            label={t('invitaciones.porAprobar.soloLasMias')}
          />
        }
      />
      <Stack spacing={3}>
        <Box component="section" aria-label={t('invitaciones.porAprobar.vinculos')}>
          <Typography variant="h4" component="h2" sx={{ mb: 1 }}>
            {t('invitaciones.porAprobar.vinculos')}
          </Typography>
          <TablaConCursor
            etiqueta={t('invitaciones.porAprobar.vinculos')}
            columnas={columnasVinculos}
            filas={vinculos.data?.datos}
            claveFila={(i) => i.vinculoId}
            filasAltas
            cargando={vinculos.isPending}
            error={vinculos.isError ? `${t('invitaciones.porAprobar.errorLista')} ${mensajeDeError(vinculos.error, t)}` : null}
            onReintentar={() => void vinculos.refetch()}
            paginacion={paginaVinculos}
            siguienteCursor={vinculos.data?.siguienteCursor}
            onFila={(i) => void navegar(`/e/${empresa.id}/vinculos/${i.vinculoId}`)}
            etiquetaFila={(i) => t('invitaciones.porAprobar.abrirVinculo', { nombre: i.terceroNombre })}
            vacio={vacioVinculos}
          />
        </Box>
        <Box component="section" aria-label={t('invitaciones.porAprobar.lotes')}>
          <Typography variant="h4" component="h2" sx={{ mb: 1 }}>
            {t('invitaciones.porAprobar.lotes')}
          </Typography>
          <TablaConCursor
            etiqueta={t('invitaciones.porAprobar.lotes')}
            columnas={columnasLotes}
            filas={lotes.data ? lotesVisibles : undefined}
            claveFila={(l) => l.id}
            filasAltas
            cargando={lotes.isPending}
            error={lotes.isError ? `${t('invitaciones.porAprobar.errorLotes')} ${mensajeDeError(lotes.error, t)}` : null}
            onReintentar={() => void lotes.refetch()}
            paginacion={paginaLotes}
            siguienteCursor={lotes.data?.siguienteCursor}
            onFila={(l) => void navegar(`/e/${empresa.id}/invitaciones/lotes/${l.id}`)}
            etiquetaFila={(l) => t('invitaciones.porAprobar.abrirLote', { nombre: l.nombre })}
            vacio={<EstadoVacio compacto titulo={t('invitaciones.porAprobar.vacioLotes')} />}
          />
        </Box>
      </Stack>
      {rechazo && (
        <DialogoRechazo
          tipo={rechazo.tipo}
          subtitulo={rechazo.tipo === 'invitacion' ? rechazo.item.terceroNombre : rechazo.lote.nombre}
          ocupado={decidirInvitacion.isPending || decidirLote.isPending}
          error={errorDelRechazo ? mensajeDeErrorDeDecision(errorDelRechazo, t) : null}
          onConfirmar={rechazar}
          onCerrar={cerrarRechazo}
        />
      )}
    </>
  );
}
