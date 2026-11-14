import { Alert, Box, Button, Card, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import { esErrorApi, type AccesoSolicitadoDto } from '@validasoft/api-client';
import { ChipEstado, EsqueletoTarjetas, EstadoVacio, formatearFecha } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { MailCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CabeceraPagina, ErrorRecuperable } from '../shared/componentes';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { useAccesosSolicitados, useDecidirAcceso } from './api';
import { SeccionContactos } from './SeccionContactos';
import { decididas, listaDeEmpresas, porDecidir, puedoDecidir } from './modelo';

interface Decision {
  solicitud: AccesoSolicitadoDto;
  aprobar: boolean;
}

/**
 * Accesos por aprobar (acceso aislado): una empresa invitó a un correo que no está entre los contactos del tercero. Quien ya es contacto lo aprueba —el correo pasa
 * a ser contacto y lo que subió para la empresa pasa a la bóveda— o lo rechaza —se cortan sus sesiones y la empresa debe invitar a otro correo—. Una sesión aislada
 * no llega aquí (la API responde 403 y la pantalla no ofrece la ruta).
 */
export function PaginaAccesos() {
  const { t } = useTranslation();
  const consulta = useAccesosSolicitados();
  const [decision, setDecision] = useState<Decision | null>(null);

  const solicitudes = consulta.data ?? [];
  const pendientes = porDecidir(solicitudes);
  const hechas = decididas(solicitudes);

  return (
    <>
      <CabeceraPagina titulo={t('accesoAislado.pagina.titulo')} sub={t('accesoAislado.pagina.sub')} />
      {consulta.isPending ? (
        <EsqueletoTarjetas cantidad={2} lineas={3} />
      ) : consulta.isError ? (
        <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />
      ) : solicitudes.length === 0 ? (
        <EstadoVacio icono={MailCheck} titulo={t('accesoAislado.pagina.vacio')} texto={t('accesoAislado.pagina.vacioTexto')} />
      ) : (
        <Stack spacing={3}>
          {pendientes.length > 0 && (
            <Box component="section" aria-labelledby="accesos-por-decidir">
              <Typography id="accesos-por-decidir" variant="h3" component="h2" sx={{ mb: 1.5 }}>
                {t('accesoAislado.pagina.porDecidir')}
              </Typography>
              <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
                {pendientes.map((s) => (
                  <TarjetaAcceso key={s.id} solicitud={s} onDecidir={(aprobar) => setDecision({ solicitud: s, aprobar })} />
                ))}
              </Stack>
            </Box>
          )}
          {hechas.length > 0 && (
            <Box component="section" aria-labelledby="accesos-decididos">
              <Typography id="accesos-decididos" variant="h3" component="h2" sx={{ mb: 1.5 }}>
                {t('accesoAislado.pagina.decididos')}
              </Typography>
              <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
                {hechas.map((s) => (
                  <TarjetaAcceso key={s.id} solicitud={s} onDecidir={(aprobar) => setDecision({ solicitud: s, aprobar })} />
                ))}
              </Stack>
            </Box>
          )}
        </Stack>
      )}
      <Box sx={{ mt: 4 }}>
        <SeccionContactos />
      </Box>
      {decision && <DialogoDeDecision decision={decision} onCerrar={() => setDecision(null)} />}
    </>
  );
}

function TarjetaAcceso({ solicitud, onDecidir }: { solicitud: AccesoSolicitadoDto; onDecidir: (aprobar: boolean) => void }) {
  const { t } = useTranslation();
  const decidida = solicitud.estado !== 'Pendiente';
  const clave = solicitud.estado === 'Aprobada' ? 'Aprobada' : solicitud.estado === 'Rechazada' ? 'Rechazada' : 'Pendiente';
  const estado = clave === 'Aprobada' ? 'Aprobado' : clave === 'Rechazada' ? 'Rechazado' : 'Pendiente';
  return (
    <Card component="li" sx={{ p: 2 }} aria-label={solicitud.correo}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography component="h3" sx={{ fontSize: 16, lineHeight: '22px', overflowWrap: 'anywhere' }}>
            {solicitud.correo}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px' }}>
            {t('accesoAislado.tarjeta.invitadoPor', { empresas: listaDeEmpresas(solicitud.empresas) })}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
            {t('accesoAislado.tarjeta.desde', { fecha: formatearFecha(solicitud.desde) })}
          </Typography>
        </Box>
        <ChipEstado estado={estado} etiqueta={t(`accesoAislado.tarjeta.estados.${clave}`)} size="medium" />
      </Stack>
      {solicitud.contactoDesplazado && (
        <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px', mt: 1 }}>{t('accesoAislado.tarjeta.desplazado')}</Typography>
      )}
      {decidida && solicitud.decididoEn && (
        <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px', mt: 1 }}>
          {decididoPor(solicitud, t)}
          {solicitud.motivo ? ` · ${t('accesoAislado.tarjeta.motivo', { motivo: solicitud.motivo })}` : ''}
        </Typography>
      )}
      {!decidida && !puedoDecidir(solicitud) && (
        <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px', mt: 1 }}>
          {solicitud.decideLaPlataforma ? t('accesoAislado.tarjeta.laPlataformaDecide') : t('accesoAislado.tarjeta.soloVerificados')}
        </Typography>
      )}
      {solicitud.estado !== 'Aprobada' && puedoDecidir(solicitud) && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.5 }}>
          <Button variant={decidida ? 'outlined' : 'contained'} onClick={() => onDecidir(true)} aria-label={t('accesoAislado.tarjeta.aprobarEste', { correo: solicitud.correo })}>
            {decidida ? t('accesoAislado.tarjeta.reconsiderar') : t('accesoAislado.tarjeta.aprobar')}
          </Button>
          {!decidida && (
            <Button variant="outlined" color="error" onClick={() => onDecidir(false)} aria-label={t('accesoAislado.tarjeta.rechazarEste', { correo: solicitud.correo })}>
              {t('accesoAislado.tarjeta.rechazar')}
            </Button>
          )}
        </Stack>
      )}
    </Card>
  );
}

