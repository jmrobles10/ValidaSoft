import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { agregarDominioSso, quitarDominioSso, verificarDominioSso, type DominioDeSsoDto } from '@validasoft/api-client';
import { Chip } from '@mui/material';
import { BadgeCheck, Clock, Copy, Plus, Trash2 } from 'lucide-react';
import { useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga, Tarjeta } from '../../../../shared/componentes';
import { invalidarRuta } from '../../../../shared/consultas';
import { mensajeDeError } from '../../../../shared/errores';
import { useAvisos } from '../../../../shared/useAvisos';

const RUTA = '/api/v1/configuracion/sso';
const SIN_EMPRESA = { empresa: null } as const;

/** Un dominio: verificado o por verificar (con el texto que hay que publicar y el botón que lo comprueba). */
function FilaDominio({ dominio }: { dominio: DominioDeSsoDto }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const alTerminar = () => invalidarRuta(cliente, RUTA);
  const verificar = useMutation({
    mutationFn: () => verificarDominioSso(dominio.dominio, SIN_EMPRESA),
    onSuccess: () => mostrar(t('sso.dominios.verificado', { dominio: dominio.dominio })),
    onSettled: alTerminar,
  });
  const quitar = useMutation({
    mutationFn: () => quitarDominioSso(dominio.dominio, SIN_EMPRESA),
    onSuccess: () => mostrar(t('sso.dominios.quitado', { dominio: dominio.dominio })),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: alTerminar,
  });
  const copiar = () => {
    if (dominio.token) void navigator.clipboard.writeText(dominio.token).then(() => mostrar(t('sso.dominios.copiado')));
  };

  return (
    <Box sx={{ py: 1, borderTop: 1, borderColor: 'divider', '&:first-of-type': { borderTop: 0 } }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 1 }}>
        <Typography sx={{ fontWeight: 600, flex: '1 1 160px', minWidth: 0, overflowWrap: 'anywhere' }}>
          @{dominio.dominio}
        </Typography>
        {dominio.verificado ? (
          <Chip size="small" variant="outlined" icon={<BadgeCheck size={14} aria-hidden />} label={t('sso.dominios.estadoVerificado')} />
        ) : (
          <Chip size="small" variant="outlined" icon={<Clock size={14} aria-hidden />} label={t('sso.dominios.estadoPendiente')} />
        )}
        {!dominio.verificado && (
          <Button size="small" variant="outlined" onClick={() => verificar.mutate()} disabled={verificar.isPending} startIcon={verificar.isPending ? <IconoCarga /> : undefined}>
            {t('sso.dominios.verificar')}
          </Button>
        )}
        <Button size="small" variant="text" aria-label={t('sso.dominios.quitarA', { dominio: dominio.dominio })} onClick={() => quitar.mutate()} disabled={quitar.isPending} sx={{ minWidth: 0, color: 'text.secondary' }}>
          <Trash2 size={16} aria-hidden />
        </Button>
      </Stack>
      {!dominio.verificado && (
        <Stack spacing={1} sx={{ mt: 1 }}>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('sso.dominios.instruccion', { ruta: `https://${dominio.dominio}/.well-known/validasoft-verificacion.txt` })}
          </Typography>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Box component="code" sx={{ flex: 1, minWidth: 0, px: 1, py: 0.5, borderRadius: (th) => `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2', fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 12, wordBreak: 'break-all' }}>
              {dominio.token}
            </Box>
            <Button size="small" variant="text" startIcon={<Copy size={14} aria-hidden />} onClick={copiar}>
              {t('sso.dominios.copiar')}
            </Button>
          </Stack>
          {verificar.isError && <Alert severity="warning">{mensajeDeError(verificar.error, t)}</Alert>}
        </Stack>
      )}
    </Box>
  );
}

/**
 * Dominios de correo del grupo (PI-2): hasta que se demuestra que son suyos —publicando un texto en su sitio—, no sirven para el SSO. Un dominio
 * verificado es de un solo grupo.
 */
export function DominiosSso({ dominios }: { dominios: readonly DominioDeSsoDto[] }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const [dominio, setDominio] = useState('');
  const agregar = useMutation({
    mutationFn: () => agregarDominioSso({ dominio: dominio.trim() }, SIN_EMPRESA),
    onSuccess: () => setDominio(''),
    onSettled: () => invalidarRuta(cliente, RUTA),
  });
  // No es un formulario: va dentro del de la página (que guarda la configuración) y un envío anidado guardaría o recargaría todo.
  const enviar = () => {
    if (dominio.trim().length > 0 && !agregar.isPending) agregar.mutate();
  };
  const alTeclear = (evento: KeyboardEvent) => {
    if (evento.key !== 'Enter') return;
    evento.preventDefault();
    enviar();
  };

  return (
    <Tarjeta titulo={t('sso.dominios.titulo')}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
        {t('sso.dominios.ayuda')}
      </Typography>
      {dominios.length === 0 ? (
        <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
          {t('sso.dominios.vacio')}
        </Typography>
      ) : (
        <Box sx={{ mb: 1.5 }}>
          {dominios.map((d) => (
            <FilaDominio key={d.dominio} dominio={d} />
          ))}
        </Box>
      )}
      {agregar.isError && (
        <Alert severity="error" sx={{ mb: 1.5 }}>
          {mensajeDeError(agregar.error, t)}
        </Alert>
      )}
      <Box>
        <Stack direction="row" spacing={1}>
          <TextField size="small" label={t('sso.dominios.campo')} placeholder="empresa.co" value={dominio} onChange={(e) => setDominio(e.target.value)} onKeyDown={alTeclear} sx={{ flex: 1, minWidth: 0 }} slotProps={{ htmlInput: { maxLength: 253 } }} />
          <Button type="button" variant="outlined" onClick={enviar} disabled={dominio.trim().length === 0 || agregar.isPending} startIcon={agregar.isPending ? <IconoCarga /> : <Plus size={16} aria-hidden />}>
            {t('sso.dominios.agregar')}
          </Button>
        </Stack>
      </Box>
    </Tarjeta>
  );
}
