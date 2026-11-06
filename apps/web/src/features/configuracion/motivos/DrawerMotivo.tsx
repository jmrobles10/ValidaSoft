import { Alert, Box, Button, Drawer, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  actualizarMotivo,
  AplicaMotivo,
  CategoriaMotivo,
  crearMotivo,
  type MotivoTipificadoDto,
} from '@validasoft/api-client';
import { X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';

/** Mismo patrón que `Motivo.Crear` del dominio. */
const PATRON_CODIGO = /^[a-z][a-z0-9]*(-[a-z0-9]+)*$/;
const MAXIMO_TEXTO = 500;

/** Crear un motivo propio o editar uno existente (el código no cambia; los de la plataforma no se editan). */
export function DrawerMotivo({ empresaId, motivo, onCerrar }: { empresaId: string; motivo: MotivoTipificadoDto | null; onCerrar: () => void }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [codigo, setCodigo] = useState(motivo?.codigo ?? '');
  const [nombre, setNombre] = useState(motivo?.nombre ?? '');
  const [categoria, setCategoria] = useState<CategoriaMotivo>(motivo?.categoria ?? 'Documento');
  const [aplicaA, setAplicaA] = useState<AplicaMotivo>(motivo?.aplicaA ?? 'Observacion');
  const [texto, setTexto] = useState(motivo?.textoAlTercero ?? '');
  const [activo, setActivo] = useState(motivo?.activo ?? true);
  const [intentado, setIntentado] = useState(false);

  const mutacion = useMutation({
    mutationFn: () =>
      motivo
        ? actualizarMotivo(motivo.id, { nombre: nombre.trim(), categoria, aplicaA, textoAlTercero: texto.trim(), activo }, { empresa: empresaId })
        : crearMotivo({ codigo: codigo.trim().toLowerCase(), nombre: nombre.trim(), categoria, aplicaA, textoAlTercero: texto.trim() }, { empresa: empresaId }),
    onSuccess: async () => {
      await invalidarRuta(cliente, '/api/v1/motivos');
      mostrar(motivo ? t('motivos.guardado') : t('motivos.creado'));
      onCerrar();
    },
  });

  const codigoInvalido = !motivo && (!PATRON_CODIGO.test(codigo.trim().toLowerCase()) || codigo.trim().length > 60);
  const faltaNombre = !nombre.trim();
  const faltaTexto = !texto.trim();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (codigoInvalido || faltaNombre || faltaTexto) return;
    mutacion.mutate();
  };

  const titulo = motivo ? t('motivos.editarTitulo', { nombre: motivo.nombre }) : t('motivos.nuevo');

  return (
    <Drawer anchor="right" open onClose={mutacion.isPending ? undefined : onCerrar} slotProps={{ paper: { sx: { width: 460, maxWidth: '100%' } } }} aria-labelledby="titulo-motivo">
      <Box component="form" onSubmit={enviar} noValidate sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Typography id="titulo-motivo" variant="h4" component="h2">
            {titulo}
          </Typography>
          <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar} disabled={mutacion.isPending}>
            <X size={18} strokeWidth={1.75} />
          </IconButton>
        </Stack>
        <Stack spacing={2} sx={{ p: 2.5, flex: 1, overflowY: 'auto' }}>
          {mutacion.isError && <Alert severity="error">{mensajeDeError(mutacion.error, t)}</Alert>}
          <TextField
            label={t('motivos.codigo')}
            value={codigo}
            onChange={(e) => setCodigo(e.target.value)}
            disabled={Boolean(motivo)}
            required={!motivo}
            error={intentado && codigoInvalido}
            helperText={intentado && codigoInvalido ? t('motivos.codigoInvalido') : t('motivos.codigoAyuda')}
            slotProps={{ htmlInput: { spellCheck: false, autoCapitalize: 'none' } }}
          />
          <TextField label={t('motivos.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} required error={intentado && faltaNombre} helperText={intentado && faltaNombre ? t('motivos.requerido') : undefined} />
          <Stack direction="row" spacing={1.5}>
            <TextField select fullWidth label={t('motivos.categoria')} value={categoria} onChange={(e) => setCategoria(e.target.value as CategoriaMotivo)}>
              {Object.values(CategoriaMotivo).map((c) => (
                <MenuItem key={c} value={c}>
                  {t(`motivos.categorias.${c}`)}
                </MenuItem>
              ))}
            </TextField>
            <TextField select fullWidth label={t('motivos.aplicaA')} value={aplicaA} onChange={(e) => setAplicaA(e.target.value as AplicaMotivo)}>
              {Object.values(AplicaMotivo).map((a) => (
                <MenuItem key={a} value={a}>
                  {t(`motivos.aplica.${a}`)}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          <TextField
            label={t('motivos.textoAlTercero')}
            value={texto}
            onChange={(e) => setTexto(e.target.value.slice(0, MAXIMO_TEXTO))}
            required
            multiline
            minRows={3}
            error={intentado && faltaTexto}
            helperText={intentado && faltaTexto ? t('motivos.requerido') : t('motivos.textoAyuda')}
          />
          {motivo && <FormControlLabel control={<Switch checked={activo} onChange={(e) => setActivo(e.target.checked)} />} label={t('motivos.disponible')} />}
        </Stack>
        <Stack direction="row" spacing={1} justifyContent="flex-end" sx={{ px: 2.5, py: 1.5, borderTop: 1, borderColor: 'divider' }}>
          <Button variant="text" onClick={onCerrar} disabled={mutacion.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={mutacion.isPending} startIcon={mutacion.isPending ? <IconoCarga /> : undefined}>
            {motivo ? t('comun.guardar') : t('motivos.crear')}
          </Button>
        </Stack>
      </Box>
    </Drawer>
  );
}
