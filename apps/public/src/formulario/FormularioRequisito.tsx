import { Alert, AlertTitle, Box, Button, Card, Stack } from '@mui/material';
import type { CampoFormularioDto, ConfirmacionCargaDto } from '@validasoft/api-client';
import { FaltaParaEnviar } from '@validasoft/ui';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import type { DestinoCarga } from '../boveda/subida';
import { useEnviarFormulario } from '../shared/api';
import { mensajeDeError } from '../shared/errores';
import { CamposFormulario } from './CamposFormulario';
import { cuerpoFormulario, esErrorDeCampo } from './formulario';
import { useFormulario } from './useFormulario';

/**
 * Un documento que se entrega llenando campos (`modo: Formulario`, TD-4). El botón queda deshabilitado mientras falte
 * algo, con lo que falta al lado; un 422 de la API marca el campo que señala.
 */
export function FormularioRequisito({
  tipo,
  campos,
  destino,
  volver,
  iniciales,
}: {
  tipo: string;
  campos: readonly CampoFormularioDto[];
  destino: DestinoCarga;
  volver: string | null;
  /** Lo ya declarado en la versión vigente, para actualizar sin escribir todo de nuevo. */
  iniciales?: Readonly<Record<string, string>>;
}) {
  const { t } = useTranslation();
  const formulario = useFormulario(campos, false, iniciales);
  const envio = useEnviarFormulario();
  const [resultado, setResultado] = useState<ConfirmacionCargaDto | null>(null);

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    const cuerpo = cuerpoFormulario(destino, formulario.datos());
    if (!formulario.completo || !cuerpo) return;
    envio.mutate(cuerpo, { onSuccess: setResultado, onError: (error) => formulario.marcarError(error) });
  };

  if (resultado) return <ResultadoFormulario resultado={resultado} tipo={tipo} volver={volver} onCambiar={() => setResultado(null)} />;

  return (
    <Card component="form" onSubmit={enviar} noValidate sx={{ p: 2 }}>
      <Stack spacing={2}>
        {envio.isError && !esErrorDeCampo(envio.error, campos) && (
          <Alert severity="error" role="alert">
            {mensajeDeError(envio.error, t)}
          </Alert>
        )}
        <CamposFormulario campos={campos} formulario={formulario} deshabilitado={envio.isPending} />
        <Stack direction={{ xs: 'column-reverse', sm: 'row' }} spacing={1.5} alignItems={{ sm: 'center' }}>
          {formulario.falta && <FaltaParaEnviar>{formulario.falta}</FaltaParaEnviar>}
          <Button type="submit" variant="contained" size="large" disabled={!formulario.completo || envio.isPending} sx={{ ml: { sm: 'auto' } }}>
            {t('formulario.enviar')}
          </Button>
        </Stack>
      </Stack>
    </Card>
  );
}

/** Enviado (versión nueva) o idéntico a lo que ya había, con el regreso a la revisión. */
function ResultadoFormulario({ resultado, tipo, volver, onCambiar }: { resultado: ConfirmacionCargaDto; tipo: string; volver: string | null; onCambiar: () => void }) {
  const { t } = useTranslation();
  const acciones = (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.5 }}>
      {volver && (
        <Button component={RouterLink} to={volver} variant="contained">
          {t('subir.volverRevision')}
        </Button>
      )}
      <Button component={RouterLink} to="/boveda" variant={volver ? 'outlined' : 'contained'}>
        {t('subir.irBoveda')}
      </Button>
    </Stack>
  );
  if (resultado.duplicado) {
    return (
      <Alert severity="info">
        <AlertTitle>{t('formulario.duplicadoTitulo')}</AlertTitle>
        {t('formulario.duplicado', { n: String(resultado.version.n) })}
        <Box>
          <Button variant="text" onClick={onCambiar} sx={{ mt: 1, px: 0 }}>
            {t('formulario.cambiar')}
          </Button>
        </Box>
        {acciones}
      </Alert>
    );
  }
  return (
    <Alert severity="success">
      <AlertTitle>{t('formulario.enviado', { tipo, n: String(resultado.version.n) })}</AlertTitle>
      {resultado.compartidos.length === 0 && t('subir.listaSinCompartir')}
      {acciones}
    </Alert>
  );
}
