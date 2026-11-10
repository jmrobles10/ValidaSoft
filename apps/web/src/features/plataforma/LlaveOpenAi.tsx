import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material';
import type { PruebaDeLlaveIaDto } from '@validasoft/api-client';
import { AlineadoConCampo } from '@validasoft/ui';
import { FlaskConical, KeyRound, RotateCcw, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';
import { llaveEnmascarada, llaveValida, type CambioDeLlave } from './modelo';

export interface LlaveOpenAiProps {
  configurada: boolean;
  ultimosCuatro: string | null;
  cambio: CambioDeLlave;
  onCambio: (cambio: CambioDeLlave) => void;
  /** Probar la llave escrita (si hay una nueva válida) o la guardada. */
  onProbar: (llave: string | null) => void;
  probando: boolean;
  prueba: PruebaDeLlaveIaDto | null;
  errorPrueba: string | null;
  errorLlave: string | null;
}

/**
 * La API key de OpenAI de la empresa es de solo escritura (PT-6): se ve «Configurada · ••••WXYZ» y se puede reemplazar o
 * quitar; el campo nuevo es de tipo contraseña y nunca se rellena con la guardada. «Probar llave» consulta al proveedor solo
 * cuando la persona lo pide.
 */
export function LlaveOpenAi({ configurada, ultimosCuatro, cambio, onCambio, onProbar, probando, prueba, errorPrueba, errorLlave }: LlaveOpenAiProps) {
  const { t } = useTranslation();
  const nueva = cambio.tipo === 'reemplazar' ? cambio.valor : '';
  const escribiendo = cambio.tipo === 'reemplazar' || !configurada;
  const puedeProbarNueva = cambio.tipo === 'reemplazar' && llaveValida(cambio.valor);
  const puedeProbar = puedeProbarNueva || (configurada && cambio.tipo === 'conservar');

  const severidad = prueba?.resultado === 'valida' ? 'success' : prueba?.resultado === 'invalida' ? 'error' : 'warning';

  return (
    <Stack spacing={1.5}>
      {configurada && (
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 14, textDecoration: cambio.tipo === 'quitar' ? 'line-through' : undefined }}>
            <KeyRound size={16} strokeWidth={1.75} aria-hidden />
            {t('plataforma.planIa.llaveConfigurada', { llave: llaveEnmascarada(ultimosCuatro) })}
          </Box>
          <Box sx={{ flex: 1 }} />
          {cambio.tipo === 'conservar' ? (
            <>
              <Button size="small" variant="text" onClick={() => onCambio({ tipo: 'reemplazar', valor: '' })}>
                {t('plataforma.planIa.reemplazar')}
              </Button>
              <Button size="small" variant="text" startIcon={<Trash2 size={14} aria-hidden />} onClick={() => onCambio({ tipo: 'quitar' })} sx={{ color: 'estado.error.texto' }}>
                {t('plataforma.planIa.quitar')}
              </Button>
            </>
          ) : (
            <Button size="small" variant="text" startIcon={<RotateCcw size={14} aria-hidden />} onClick={() => onCambio({ tipo: 'conservar' })}>
              {t('plataforma.planIa.deshacer')}
            </Button>
          )}
        </Stack>
      )}
      {!configurada && cambio.tipo !== 'reemplazar' && (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('plataforma.planIa.llaveNoConfigurada')}
        </Typography>
      )}
      {cambio.tipo === 'quitar' && <Alert severity="warning">{t('plataforma.planIa.seQuitara')}</Alert>}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
        {escribiendo && cambio.tipo !== 'quitar' && (
          <TextField
            type="password"
            label={t('plataforma.planIa.llaveNueva')}
            value={nueva}
            onChange={(e) => onCambio(e.target.value ? { tipo: 'reemplazar', valor: e.target.value } : configurada ? { tipo: 'reemplazar', valor: '' } : { tipo: 'conservar' })}
            error={errorLlave !== null}
            helperText={errorLlave ?? t('plataforma.planIa.llaveNuevaAyuda')}
            slotProps={{ htmlInput: { autoComplete: 'new-password', spellCheck: false } }}
            sx={{ flex: 1, minWidth: 0, '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
          />
        )}
        <AlineadoConCampo conEtiqueta={escribiendo && cambio.tipo !== 'quitar'}>
          <Button
            variant="outlined"
            disabled={!puedeProbar || probando}
            startIcon={probando ? <IconoCarga /> : <FlaskConical size={16} aria-hidden />}
            onClick={() => onProbar(puedeProbarNueva ? nueva.trim() : null)}
          >
            {probando ? t('plataforma.planIa.probando') : puedeProbarNueva ? t('plataforma.planIa.probarNueva') : t('plataforma.planIa.probar')}
          </Button>
        </AlineadoConCampo>
      </Stack>
      <Typography variant="caption" sx={{ color: 'text.secondary' }}>
        {t('plataforma.planIa.probarAyuda')}
      </Typography>
      {errorPrueba && <Alert severity="error">{errorPrueba}</Alert>}
      {prueba && (
        <Alert severity={severidad} role="status">
          {t(`plataforma.planIa.resultado.${prueba.resultado as 'valida' | 'invalida' | 'limite' | 'sin-llave' | 'error'}`, {
            ultimos: prueba.ultimosCuatro ?? '',
            mensaje: prueba.mensaje,
          })}
        </Alert>
      )}
    </Stack>
  );
}
