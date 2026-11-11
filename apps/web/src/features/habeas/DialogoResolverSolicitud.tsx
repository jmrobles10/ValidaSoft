import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, Stack, TextField, Typography } from '@mui/material';
import type { SolicitudHabeasDto } from '@validasoft/api-client';
import { FaltaParaEnviar } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useResolverSolicitud, type OrigenDeSolicitudes } from './api';
import {
  BASE_LEGAL_SUGERIDA,
  BORRADOR_DE_RESPUESTA_VACIO,
  cuerpoDeRespuesta,
  esSupresionDeTodo,
  MAXIMO_BASE_LEGAL,
  MAXIMO_RESPUESTA,
  validarRespuesta,
  type BorradorDeRespuesta,
  type DecisionDeSolicitud,
} from './modelo';
import { NombreDeDerecho } from './NombreDeDerecho';
import { PlanDeSupresion } from './PlanDeSupresion';

/**
 * Responder una solicitud (OD-1, OD-3, OD-6): atenderla o rechazarla, con la respuesta que recibe el titular. Antes de enviar dice qué
 * pasa y a quién le llega: el titular lo recibe en su portal y por correo con la constancia, y queda en la auditoría. En la supresión
 * que atiende la plataforma, muestra primero lo que se elimina, lo que se conserva por ley y lo que se anonimiza.
 */
export function DialogoResolverSolicitud({ solicitud, origen, onCerrar }: { solicitud: SolicitudHabeasDto; origen: OrigenDeSolicitudes; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const resolver = useResolverSolicitud(origen);
  const [b, setB] = useState<BorradorDeRespuesta>(BORRADOR_DE_RESPUESTA_VACIO);
  const [intentado, setIntentado] = useState(false);
  const errores = validarRespuesta(b);
  const primero = errores[0];
  const ejecutaSupresion = origen.tipo === 'plataforma' && esSupresionDeTodo(solicitud) && b.decision === 'Atender';
  const supresionDeEmpresa = origen.tipo === 'empresa' && solicitud.tipo === 'Supresion' && b.decision === 'Atender';

  const enviar = () => {
    setIntentado(true);
    if (primero) return;
    resolver.mutate(
      { id: solicitud.id, cuerpo: cuerpoDeRespuesta(b) },
      {
        onSuccess: () => {
          mostrar(t('habeas.resolver.enviada', { radicado: solicitud.radicado }));
          onCerrar();
        },
      },
    );
  };

  return (
    <Dialog open onClose={resolver.isPending ? undefined : onCerrar} maxWidth="sm" fullWidth aria-labelledby="titulo-resolver-habeas">
      <DialogTitle id="titulo-resolver-habeas">
        {t('habeas.resolver.titulo', { radicado: solicitud.radicado })}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25 }}>
          <NombreDeDerecho solicitud={solicitud} />
        </Typography>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 0.5 }}>
          <Alert severity="info" sx={{ overflowWrap: 'anywhere' }}>{t('habeas.resolver.queOcurre', { correo: solicitud.titular?.correo ?? t('habeas.resolver.alTitular') })}</Alert>

          <FormControl>
            <FormLabel id="decision-habeas" sx={{ fontSize: 13, mb: 0.25 }}>
              {t('habeas.resolver.decision')}
            </FormLabel>
            <RadioGroup row aria-labelledby="decision-habeas" value={b.decision} onChange={(e) => setB((p) => ({ ...p, decision: e.target.value as DecisionDeSolicitud }))}>
              <FormControlLabel value="Atender" control={<Radio size="small" />} label={t('habeas.resolver.atender')} />
              <FormControlLabel value="Rechazar" control={<Radio size="small" />} label={t('habeas.resolver.rechazar')} />
            </RadioGroup>
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              {b.decision === 'Atender' ? t('habeas.resolver.atenderAyuda') : t('habeas.resolver.rechazarAyuda')}
            </Typography>
          </FormControl>

          {ejecutaSupresion && (
            <>
              <PlanDeSupresion solicitudId={solicitud.id} />
              <Alert severity="warning">{t('habeas.resolver.irreversible')}</Alert>
            </>
          )}
          {supresionDeEmpresa && <Alert severity="info">{t('habeas.resolver.supresionDeEmpresa')}</Alert>}

          <TextField
            label={t('habeas.resolver.respuesta')}
            value={b.respuesta}
            onChange={(e) => setB((p) => ({ ...p, respuesta: e.target.value }))}
            multiline
            minRows={4}
            error={intentado && (errores.includes('respuesta-corta') || errores.includes('respuesta-larga'))}
            helperText={t('habeas.resolver.respuestaAyuda', { actual: String(b.respuesta.trim().length), maximo: String(MAXIMO_RESPUESTA) })}
            slotProps={{ htmlInput: { maxLength: MAXIMO_RESPUESTA + 200 } }}
          />
          {b.decision === 'Rechazar' && (
            <TextField
              label={t('habeas.resolver.baseLegal')}
              value={b.baseLegal}
              onChange={(e) => setB((p) => ({ ...p, baseLegal: e.target.value }))}
              placeholder={BASE_LEGAL_SUGERIDA}
              error={errores.includes('base-legal-larga')}
              helperText={t('habeas.resolver.baseLegalAyuda')}
              slotProps={{ htmlInput: { maxLength: MAXIMO_BASE_LEGAL + 50 } }}
            />
          )}
          {resolver.isError && <Alert severity="error">{mensajeDeError(resolver.error, t)}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        {intentado && primero && <FaltaParaEnviar>{t(`habeas.resolver.errores.${primero}`)}</FaltaParaEnviar>}
        <Button variant="text" onClick={onCerrar} disabled={resolver.isPending} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" color={ejecutaSupresion ? 'error' : 'primary'} onClick={enviar} disabled={resolver.isPending} startIcon={resolver.isPending ? <IconoCarga /> : undefined}>
          {ejecutaSupresion ? t('habeas.resolver.enviarYSuprimir') : t('habeas.resolver.enviar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
