import { Alert, Box, Button, FormControlLabel, Stack, Switch, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type AreaDto, type ConfiguracionSlaDto } from '@validasoft/api-client';
import { CampoNumero, EsqueletoFormulario, EsqueletoTabla, EstadoVacio, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Timer } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, IconoCarga, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useAreas } from '../areas/api';
import { useConfiguracionSla, useGuardarConfiguracionSla } from './api';
import { aActualizarDto, borradorDeSla, errorDelPorcentaje, errorDeHoras, errorDePorcentaje, esValido, hayCambios, type BorradorSla } from './modelo';

const COLUMNAS = { xs: 'minmax(0, 1fr)', md: '340px minmax(0, 1fr)' } as const;

/**
 * Plazos de revisión de la empresa (OP-10, OP-11): a qué porcentaje del plazo se avisa, si los administradores se enteran de
 * los incumplimientos y el plazo por defecto de cada área (el del flujo manda sobre este). Sin `configuracion:escribir` es de lectura.
 */
export function PaginaPlazosDeRevision() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const puedeEscribir = permisos.puede(SCOPES.configuracionEscribir);
  const consulta = useConfiguracionSla(empresa.id, puedeLeer);
  const areas = useAreas(empresa.id, puedeLeer);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.plazos-de-revision')} />;

  const encabezado = <EncabezadoPagina titulo={t('sla.titulo')} subtitulo={t('sla.subtitulo', { empresa: empresa.nombre })} />;
  if (consulta.isError || areas.isError) {
    const error = consulta.error ?? areas.error;
    return (
      <>
        {encabezado}
        <ErrorRecuperable
          mensaje={`${t('sla.errorCarga')} ${mensajeDeError(error, t)}`}
          onReintentar={() => {
            void consulta.refetch();
            void areas.refetch();
          }}
        />
      </>
    );
  }
  if (!consulta.data || !areas.data) {
    return (
      <>
        {encabezado}
        <Box sx={{ display: 'grid', gridTemplateColumns: COLUMNAS, gap: 2, alignItems: 'start' }}>
          <EsqueletoFormulario conTarjeta campos={2} etiqueta={t('sla.titulo')} />
          <EsqueletoTabla etiqueta={t('sla.areas.titulo')} columnas={2} filas={4} />
        </Box>
      </>
    );
  }
  // Se arma de nuevo cuando el servidor cambia (al guardar): el formulario no copia estado en efectos.
  return (
    <FormularioSla
      key={consulta.data.actualizadaEn ?? 'sin-guardar'}
      empresaId={empresa.id}
      empresaNombre={empresa.nombre}
      configuracion={consulta.data}
      areas={areas.data}
      puedeEscribir={puedeEscribir}
    />
  );
}

