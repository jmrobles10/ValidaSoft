import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, FormControlLabel, FormHelperText, FormLabel, Radio, RadioGroup, Stack, TextField,
  Typography, useMediaQuery, useTheme,
} from '@mui/material';
import type { SolicitudHabeasDto, VinculoPublicoDto } from '@validasoft/api-client';
import { FaltaParaEnviar } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { SelectorUnico } from '../ficha/selectores';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { useCrearSolicitud } from './api';
import {
  ALCANCES_DE_SUPRESION, BORRADOR_VACIO, cuerpoDeSolicitud, DESTINO_PLATAFORMA, diasHabilesDe, esAlcanceDeSupresion, esTipoDeSolicitud, hayIgualEnTramite,
  MAXIMO_DETALLE, TIPOS_DE_SOLICITUD, validarSolicitud, type BorradorDeSolicitud,
} from './modelo';

/**
 * Nueva solicitud de habeas data (Ley 1581): qué derecho ejerce, a quién se la dirige (a Validasoft por su bóveda o a una empresa
 * con la que tiene un vínculo) y, en la supresión, qué se suprime. Avisa antes qué pasará: quién la recibe, en cuántos días hábiles
 * se responde y, en la supresión, que lo que la ley obliga a guardar se conserva. En el celular ocupa toda la pantalla.
 */
export function DialogoNuevaSolicitud({ vinculos, solicitudes, onCerrar }: { vinculos: readonly VinculoPublicoDto[]; solicitudes: readonly SolicitudHabeasDto[]; onCerrar: () => void }) {
  const { t } = useTranslation();
  const tema = useTheme();
  const pantallaCompleta = useMediaQuery(tema.breakpoints.down('sm'));
  const mostrar = useAvisos((s) => s.mostrar);
  const crear = useCrearSolicitud();
  const [b, setB] = useState<BorradorDeSolicitud>(BORRADOR_VACIO);
  const [intentado, setIntentado] = useState(false);

  const empresas = [...new Map(vinculos.filter((v) => v.invitacionAceptada).map((v) => [v.empresa.id, v.empresa.nombre])).entries()]
    .map(([codigo, nombre]) => ({ codigo, nombre }))
    .sort((x, y) => x.nombre.localeCompare(y.nombre, 'es-CO'));
  const destinos = [{ codigo: DESTINO_PLATAFORMA, nombre: t('privacidad.solicitud.destinoPlataforma') }, ...empresas];
  const destinoNombre = destinos.find((d) => d.codigo === b.destino)?.nombre ?? '';

  const errores = validarSolicitud(b);
  const igual = hayIgualEnTramite(solicitudes, b.tipo, b.destino);
  const primero = igual ? 'en-curso' : errores[0];
  const cambiar = (parcial: Partial<BorradorDeSolicitud>) => setB((p) => ({ ...p, ...parcial }));

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (primero) return;
    crear.mutate(cuerpoDeSolicitud(b), {
      onSuccess: (creada) => {
        mostrar(t('privacidad.solicitud.enviada', { radicado: creada.radicado }));
        onCerrar();
      },
    });
  };

  const errorDeTipo = intentado && errores.includes('tipo-requerido') ? t('privacidad.solicitud.errores.tipo-requerido') : null;
  const errorDeDetalle = errores.find((e) => e === 'detalle-requerido' || e === 'detalle-largo');

  return (
    <Dialog open onClose={crear.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" fullScreen={pantallaCompleta} aria-labelledby="titulo-nueva-solicitud">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-nueva-solicitud">{t('privacidad.solicitud.titulo')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography sx={{ color: 'text.secondary' }}>{t('privacidad.solicitud.ayuda')}</Typography>
            {crear.isError && <Alert severity="error">{mensajeDeError(crear.error, t)}</Alert>}
            <FormControl required error={Boolean(errorDeTipo)}>
              <FormLabel id="tipo-solicitud">{t('privacidad.solicitud.queQuieres')}</FormLabel>
              <RadioGroup aria-labelledby="tipo-solicitud" value={b.tipo ?? ''} onChange={(e) => cambiar({ tipo: esTipoDeSolicitud(e.target.value) ? e.target.value : null })}>
                {TIPOS_DE_SOLICITUD.map((tipo) => (
                  <FormControlLabel
                    key={tipo}
                    value={tipo}
                    control={<Radio />}
                    sx={{ alignItems: 'flex-start', py: 0.5 }}
                    label={
                      <Box>
                        <Typography sx={{ fontWeight: 500, fontSize: 14 }}>{t(`privacidad.solicitud.tipos.${tipo}.titulo`)}</Typography>
                        <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
                          {t(`privacidad.solicitud.tipos.${tipo}.texto`, { dias: String(diasHabilesDe(tipo)) })}
                        </Typography>
                      </Box>
                    }
                  />
                ))}
              </RadioGroup>
              {errorDeTipo && <FormHelperText>{errorDeTipo}</FormHelperText>}
            </FormControl>

            <SelectorUnico
              opciones={destinos}
              valor={b.destino}
              onCambiar={(destino) => cambiar({ destino: destino ?? DESTINO_PLATAFORMA })}
              etiqueta={t('privacidad.solicitud.destino')}
              ayuda={b.destino === DESTINO_PLATAFORMA ? t('privacidad.solicitud.destinoPlataformaAyuda') : t('privacidad.solicitud.destinoEmpresaAyuda')}
            />

            {b.tipo === 'Supresion' && (
              <>
                <FormControl>
                  <FormLabel id="alcance-supresion">{t('privacidad.solicitud.alcance')}</FormLabel>
                  <RadioGroup aria-labelledby="alcance-supresion" value={b.alcance} onChange={(e) => cambiar({ alcance: esAlcanceDeSupresion(e.target.value) ? e.target.value : 'Todo' })}>
                    {ALCANCES_DE_SUPRESION.map((alcance) => (
                      <FormControlLabel key={alcance} value={alcance} control={<Radio />} label={t(`privacidad.solicitud.alcances.${alcance}`)} />
                    ))}
                  </RadioGroup>
                </FormControl>
                <Alert severity="warning">{t('privacidad.solicitud.avisoSupresion')}</Alert>
              </>
            )}

            <TextField
              label={b.tipo === 'Correccion' ? t('privacidad.solicitud.detalleCorreccion') : t('privacidad.solicitud.detalle')}
              value={b.detalle}
              onChange={(e) => cambiar({ detalle: e.target.value })}
              required={b.tipo === 'Correccion'}
              multiline
              minRows={3}
              error={intentado && Boolean(errorDeDetalle)}
              helperText={intentado && errorDeDetalle ? t(`privacidad.solicitud.errores.${errorDeDetalle}`) : t('privacidad.solicitud.detalleAyuda', { maximo: String(MAXIMO_DETALLE) })}
              slotProps={{ htmlInput: { maxLength: MAXIMO_DETALLE + 200 } }}
            />

            {b.tipo && (
              <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
                {t('privacidad.solicitud.queSigue', { destino: destinoNombre, dias: String(diasHabilesDe(b.tipo)) })}
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2, gap: 1, flexWrap: 'wrap' }}>
          {intentado && primero && <FaltaParaEnviar>{primero === 'en-curso' ? t('privacidad.solicitud.errores.en-curso', { radicado: igual?.radicado ?? '' }) : t(`privacidad.solicitud.errores.${primero}`)}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={crear.isPending}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={crear.isPending}>
            {t('privacidad.solicitud.enviar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
