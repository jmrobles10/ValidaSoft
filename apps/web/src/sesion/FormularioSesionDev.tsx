import { Alert, Button, Card, CardContent, Link, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { abrirSesionDelegada, canjearSesion, cargarSemilla, type SemillaDev } from '../dev/firmarAsercion';
import { mensajeDeError } from '../shared/errores';
import { useSesion } from './sesionStore';

/**
 * Solo en `vite dev` y fuera de un iframe: abre una sesión como lo haría el sistema integrado, para trabajar en la
 * UI sin un contenedor. Los campos se prellenan con `pnpm semilla:dev`.
 */
export function FormularioSesionDev({ vencida = false }: { vencida?: boolean }) {
  const { t } = useTranslation();
  const establecer = useSesion((s) => s.establecer);
  const [semilla, setSemilla] = useState<SemillaDev | null>(null);
  const [apiKey, setApiKey] = useState('');
  const [sistema, setSistema] = useState('');
  const [clavePrivadaPem, setClavePrivadaPem] = useState('');
  const [sub, setSub] = useState('');
  const [nombre, setNombre] = useState('');
  const [correo, setCorreo] = useState('');
  const [asercion, setAsercion] = useState('');
  const [error, setError] = useState<unknown>(null);
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    void cargarSemilla().then((datos) => {
      if (!datos) return;
      setSemilla(datos);
      setApiKey(datos.apiKey);
      setSistema(datos.sistema.codigo);
      setClavePrivadaPem(datos.clavePrivadaPem);
      const primero = datos.usuarios[0];
      if (primero) {
        setSub(primero.sub);
        setNombre(primero.nombre);
        setCorreo(primero.correo);
      }
    });
  }, []);

  const elegirPersona = (valor: string) => {
    const persona = semilla?.usuarios.find((u) => u.sub === valor);
    if (!persona) return;
    setSub(persona.sub);
    setNombre(persona.nombre);
    setCorreo(persona.correo);
  };

  const enviar = async (evento: FormEvent) => {
    evento.preventDefault();
    setEnviando(true);
    setError(null);
    try {
      const sesion = asercion.trim()
        ? await canjearSesion(apiKey.trim(), asercion.trim())
        : await abrirSesionDelegada({ apiKey: apiKey.trim(), sistema: sistema.trim(), clavePrivadaPem }, { sub, nombre, correo });
      establecer(sesion.token);
    } catch (e) {
      setError(e);
    } finally {
      setEnviando(false);
    }
  };

  return (
    <Card sx={{ maxWidth: 640, mx: 'auto', my: 4, borderRadius: (th) => `${String(th.radio.md)}px` }}>
      <CardContent component="form" onSubmit={(e) => void enviar(e)}>
        <Stack spacing={2}>
          <Typography variant="h2" component="h1">
            {t('sesion.dev.titulo')}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t('sesion.dev.ayuda')}
          </Typography>
          {vencida && <Alert severity="warning">{t('sesion.vencida')}</Alert>}
          <Alert severity={semilla ? 'success' : 'info'}>{semilla ? t('sesion.dev.semilla') : t('sesion.dev.sinSemilla')}</Alert>
          {error !== null && <Alert severity="error">{mensajeDeError(error, t)}</Alert>}
          {semilla && (
            <TextField select label={t('sesion.dev.usuario')} value={sub} onChange={(e) => elegirPersona(e.target.value)}>
              {semilla.usuarios.map((u) => (
                <MenuItem key={u.sub} value={u.sub}>
                  {u.nombre} — {u.descripcion}
                </MenuItem>
              ))}
            </TextField>
          )}
          <TextField required label={t('sesion.dev.apiKey')} value={apiKey} onChange={(e) => setApiKey(e.target.value)} />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField label={t('sesion.dev.sistema')} value={sistema} onChange={(e) => setSistema(e.target.value)} />
            <TextField label={t('sesion.dev.sub')} value={sub} onChange={(e) => setSub(e.target.value)} />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField fullWidth label={t('sesion.dev.nombre')} value={nombre} onChange={(e) => setNombre(e.target.value)} />
            <TextField fullWidth label={t('sesion.dev.correo')} value={correo} onChange={(e) => setCorreo(e.target.value)} />
          </Stack>
          <TextField
            label={t('sesion.dev.clavePrivada')}
            value={clavePrivadaPem}
            onChange={(e) => setClavePrivadaPem(e.target.value)}
            multiline
            minRows={3}
            maxRows={6}
            slotProps={{ htmlInput: { style: { fontFamily: 'monospace', fontSize: 11 } } }}
          />
          <TextField label={t('sesion.dev.asercion')} value={asercion} onChange={(e) => setAsercion(e.target.value)} />
          <Stack direction="row" spacing={2} alignItems="center">
            <Button type="submit" variant="contained" disabled={enviando || !apiKey}>
              {t('sesion.dev.entrar')}
            </Button>
            <Link href="embed-demo.html">{t('sesion.dev.demo')}</Link>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}