function FormularioSla({
  empresaId,
  empresaNombre,
  configuracion,
  areas,
  puedeEscribir,
}: {
  empresaId: string;
  empresaNombre: string;
  configuracion: ConfiguracionSlaDto;
  areas: readonly AreaDto[];
  puedeEscribir: boolean;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useGuardarConfiguracionSla(empresaId);
  const original = borradorDeSla(configuracion);
  const [borrador, setBorrador] = useState<BorradorSla>(original);
  const [intentado, setIntentado] = useState(false);
  const errorPorcentaje = errorDePorcentaje(borrador.porcentaje);
  const valido = esValido(borrador);
  const cambio = hayCambios(borrador, original);
  const nombreDe = (areaId: string) => areas.find((a) => a.id === areaId)?.nombre ?? areaId.slice(0, 8);

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (!valido || !cambio || !puedeEscribir) return;
    guardar.mutate(aActualizarDto(borrador), { onSuccess: () => mostrar(t('sla.guardado')) });
  };

  const porcentajeEnError = errorDelPorcentaje(guardar.error) || (errorPorcentaje !== null && (intentado || borrador.porcentaje !== original.porcentaje));
  const textoErrorPorcentaje = errorPorcentaje ? t(`sla.avisos.errores.${errorPorcentaje}`) : errorDelPorcentaje(guardar.error) ? mensajeDeError(guardar.error, t) : undefined;

  return (
    <Box component="form" noValidate onSubmit={enviar}>
      <EncabezadoPagina
        titulo={t('sla.titulo')}
        subtitulo={t('sla.subtitulo', { empresa: empresaNombre })}
        acciones={
          puedeEscribir && (
            <Button type="submit" variant="contained" disabled={!cambio || guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
              {t('sla.guardar')}
            </Button>
          )
        }
      />
      {guardar.isError && !errorDelPorcentaje(guardar.error) && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mensajeDeError(guardar.error, t)}
        </Alert>
      )}
      {!puedeEscribir && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {t('sla.soloLectura')}
        </Alert>
      )}
      <Box sx={{ display: 'grid', gridTemplateColumns: COLUMNAS, gap: 2, alignItems: 'start' }}>
        <Tarjeta titulo={t('sla.avisos.titulo')}>
          <Stack spacing={2}>
            <CampoNumero
              etiqueta={t('sla.avisos.porcentaje')}
              valor={borrador.porcentaje}
              onCambiar={(texto) => setBorrador((b) => ({ ...b, porcentaje: texto }))}
              maxDigitos={3}
              requerido
              deshabilitado={!puedeEscribir || guardar.isPending}
              error={porcentajeEnError}
              ayuda={porcentajeEnError ? textoErrorPorcentaje : t('sla.avisos.porcentajeAyuda')}
              sx={{ maxWidth: 200 }}
            />
            <Box>
              <FormControlLabel
                control={
                  <Switch
                    checked={borrador.avisarAdministradores}
                    onChange={(e) => setBorrador((b) => ({ ...b, avisarAdministradores: e.target.checked }))}
                    disabled={!puedeEscribir || guardar.isPending}
                  />
                }
                label={t('sla.avisos.administradores')}
              />
              <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', ml: 0.5 }}>
                {t('sla.avisos.administradoresAyuda')}
              </Typography>
            </Box>
          </Stack>
        </Tarjeta>

        <Tarjeta titulo={t('sla.areas.titulo')}>
          <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
            {t('sla.areas.ayuda')}
          </Typography>
          {configuracion.areas.length === 0 ? (
            <EstadoVacio compacto icono={Timer} titulo={t('sla.areas.vacio')} texto={t('sla.areas.vacioAyuda')} />
          ) : (
            <MarcoTabla>
              <Table size="small" aria-label={t('sla.areas.titulo')}>
                <TableHead>
                  <TableRow>
                    <TableCell>{t('sla.areas.area')}</TableCell>
                    <TableCell sx={{ width: 180 }}>{t('sla.areas.horas')}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {configuracion.areas.map((a) => {
                    const texto = borrador.horas[a.areaId] ?? '';
                    const enError = errorDeHoras(texto) !== null;
                    return (
                      <TableRow key={a.areaId}>
                        <TableCell sx={{ fontWeight: 500 }}>{nombreDe(a.areaId)}</TableCell>
                        <TableCell>
                          <CampoNumero
                            etiqueta={t('sla.areas.horasDe', { area: nombreDe(a.areaId) })}
                            valor={texto}
                            onCambiar={(nuevo) => setBorrador((b) => ({ ...b, horas: { ...b.horas, [a.areaId]: nuevo } }))}
                            maxDigitos={4}
                            deshabilitado={!puedeEscribir || guardar.isPending}
                            error={enError}
                            ayuda={enError ? t('sla.areas.errorHoras') : undefined}
                            placeholder={t('sla.indicador.sinPlazo')}
                            // La etiqueta solo la lee el lector de pantalla: el encabezado de la columna ya dice qué es.
                            sx={{ width: 150, '& .MuiInputLabel-root': visualmenteOculto }}
                          />
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </MarcoTabla>
          )}
        </Tarjeta>
      </Box>
    </Box>
  );
}
