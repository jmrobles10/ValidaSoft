import { Alert, Box, Button, Card, Checkbox, FormControlLabel, Stack, Typography } from '@mui/material';
import { esErrorApi } from '@validasoft/api-client';
import { formatearFecha, PaginaEstado } from '@validasoft/ui';
import { CircleCheck, CirclePause, CircleSlash, FileClock, SearchX } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, Link as RouterLink, useParams } from 'react-router';
import { useSesionTercero } from '../sesion/sesionTercero';
import { useAceptarInvitacion, useInvitacion } from '../shared/api';
import { useAvisos } from '../shared/useAvisos';
import { CabeceraPagina, ErrorRecuperable, EsqueletoTarjetas } from '../shared/componentes';
import { MarcaDeEmpresa } from '../shared/MarcaDeEmpresa';
import { mensajeDeError } from '../shared/errores';
import { claveTexto, consentimientosDe, estadoInvitacion, falloDeAceptacion, marcadosVigentes, puedeAceptar, textosOrdenados } from './textos';

/**
 * Invitación de una empresa: los textos legales vigentes con su versión (el aviso de la plataforma solo si falta) y
 * «Aceptar», que registra la evidencia y comparte con la empresa los documentos que exige (docs/05 §2.9.1).
 */
export function PaginaInvitacion() {
  const { t } = useTranslation();
  const { vinculoId = '' } = useParams();
  const consulta = useInvitacion(vinculoId);
  const aceptacion = useAceptarInvitacion(vinculoId);
  const mostrar = useAvisos((s) => s.mostrar);
  const aislada = useSesionTercero((s) => s.sesion?.aislada === true);
  const [marcados, setMarcados] = useState<Set<string>>(new Set());
  const [intentado, setIntentado] = useState(false);

  if (consulta.isPending) return <EsqueletoTarjetas cantidad={3} />;
  if (consulta.isError) {
    if (esErrorApi(consulta.error) && consulta.error.esNoEncontrado) {
      return <PaginaEstado icono={SearchX} titulo={t('enlace.fallos.no-encontrado.titulo')} texto={t('empresas.vacioTexto')} />;
    }
    return <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />;
  }

  const invitacion = consulta.data;
  const empresa = invitacion.vinculo.empresa.nombre;
  const estado = estadoInvitacion(invitacion);
  const expedienteId = invitacion.vinculo.expedienteId;

  if (estado === 'suspendida' || estado === 'terminada') {
    return (
      <PaginaEstado
        icono={estado === 'suspendida' ? CirclePause : CircleSlash}
        titulo={t(`vinculoSituacion.${estado}.titulo`, { empresa })}
        texto={t(`vinculoSituacion.${estado}.texto`, { empresa })}
        acciones={
          <Button component={RouterLink} to="/boveda" variant="outlined">
            {t('invitacion.irBoveda')}
          </Button>
        }
      />
    );
  }

  // Ya aceptó y tiene revisión abierta (p. ej. entró con el enlace del correo de observación): directo a ella.
  if (estado === 'aceptada' && expedienteId) return <Navigate to={`/expediente/${expedienteId}`} replace />;

  if (estado === 'aceptada') {
    return (
      <PaginaEstado
        icono={CircleCheck}
        titulo={t('invitacion.aceptadaTitulo')}
        texto={t('invitacion.aceptadaTexto', { empresa, fecha: invitacion.vinculo.invitacionAceptadaEn ? formatearFecha(invitacion.vinculo.invitacionAceptadaEn) : '—' })}
        acciones={
          <>
            {expedienteId && (
              <Button component={RouterLink} to={`/expediente/${expedienteId}`} variant="contained">
                {t('invitacion.irExpediente')}
              </Button>
            )}
            <Button component={RouterLink} to="/boveda" variant={expedienteId ? 'outlined' : 'contained'}>
              {t('invitacion.irBoveda')}
            </Button>
          </>
        }
      />
    );
  }

  if (estado === 'faltan-textos') {
    return <PaginaEstado icono={FileClock} titulo={t('invitacion.faltanTitulo', { empresa })} texto={t('invitacion.faltanTexto')} />;
  }
  if (estado === 'no-disponible') {
    return <PaginaEstado icono={FileClock} titulo={t('invitacion.noDisponibleTitulo')} texto={t('invitacion.noDisponibleTexto', { empresa })} />;
  }

  const fallo = aceptacion.isError ? falloDeAceptacion(aceptacion.error) : null;
  const listo = puedeAceptar(invitacion, marcados);

  const alternar = (clave: string) =>
    setMarcados((previos) => {
      const siguientes = new Set(previos);
      if (siguientes.has(clave)) siguientes.delete(clave);
      else siguientes.add(clave);
      return siguientes;
    });

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (!listo) return;
    aceptacion.mutate(consentimientosDe(invitacion, marcados), {
      onSuccess: (resultado) => {
        mostrar(
          resultado.compartidos.length > 0
            ? `${t('invitacion.exito', { empresa })}: ${t('invitacion.compartidos', { count: resultado.compartidos.length })}`
            : t('invitacion.exito', { empresa }),
        );
      },
      onError: () => {
        // Si un texto cambió, la recarga trae la versión nueva y ese texto vuelve a quedar sin marcar.
        void consulta.refetch().then((r) => r.data && setMarcados((previos) => marcadosVigentes(r.data, previos)));
      },
    });
  };

  return (
    <MarcaDeEmpresa empresa={invitacion.vinculo.empresa}>
    <Box component="form" onSubmit={enviar} noValidate>
      <CabeceraPagina titulo={t('invitacion.titulo', { empresa })} sub={t('invitacion.sub', { empresa, tipo: invitacion.vinculo.tipoVinculo.nombre.toLowerCase() })} />

      <Typography component="h2" sx={{ fontWeight: 600, fontSize: 13, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'text.secondary', mb: 1 }}>
        {t('invitacion.textos')}
      </Typography>
      <Stack spacing={1.5} sx={{ mb: 2.5 }}>
        {textosOrdenados(invitacion).map((texto) => {
          const clave = claveTexto(texto);
          return (
            <Card key={clave} component="section" aria-labelledby={`texto-${clave}`} sx={{ p: 2 }}>
              <Typography id={`texto-${clave}`} component="h3" sx={{ fontWeight: 600, fontSize: 16 }}>
                {t(`invitacion.tipos.${texto.tipo}`, { empresa })}
              </Typography>
              <Typography sx={{ color: 'text.secondary', fontSize: 13, mb: 1 }}>
                {t('invitacion.versionPublicada', { version: String(texto.textoVersion), fecha: formatearFecha(texto.publicadoEn) })}
              </Typography>
              <Box
                tabIndex={0}
                aria-label={t(`invitacion.tipos.${texto.tipo}`, { empresa })}
                sx={{ maxHeight: 220, overflowY: 'auto', p: 1.5, borderRadius: 1, bgcolor: 'superficie.nivel2', fontSize: 14, lineHeight: '21px', whiteSpace: 'pre-wrap' }}
              >
                {texto.contenido}
              </Box>
              <FormControlLabel
                sx={{ mt: 1, mx: 0, minHeight: 44 }}
                control={<Checkbox checked={marcados.has(clave)} onChange={() => alternar(clave)} />}
                label={t('invitacion.leiYAcepto')}
              />
            </Card>
          );
        })}
      </Stack>

      <Stack spacing={1.5}>
        {fallo && fallo !== 'otro' && <Alert severity={fallo === 'ya-aceptada' ? 'info' : 'warning'}>{t(`invitacion.fallos.${fallo}`)}</Alert>}
        {fallo === 'otro' && <Alert severity="error">{mensajeDeError(aceptacion.error, t)}</Alert>}
        {intentado && !listo && <Alert severity="info">{t('invitacion.marcaTodos')}</Alert>}
        <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>
          {t('invitacion.ayudaAceptar', { empresa })}
        </Typography>
        {aislada && <Alert severity="info">{t('accesoAislado.aviso.enInvitacion', { empresa })}</Alert>}
        <Button type="submit" variant="contained" size="large" fullWidth disabled={aceptacion.isPending}>
          {t('invitacion.aceptar')}
        </Button>
      </Stack>
    </Box>
    </MarcaDeEmpresa>
  );
}
