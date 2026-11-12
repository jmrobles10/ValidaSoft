import { Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, FormControlLabel, FormHelperText, Stack, Typography, useMediaQuery, useTheme } from '@mui/material';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { DeclaracionPep } from '../tipos';
import { declaracionInicial, declaracionParaEntregar, type DatosBasicos } from './declaracion';
import { SeccionBasicos } from './SeccionBasicos';
import { SeccionCondicion } from './SeccionCondicion';
import { SeccionFirma } from './SeccionFirma';
import { SeccionVinculo } from './SeccionVinculo';
import { hayErrores, hoyIso, validarDeclaracionPep, type CodigoError } from './validarDeclaracionPep';

interface Props {
  basicos: DatosBasicos;
  /** La declaración guardada (o la inactiva de una respuesta anterior): el diálogo abre precargado. */
  previa: DeclaracionPep | null | undefined;
  /** Error del servidor de una ruta de la declaración (relativa: `cargo`, `familiares[0].numero`). */
  mensajeServidor?: (ruta: string) => string | null;
  onConfirmar: (declaracion: DeclaracionPep) => void;
  /** Salió sin confirmar (ya aceptó perder lo escrito). */
  onSalir: () => void;
}

type Salida = 'incompleta' | 'descartar';

/**
 * La declaración jurada PEP (HU 49093): cuatro secciones, el juramento y los botones fijos al pie (en el celular, a pantalla
 * completa). Confirmar valida con las reglas del servidor y entrega la declaración al formulario; nada se guarda hasta que
 * se guarda la sección. Salir sin confirmar pregunta antes.
 */
export function DialogoDeclaracionPep({ basicos, previa, mensajeServidor, onConfirmar, onSalir }: Props) {
  const { t } = useTranslation();
  const tema = useTheme();
  const pantallaCompleta = useMediaQuery(tema.breakpoints.down('sm'));
  const hoy = useMemo(() => hoyIso(), []);
  const [d, setD] = useState<DeclaracionPep>(() => declaracionInicial(basicos, previa));
  const [intento, setIntento] = useState(0);
  const [sucio, setSucio] = useState(false);
  const [saliendo, setSaliendo] = useState<Salida | null>(null);
  const contenido = useRef<HTMLDivElement>(null);
  const errores = validarDeclaracionPep(d, basicos, hoy);

  // Tras un intento fallido el foco va al primer campo marcado (el diálogo puede ser largo).
  useEffect(() => {
    if (intento > 0) contenido.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
  }, [intento]);

  const cambiar = (cambio: Partial<DeclaracionPep>) => {
    setD((actual) => ({ ...actual, ...cambio }));
    setSucio(true);
  };

  const textoDe = (codigo: CodigoError) => t(`ficha.pep.errores.${codigo}`);
  // Los errores locales solo se ven después del primer intento de confirmar; los del servidor, siempre.
  const mensaje = (ruta: string): string | null => {
    const local = errores[ruta];
    if (intento > 0 && local) return textoDe(local);
    return mensajeServidor?.(ruta) ?? null;
  };

  const confirmar = () => {
    if (hayErrores(errores)) {
      setIntento((n) => n + 1);
      return;
    }
    onConfirmar(declaracionParaEntregar(d, basicos));
  };

  const pedirSalida = () => {
    if (!previa) setSaliendo('incompleta');
    else if (sucio) setSaliendo('descartar');
    else onSalir();
  };

  const seccion = { d, cambiar, mensaje, basicos, hoy };
  const textosSalida = saliendo ? { titulo: t(`ficha.pep.salir.${saliendo}.titulo`), mensaje: t(`ficha.pep.salir.${saliendo}.mensaje`), seguir: t(`ficha.pep.salir.${saliendo}.seguir`), salir: t(`ficha.pep.salir.${saliendo}.salir`) } : null;

  return (
    <>
      <Dialog open onClose={pedirSalida} fullWidth maxWidth="md" fullScreen={pantallaCompleta} aria-labelledby="titulo-declaracion-pep">
        <DialogTitle id="titulo-declaracion-pep" sx={{ pb: 0.5 }}>
          {t('ficha.pep.titulo')}
        </DialogTitle>
        <DialogContent ref={contenido}>
          <Stack spacing={3} sx={{ pt: 1 }}>
            <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('ficha.pep.subtitulo')}</Typography>
            {intento > 0 && hayErrores(errores) && <Alert severity="error">{t('ficha.pep.revisar')}</Alert>}
            <SeccionBasicos {...seccion} />
            <SeccionCondicion {...seccion} />
            <SeccionVinculo {...seccion} />
            <SeccionFirma {...seccion} />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ flexDirection: 'column', alignItems: 'stretch', gap: 1, px: { xs: 2, sm: 3 }, py: 1.5, borderTop: 1, borderColor: 'divider', '& > :not(style) ~ :not(style)': { ml: 0 } }}>
          <Box>
            <FormControlLabel
              control={<Checkbox checked={d.acepta_juramento === true} onChange={(e) => cambiar({ acepta_juramento: e.target.checked })} />}
              label={t('ficha.pep.juramento.texto')}
              slotProps={{ typography: { sx: { fontSize: { xs: 13, sm: 14 }, lineHeight: 1.4 } } }}
              sx={{ alignItems: 'flex-start', m: 0, '& .MuiCheckbox-root': { mt: -0.5 } }}
            />
            {mensaje('acepta_juramento') && (
              <FormHelperText error role="alert" sx={{ mx: 0 }}>
                {mensaje('acepta_juramento')}
              </FormHelperText>
            )}
            <Typography sx={{ display: { xs: 'none', sm: 'block' }, color: 'text.secondary', fontSize: 13, mt: 0.5 }}>{t('ficha.pep.juramento.aviso')}</Typography>
          </Box>
          <Stack direction="row" spacing={1} useFlexGap sx={{ justifyContent: 'flex-end', '& .MuiButton-root': { ml: '0 !important', flex: { xs: 1, sm: 'none' } } }}>
            <Button variant="text" onClick={pedirSalida} sx={{ color: 'text.primary' }}>
              {t('ficha.pep.cerrar')}
            </Button>
            <Button variant="contained" onClick={confirmar}>
              {t('ficha.pep.confirmar')}
            </Button>
          </Stack>
        </DialogActions>
      </Dialog>
      <Dialog open={saliendo !== null} onClose={() => setSaliendo(null)} aria-labelledby="titulo-salir-declaracion-pep">
        <DialogTitle id="titulo-salir-declaracion-pep">{textosSalida?.titulo}</DialogTitle>
        <DialogContent>
          <DialogContentText>{textosSalida?.mensaje}</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button color="inherit" onClick={onSalir}>
            {textosSalida?.salir}
          </Button>
          <Button variant="contained" onClick={() => setSaliendo(null)}>
            {textosSalida?.seguir}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
