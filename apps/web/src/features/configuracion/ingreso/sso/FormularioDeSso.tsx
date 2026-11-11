import { Alert, Box, Button, Chip, Stack, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarConfiguracionSso, type ConfiguracionDeSsoDto } from '@validasoft/api-client';
import { DosColumnas } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, IconoCarga } from '../../../../shared/componentes';
import { invalidarRuta } from '../../../../shared/consultas';
import { mensajeDeError } from '../../../../shared/errores';
import { useAvisos } from '../../../../shared/useAvisos';
import { useEmpresaActiva } from '../../../empresas/useEmpresas';
import { AccesoSso, AprovisionamientoSso } from './AprovisionamientoSso';
import { DominiosSso } from './DominiosSso';
import { ProveedorSso } from './ProveedorSso';
import { cuerpoDeSso, dominiosVerificados, formularioDesde, problemasDeSso, secretoParaEnviar, secretoValido, sinCambiosDeSso, type AccionDeSecreto, type FormularioSso, type ProblemaSso } from './modeloSso';

/** Lo que falta o está mal, junto en un solo aviso (no uno por campo). */
function AvisoDeProblemas({ problemas }: { problemas: readonly ProblemaSso[] }) {
  const { t } = useTranslation();
  if (problemas.length === 0) return null;
  return (
    <Alert severity="warning" sx={{ mb: 2 }}>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {t('sso.problemas.titulo')}
      </Typography>
      <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
        {problemas.map((p) => (
          <li key={p}>{t(`sso.problemas.${p}`)}</li>
        ))}
      </Box>
    </Alert>
  );
}

/** El formulario del SSO del grupo: se edita en memoria y se guarda de una vez; los dominios se agregan y verifican aparte (tienen su propio flujo). */
export function FormularioDeSso({ inicial }: { inicial: ConfiguracionDeSsoDto }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const { empresas } = useEmpresaActiva();
  const original = formularioDesde(inicial);
  const [formulario, setFormulario] = useState<FormularioSso>(original);
  const [accion, setAccion] = useState<AccionDeSecreto>('conservar');
  const [secreto, setSecreto] = useState('');

  const alCambiar = (cambios: Partial<FormularioSso>) => setFormulario((f) => ({ ...f, ...cambios }));
  const aEnviar = secretoParaEnviar(accion, secreto);
  const cambio = !sinCambiosDeSso(formulario, original, aEnviar);
  const problemas = problemasDeSso(formulario, dominiosVerificados(inicial).length > 0);
  const secretoMalo = accion === 'cambiar' && !secretoValido(secreto);
  const noSePuede = problemas.length > 0 || secretoMalo;

  const guardar = useMutation({
    mutationFn: () => actualizarConfiguracionSso(cuerpoDeSso(formulario, aEnviar), { empresa: null }),
    onSuccess: (guardado) => {
      mostrar(t('sso.guardado'));
      // Lo guardado es ahora lo original: el secreto escrito ya no se conserva en pantalla.
      setFormulario(formularioDesde(guardado));
      setAccion('conservar');
      setSecreto('');
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/configuracion/sso'),
  });

  return (
    <Box
      component="form"
      onSubmit={(evento) => {
        evento.preventDefault();
        if (cambio && !noSePuede && !guardar.isPending) guardar.mutate();
      }}
      noValidate
    >
      <EncabezadoPagina
        titulo={t('sso.titulo')}
        subtitulo={t('sso.ayuda')}
        acciones={
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            <Chip size="small" variant="outlined" color={inicial.activo ? 'success' : 'default'} label={inicial.activo ? t('sso.estado.activo') : inicial.configurado ? t('sso.estado.apagado') : t('sso.estado.sinConfigurar')} />
            {cambio && !guardar.isPending && (
              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                {t('sso.cambiosSinGuardar')}
              </Typography>
            )}
            <Button type="submit" variant="contained" disabled={!cambio || noSePuede || guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
              {t('sso.guardar')}
            </Button>
          </Stack>
        }
      />
      {guardar.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mensajeDeError(guardar.error, t)}
        </Alert>
      )}
      {cambio && <AvisoDeProblemas problemas={problemas} />}
      {formulario.activo && formulario.modo === 'Exclusivo' && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {t('sso.acceso.advertenciaExclusivo')}
        </Alert>
      )}
      <DosColumnas
        izquierda={
          <>
            <ProveedorSso
              config={inicial}
              formulario={formulario}
              alCambiar={alCambiar}
              accionDeSecreto={accion}
              secreto={secreto}
              alCambiarSecreto={(a, valor) => {
                setAccion(a);
                setSecreto(valor);
              }}
              sinGuardar={cambio}
            />
            <DominiosSso dominios={inicial.dominios} />
          </>
        }
        derecha={
          <>
            <AccesoSso formulario={formulario} alCambiar={alCambiar} tieneDominioVerificado={dominiosVerificados(inicial).length > 0} />
            <AprovisionamientoSso formulario={formulario} empresas={empresas} alCambiar={alCambiar} />
          </>
        }
      />
    </Box>
  );
}
