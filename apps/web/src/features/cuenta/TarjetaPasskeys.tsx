import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Skeleton, Stack, TextField, Tooltip, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  eliminarMiPasskey,
  getListarMisPasskeysQueryOptions,
  obtenerOpcionesRegistroPasskey,
  registrarMiPasskey,
  renombrarMiPasskey,
  type PasskeyDto,
} from '@validasoft/api-client';
import { enfocarNodo, EstadoVacio } from '@validasoft/ui';
import { KeyRound, Pencil, Plus, Trash2 } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { crearPasskey, nombreSugeridoDePasskey } from '../../ingreso/passkeys';
import { mensajeDeIngreso } from '../../ingreso/mensajes';
import { DialogoConfirmacion, ErrorRecuperable, IconoCarga, Tarjeta } from '../../shared/componentes';
import { invalidarRuta } from '../../shared/consultas';
import { useAvisos } from '../../shared/useAvisos';
import { FilaDePasskey, ListaDeFilas } from './FilasDeAcceso';

const RUTA = '/api/v1/mi-cuenta/passkeys';

/** Pide un nombre (para registrar una passkey nueva o cambiar el de una existente). */
function DialogoNombre({
  titulo,
  ayuda,
  confirmar,
  inicial,
  ocupado,
  error,
  onConfirmar,
  onCerrar,
}: {
  titulo: string;
  ayuda?: string;
  confirmar: string;
  inicial: string;
  ocupado: boolean;
  error: unknown;
  onConfirmar: (nombre: string) => void;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  const [nombre, setNombre] = useState(inicial);
  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!ocupado) onConfirmar(nombre.trim());
  };
  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 440, maxWidth: 'calc(100% - 32px)' }, component: 'form', onSubmit: enviar } }} aria-labelledby="titulo-nombre-passkey">
      <DialogTitle id="titulo-nombre-passkey">{titulo}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 0.5 }}>
          {ayuda && <Typography sx={{ color: 'text.secondary' }}>{ayuda}</Typography>}
          {error !== null && <Alert severity="error">{mensajeDeIngreso(error, t)}</Alert>}
          <TextField label={t('ingreso.registrarNombre')} helperText={t('ingreso.registrarNombreAyuda')} value={nombre} onChange={(e) => setNombre(e.target.value)} inputRef={enfocarNodo} fullWidth slotProps={{ htmlInput: { maxLength: 60 } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button type="submit" variant="contained" disabled={ocupado} startIcon={ocupado ? <IconoCarga /> : undefined}>
          {confirmar}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

type Dialogo = { tipo: 'nueva' } | { tipo: 'renombrar'; passkey: PasskeyDto } | { tipo: 'eliminar'; passkey: PasskeyDto };

/** Mis passkeys (ING-3): ver, agregar, renombrar y eliminar. Si la cuenta las exige, no deja eliminar la última. */
export function TarjetaPasskeys({ exigePasskey }: { exigePasskey: boolean }) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = useQuery(getListarMisPasskeysQueryOptions({ request: { empresa: null } }));
  const [dialogo, setDialogo] = useState<Dialogo | null>(null);

  const alTerminar = (mensaje: string) => {
    mostrar(mensaje);
    setDialogo(null);
    return invalidarRuta(cliente, RUTA);
  };

  const agregar = useMutation({
    mutationFn: async (nombre: string) => {
      const { desafio, opciones } = await obtenerOpcionesRegistroPasskey({ empresa: null });
      return registrarMiPasskey({ desafio, respuesta: await crearPasskey(opciones), nombre: nombre || null }, { empresa: null });
    },
    onSuccess: () => alTerminar(t('cuenta.passkeys.registrada')),
  });
  const renombrar = useMutation({
    mutationFn: ({ id, nombre }: { id: string; nombre: string }) => renombrarMiPasskey(id, { nombre }, { empresa: null }),
    onSuccess: () => alTerminar(t('cuenta.passkeys.renombrada')),
  });
  const eliminar = useMutation({
    mutationFn: (id: string) => eliminarMiPasskey(id, { empresa: null }),
    onSuccess: () => alTerminar(t('cuenta.passkeys.eliminada')),
    onError: (error) => {
      mostrar(mensajeDeIngreso(error, t), 'error');
      setDialogo(null);
    },
  });

  const passkeys = consulta.data ?? [];
  const unica = passkeys.length <= 1;

  return (
    <>
      <Tarjeta
        titulo={t('cuenta.passkeys.titulo')}
        acciones={
          <Button
            size="small"
            variant="outlined"
            startIcon={<Plus size={14} aria-hidden />}
            onClick={() => {
              agregar.reset();
              setDialogo({ tipo: 'nueva' });
            }}
          >
            {t('cuenta.passkeys.agregar')}
          </Button>
        }
      >
        <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
          {t('cuenta.passkeys.ayuda')} {exigePasskey ? t('cuenta.exigePasskey') : t('cuenta.noExigePasskey')}
        </Typography>
        {consulta.isError && <ErrorRecuperable mensaje={t('cuenta.passkeys.errorCarga')} onReintentar={() => void consulta.refetch()} />}
        {consulta.isPending && (
          <Stack spacing={1.5} aria-busy aria-label={t('comun.cargando')} role="status">
            {[0, 1].map((i) => (
              <Skeleton key={i} variant="rounded" height={44} />
            ))}
          </Stack>
        )}
        {consulta.data && passkeys.length === 0 && (
          <EstadoVacio compacto icono={KeyRound} titulo={t('cuenta.passkeys.vacio')} texto={t('cuenta.passkeys.vacioAyuda')} />
        )}
        {passkeys.length > 0 && (
          <ListaDeFilas etiqueta={t('cuenta.passkeys.titulo')}>
            {passkeys.map((p) => (
              <FilaDePasskey
                key={p.id}
                passkey={p}
                acciones={
                  <>
                    <Tooltip title={t('cuenta.passkeys.renombrar')}>
                      <IconButton aria-label={`${t('cuenta.passkeys.renombrar')}: ${p.nombre}`} size="small" onClick={() => { renombrar.reset(); setDialogo({ tipo: 'renombrar', passkey: p }); }}>
                        <Pencil size={14} aria-hidden />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={exigePasskey && unica ? t('cuenta.passkeys.eliminarUnica') : t('cuenta.passkeys.eliminar')}>
                      <span>
                        <IconButton
                          aria-label={`${t('cuenta.passkeys.eliminar')}: ${p.nombre}`}
                          size="small"
                          disabled={exigePasskey && unica}
                          onClick={() => setDialogo({ tipo: 'eliminar', passkey: p })}
                        >
                          <Trash2 size={14} aria-hidden />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </>
                }
              />
            ))}
          </ListaDeFilas>
        )}
      </Tarjeta>

      {dialogo?.tipo === 'nueva' && (
        <DialogoNombre
          titulo={t('cuenta.passkeys.nuevaTitulo')}
          ayuda={t('cuenta.passkeys.nuevaAyuda')}
          confirmar={t('ingreso.registrar')}
          inicial={nombreSugeridoDePasskey(navigator.userAgent)}
          ocupado={agregar.isPending}
          error={agregar.error}
          onConfirmar={(nombre) => agregar.mutate(nombre)}
          onCerrar={() => setDialogo(null)}
        />
      )}
      {dialogo?.tipo === 'renombrar' && (
        <DialogoNombre
          titulo={t('cuenta.passkeys.renombrarTitulo')}
          confirmar={t('comun.guardar')}
          inicial={dialogo.passkey.nombre}
          ocupado={renombrar.isPending}
          error={renombrar.error}
          onConfirmar={(nombre) => renombrar.mutate({ id: dialogo.passkey.id, nombre })}
          onCerrar={() => setDialogo(null)}
        />
      )}
      {dialogo?.tipo === 'eliminar' && (
        <DialogoConfirmacion
          titulo={t('cuenta.passkeys.eliminarTitulo', { nombre: dialogo.passkey.nombre })}
          mensaje={t('cuenta.passkeys.eliminarTexto')}
          confirmar={t('cuenta.passkeys.eliminar')}
          icono={<Trash2 size={16} aria-hidden />}
          ocupado={eliminar.isPending}
          onConfirmar={() => eliminar.mutate(dialogo.passkey.id)}
          onCerrar={() => setDialogo(null)}
        />
      )}
    </>
  );
}
