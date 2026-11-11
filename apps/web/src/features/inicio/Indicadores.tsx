import { Box, Stack, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import { EsqueletoTarjeta } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { ErrorRecuperable } from '../../shared/componentes';
import { rutaInformeDeVentana } from '../informes/modelo';
import { mensajeDeError } from '../../shared/errores';
import { useIndicadores } from './api';
import { Cifra, GrupoCifras } from './Cifras';
import { aNumero, formatearConteo, formatearDias, leerVentana, VENTANA_POR_DEFECTO, VENTANAS, type Ventana } from './modelo';

/**
 * Indicadores de la empresa (IN-2): vínculos y certificados al momento; expedientes en curso al momento y, en la ventana
 * elegida (7, 30 o 90 días, en la URL), los aprobados, rechazados y el tiempo de la apertura a la aprobación.
 */
export function Indicadores({ empresaId, puedeVerVinculos }: { empresaId: string; puedeVerVinculos: boolean }) {
  const { t } = useTranslation();
  const [parametros, setParametros] = useSearchParams();
  const dias = leerVentana(parametros);
  const consulta = useIndicadores(empresaId, dias);
  const datos = consulta.data;
  const sinDato = t('inicio.indicadores.sinDato');

  // La ventana por defecto no se escribe en la URL.
  const cambiarVentana = (valor: Ventana) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor === VENTANA_POR_DEFECTO) nuevos.delete('dias');
        else nuevos.set('dias', String(valor));
        return nuevos;
      },
      { replace: true },
    );
  const aVinculos = (estado: string) => (puedeVerVinculos ? `/e/${empresaId}/vinculos?estado=${estado}` : null);
  // Los aprobados y su tiempo llevan al informe de estado con la misma ventana y medida (IF-7).
  const aInforme = rutaInformeDeVentana(empresaId, dias);

  return (
    <Box component="section" aria-labelledby="titulo-indicadores" sx={{ mt: 3 }}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', mb: 1.5 }}>
        <Typography variant="h3" component="h2" id="titulo-indicadores">
          {t('inicio.indicadores.titulo')}
        </Typography>
        <ToggleButtonGroup
          exclusive
          size="small"
          value={dias}
          onChange={(_evento, valor: Ventana | null) => {
            if (valor !== null) cambiarVentana(valor);
          }}
          aria-label={t('inicio.indicadores.ventana')}
          sx={{ '& .MuiToggleButton-root': { height: 32, px: 1.25, '&.Mui-selected': { bgcolor: 'acento.tinte', color: 'acento.main' } } }}
        >
          {VENTANAS.map((v) => (
            <ToggleButton key={v} value={v}>
              {t(`inicio.indicadores.ventanas.${v}`)}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      </Stack>

      {consulta.isError && !datos ? (
        <ErrorRecuperable mensaje={`${t('inicio.indicadores.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
      ) : !datos ? (
        <Stack spacing={1.5} aria-busy>
          {[0, 1, 2].map((i) => (
            <EsqueletoTarjeta key={i} alto={112} lineas={2} />
          ))}
        </Stack>
      ) : (
        <Stack spacing={1.5} sx={{ opacity: consulta.isPlaceholderData ? 0.6 : 1, transition: 'opacity 120ms' }} aria-busy={consulta.isPlaceholderData || undefined}>
          <GrupoCifras titulo={t('inicio.indicadores.vinculos')}>
            <Cifra etiqueta={t('inicio.indicadores.activos')} valor={formatearConteo(datos.vinculosActivos)} />
            {datos.vinculosPorVencer ? (
              <Cifra etiqueta={t('inicio.indicadores.porVencer')} valor={formatearConteo(datos.vinculosPorVencer)} a={aVinculos('PorVencer')} />
            ) : null}
            <Cifra etiqueta={t('inicio.indicadores.enValidacion')} valor={formatearConteo(datos.vinculosEnValidacion)} a={aVinculos('EnValidacion')} />
            <Cifra etiqueta={t('inicio.indicadores.invitados')} valor={formatearConteo(datos.vinculosInvitados)} a={aVinculos('Invitado')} />
            <Cifra etiqueta={t('inicio.indicadores.vencidos')} valor={formatearConteo(datos.vinculosVencidos)} a={aVinculos('Vencido')} />
            <Cifra etiqueta={t('inicio.indicadores.bloqueados')} valor={formatearConteo(datos.vinculosBloqueados)} a={aVinculos('Bloqueado')} />
          </GrupoCifras>
          <GrupoCifras titulo={t('inicio.indicadores.expedientes')} nota={t('inicio.indicadores.enLaVentana', { count: datos.dias })}>
            <Cifra etiqueta={t('inicio.indicadores.enCurso')} valor={formatearConteo(datos.expedientesEnCurso)} />
            <Cifra etiqueta={t('inicio.indicadores.observados')} valor={formatearConteo(datos.expedientesObservados)} />
            <Cifra etiqueta={t('inicio.indicadores.aprobados')} valor={formatearConteo(datos.expedientesAprobados)} a={aInforme} />
            <Cifra etiqueta={t('inicio.indicadores.rechazados')} valor={formatearConteo(datos.expedientesRechazados)} />
            <Cifra
              etiqueta={t('inicio.indicadores.tiempoPromedio')}
              a={aInforme}
              valor={formatearDias(datos.tiempoPromedioDias, sinDato)}
              nota={aNumero(datos.tiempoPromedioDias) === null ? t('inicio.indicadores.sinDatoAyuda') : undefined}
            />
            <Cifra
              etiqueta={t('inicio.indicadores.tiempoMediana')}
              valor={formatearDias(datos.tiempoMedianaDias, sinDato)}
              nota={aNumero(datos.tiempoMedianaDias) === null ? t('inicio.indicadores.sinDatoAyuda') : undefined}
            />
          </GrupoCifras>
          <GrupoCifras titulo={t('inicio.indicadores.certificados')}>
            <Cifra etiqueta={t('inicio.indicadores.vigentes')} valor={formatearConteo(datos.certificadosVigentes)} />
            <Cifra etiqueta={t('inicio.indicadores.conPendientes')} valor={formatearConteo(datos.certificadosConPendientes)} />
            <Cifra etiqueta={t('inicio.indicadores.noValidos')} valor={formatearConteo(datos.certificadosNoValidos)} />
          </GrupoCifras>
        </Stack>
      )}
    </Box>
  );
}