/** Quién decidió: un contacto (su correo), la plataforma o la verificación del propio correo. */
function decididoPor(solicitud: AccesoSolicitadoDto, t: TFunction): string {
  const fecha = formatearFecha(solicitud.decididoEn ?? solicitud.desde);
  if (solicitud.decididoVia === 'Plataforma') return t('accesoAislado.tarjeta.decididoPorLaPlataforma', { fecha });
  if (solicitud.decididoVia === 'Verificacion') return t('accesoAislado.tarjeta.decididoPorVerificacion', { fecha });
  return t('accesoAislado.tarjeta.decididoPor', { correo: solicitud.decididoPor ?? '', fecha });
}

/** Confirma una decisión que cambia quién entra a la cuenta: dice qué pasa y, al rechazar, deja anotar el motivo. */
function DialogoDeDecision({ decision, onCerrar }: { decision: Decision; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const decidir = useDecidirAcceso();
  const [motivo, setMotivo] = useState('');
  const { solicitud, aprobar } = decision;
  const empresas = listaDeEmpresas(solicitud.empresas);
  const clave = aprobar ? 'aprobar' : 'rechazar';

  const confirmar = () =>
    decidir.mutate(
      { id: solicitud.id, aprobar, motivo },
      {
        onSuccess: () => {
          mostrar(t(aprobar ? 'accesoAislado.exito.aprobado' : 'accesoAislado.exito.rechazado', { correo: solicitud.correo }));
          onCerrar();
        },
      },
    );

  return (
    <Dialog open onClose={decidir.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="dialogo-acceso-titulo">
      <DialogTitle id="dialogo-acceso-titulo">{t(`accesoAislado.dialogo.${clave}Titulo`, { correo: solicitud.correo })}</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ color: 'text.primary' }}>{t(`accesoAislado.dialogo.${clave}Texto`, { empresas })}</DialogContentText>
        {!aprobar && (
          <TextField
            label={t('accesoAislado.dialogo.motivo')}
            helperText={t('accesoAislado.dialogo.motivoAyuda')}
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            fullWidth
            multiline
            minRows={2}
            sx={{ mt: 2 }}
            slotProps={{ htmlInput: { maxLength: 500 } }}
          />
        )}
        {decidir.isError && (
          <Alert severity="error" role="alert" sx={{ mt: 2 }}>
            {esErrorApi(decidir.error) ? mensajeDeError(decidir.error, t) : t('comun.reintentar')}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onCerrar} disabled={decidir.isPending}>
          {t('accesoAislado.dialogo.cancelar')}
        </Button>
        <Button variant="contained" color={aprobar ? 'primary' : 'error'} onClick={confirmar} disabled={decidir.isPending}>
          {t(`accesoAislado.dialogo.${clave}Confirmar`)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
