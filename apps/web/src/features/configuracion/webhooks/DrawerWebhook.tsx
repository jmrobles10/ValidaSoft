import { Alert, Box, Button, Checkbox, Drawer, FormControlLabel, IconButton, Radio, RadioGroup, Stack, TextField, Typography } from '@mui/material';
import type { EmpresaDto, TipoEventoPublicoDto, WebhookDto } from '@validasoft/api-client';
import { FaltaParaEnviar } from '@validasoft/ui';
import { X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useGuardarWebhook } from './api';
import { agruparTipos, alternarTipo, borradorDeWebhook, borradorWebhookNuevo, esGrupoConocido, TODOS, validarUrl, validarWebhook, type BorradorWebhook } from './modelo';

/** Crear o editar una suscripción: URL, qué eventos y de qué empresas. Al crear, el padre muestra el secreto. */
export function DrawerWebhook({
  webhook,
  catalogo,
  empresas,
  onCerrar,
  onCreado,
}: {
  webhook: WebhookDto | null;
  catalogo: readonly TipoEventoPublicoDto[];
  empresas: readonly EmpresaDto[];
  onCerrar: () => void;
  onCreado: (creado: WebhookDto) => void;
}) {
  const { t } = useTranslation();
  const [b, setB] = useState<BorradorWebhook>(() => (webhook ? borradorDeWebhook(webhook) : borradorWebhookNuevo()));
  const [urlTocada, setUrlTocada] = useState(false);
  const guardar = useGuardarWebhook(webhook);
  const errores = validarWebhook(b);
  const errorUrl = urlTocada ? validarUrl(b.url) : null;
  const todosLosTipos = b.tipos.includes(TODOS);
  const todasLasEmpresas = b.empresas.includes(TODOS);
  const tipos = catalogo.map((c) => c.tipo);
  const primero = errores[0];

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (primero) return;
    guardar.mutate(b, { onSuccess: (resultado) => (webhook ? onCerrar() : onCreado(resultado)) });
  };

  return (
    <Drawer anchor="right" open onClose={guardar.isPending ? undefined : onCerrar} slotProps={{ paper: { sx: { width: 560, maxWidth: '100%' } } }} aria-labelledby="titulo-webhook">
      <Box component="form" onSubmit={enviar} noValidate sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Typography id="titulo-webhook" variant="h4" component="h2">
            {webhook ? t('webhooks.editar') : t('webhooks.nuevo')}
          </Typography>
          <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar} disabled={guardar.isPending}>
            <X size={18} strokeWidth={1.75} />
          </IconButton>
        </Stack>
        <Stack spacing={2.5} sx={{ p: 2.5, flex: 1, overflowY: 'auto' }}>
          {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
          <TextField
            required
            label={t('webhooks.campos.url')}
            placeholder="https://srm.cliente.co/validasoft/webhook"
            value={b.url}
            onChange={(e) => setB({ ...b, url: e.target.value })}
            onBlur={() => setUrlTocada(true)}
            error={errorUrl !== null}
            helperText={errorUrl ? t(`webhooks.errores.${errorUrl}`) : t('webhooks.campos.urlAyuda')}
            slotProps={{ htmlInput: { spellCheck: false, inputMode: 'url' } }}
            sx={{ '& input': { fontFamily: (th) => th.typography.mono.fontFamily } }}
          />
          <TextField label={t('webhooks.campos.descripcion')} value={b.descripcion} onChange={(e) => setB({ ...b, descripcion: e.target.value })} helperText={t('webhooks.campos.descripcionAyuda')} />

          <Box component="fieldset" sx={{ border: 0, m: 0, p: 0 }}>
            <Typography component="legend" sx={{ fontSize: 14, fontWeight: 600 }}>
              {t('webhooks.campos.eventos')}
            </Typography>
            <FormControlLabel
              control={<Checkbox checked={todosLosTipos} onChange={(e) => setB({ ...b, tipos: e.target.checked ? [TODOS] : [] })} />}
              label={t('webhooks.campos.todosLosEventos')}
            />
            <Stack spacing={1.25} sx={{ pl: 1, mt: 0.5 }}>
              {agruparTipos(catalogo).map(({ grupo, tipos: delGrupo }) => (
                <Box key={grupo}>
                  <Typography variant="overline" color="text.secondary">
                    {esGrupoConocido(grupo) ? t(`webhooks.grupos.${grupo}`) : grupo}
                  </Typography>
                  {delGrupo.map((tipo) => (
                    <FormControlLabel
                      key={tipo.tipo}
                      sx={{ display: 'flex', alignItems: 'flex-start', mx: 0, '& .MuiCheckbox-root': { mt: -0.5 } }}
                      control={<Checkbox size="small" checked={todosLosTipos || b.tipos.includes(tipo.tipo)} onChange={() => setB({ ...b, tipos: alternarTipo(b.tipos, tipo.tipo, tipos) })} />}
                      label={
                        <Box>
                          <Typography component="span" variant="mono" sx={{ fontSize: 13, display: 'block' }}>
                            {tipo.tipo}
                          </Typography>
                          <Typography component="span" variant="body2" color="text.secondary">
                            {tipo.descripcion}
                          </Typography>
                        </Box>
                      }
                    />
                  ))}
                </Box>
              ))}
            </Stack>
          </Box>

          <Box component="fieldset" sx={{ border: 0, m: 0, p: 0 }}>
            <Typography component="legend" sx={{ fontSize: 14, fontWeight: 600 }}>
              {t('webhooks.campos.empresas')}
            </Typography>
            <RadioGroup value={todasLasEmpresas ? 'todas' : 'algunas'} onChange={(e) => setB({ ...b, empresas: e.target.value === 'todas' ? [TODOS] : [] })}>
              <FormControlLabel value="todas" control={<Radio size="small" />} label={t('webhooks.campos.todasLasEmpresas')} />
              <FormControlLabel value="algunas" control={<Radio size="small" />} label={t('webhooks.campos.algunasEmpresas')} />
            </RadioGroup>
            {!todasLasEmpresas && (
              <Stack sx={{ pl: 3.5 }}>
                {empresas.map((e) => (
                  <FormControlLabel
                    key={e.id}
                    control={<Checkbox size="small" checked={b.empresas.includes(e.id)} onChange={() => setB({ ...b, empresas: b.empresas.includes(e.id) ? b.empresas.filter((x) => x !== e.id) : [...b.empresas, e.id] })} />}
                    label={e.nombre}
                  />
                ))}
              </Stack>
            )}
          </Box>
        </Stack>
        <Stack direction="row" spacing={1} alignItems="center" justifyContent="flex-end" sx={{ px: 2.5, py: 1.5, borderTop: 1, borderColor: 'divider' }}>
          {primero && <FaltaParaEnviar>{t(`webhooks.errores.${primero}`)}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending || primero !== undefined} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
            {webhook ? t('comun.guardar') : t('webhooks.crear')}
          </Button>
        </Stack>
      </Box>
    </Drawer>
  );
}
