import { Box, Button, FormHelperText, Stack, Tab, Tabs, Typography } from '@mui/material';
import { FileUp, Eraser, Pencil } from 'lucide-react';
import { useEffect, useRef, useState, type ChangeEvent, type PointerEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { conPrefijo, ErrorDeFirma, FIRMA_ALTO, FIRMA_ANCHO, normalizarArchivo, normalizarLienzo, type CodigoErrorFirma } from './normalizarFirma';

interface Props {
  /** Firma actual: PNG en base64 sin prefijo; `null` si no hay. */
  valor: string | null | undefined;
  onCambiar: (png: string | null) => void;
  /** Mensaje de la regla (firma obligatoria). */
  error?: string | null;
  deshabilitado?: boolean;
}

type Modo = 'dibujar' | 'subir';

/**
 * La firma de la declaración: se dibuja con el dedo o el mouse o se sube una imagen. En los dos casos se entrega un PNG de
 * 600 × 200 con fondo blanco (`normalizarFirma`). Con una firma ya guardada muestra su vista previa y deja cambiarla.
 */
export function FirmaPad({ valor, onCambiar, error, deshabilitado = false }: Props) {
  const { t } = useTranslation();
  const [modo, setModo] = useState<Modo>('dibujar');
  // Una firma que ya viene guardada se ve como vista previa (con «Cambiar firma»); lo que se dibuja o se sube aquí no la reemplaza mientras se firma.
  const [vista, setVista] = useState(Boolean(valor));
  const [errorLocal, setErrorLocal] = useState<CodigoErrorFirma | null>(null);
  const mostrarVista = vista && Boolean(valor);
  const mensaje = errorLocal ? t(`ficha.pep.firma.errores.${errorLocal}`) : (error ?? null);

  const alCambiar = (png: string | null) => {
    setErrorLocal(null);
    onCambiar(png);
  };
  const alFallar = (e: unknown) => setErrorLocal(e instanceof ErrorDeFirma ? e.codigo : 'lectura');

  return (
    <Stack spacing={1} role="group" aria-label={t('ficha.pep.firma.titulo')}>
      {mostrarVista ? (
        <Stack spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'flex-start' } }}>
          <Box
            component="img"
            src={conPrefijo(valor ?? '')}
            alt={t('ficha.pep.firma.vistaPrevia')}
            sx={(th) => ({ width: '100%', maxWidth: FIRMA_ANCHO, aspectRatio: '3 / 1', objectFit: 'contain', bgcolor: '#fff', border: 1, borderColor: 'divider', borderRadius: `${String(th.radio.md)}px` })}
          />
          <Button variant="outlined" startIcon={<Pencil size={16} aria-hidden />} disabled={deshabilitado} onClick={() => setVista(false)}>
            {t('ficha.pep.firma.cambiar')}
          </Button>
        </Stack>
      ) : (
        <>
          <Tabs value={modo} onChange={(_e, m: Modo) => setModo(m)} aria-label={t('ficha.pep.firma.como')} sx={{ minHeight: 36 }}>
            <Tab value="dibujar" label={t('ficha.pep.firma.dibujar')} id="firma-dibujar" aria-controls="firma-panel" disabled={deshabilitado} />
            <Tab value="subir" label={t('ficha.pep.firma.subir')} id="firma-subir" aria-controls="firma-panel" disabled={deshabilitado} />
          </Tabs>
          <Box id="firma-panel" role="tabpanel" aria-labelledby={modo === 'dibujar' ? 'firma-dibujar' : 'firma-subir'}>
            {modo === 'dibujar' ? (
              <Lienzo onCambiar={alCambiar} onError={alFallar} deshabilitado={deshabilitado} />
            ) : (
              <SubirImagen onCambiar={(png) => { alCambiar(png); setVista(true); }} onError={alFallar} deshabilitado={deshabilitado} />
            )}
          </Box>
        </>
      )}
      {mensaje && (
        <FormHelperText error role="alert" sx={{ mx: 0 }}>
          {mensaje}
        </FormHelperText>
      )}
    </Stack>
  );
}

interface PropsLienzo {
  onCambiar: (png: string | null) => void;
  onError: (error: unknown) => void;
  deshabilitado: boolean;
}

/** Lienzo de 3 : 1 con eventos de puntero (mouse, dedo o lápiz); al soltar entrega la firma normalizada. */
function Lienzo({ onCambiar, onError, deshabilitado }: PropsLienzo) {
  const { t } = useTranslation();
  const ref = useRef<HTMLCanvasElement>(null);
  const ultimo = useRef<{ x: number; y: number } | null>(null);
  const [conTrazos, setConTrazos] = useState(false);

  // Resolución por la densidad de píxeles de la pantalla; el tamaño en pantalla lo da el CSS.
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 3);
    el.width = FIRMA_ANCHO * dpr;
    el.height = FIRMA_ALTO * dpr;
    const ctx = el.getContext('2d');
    if (!ctx) return;
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, el.width, el.height);
  }, []);

  const punto = (e: PointerEvent<HTMLCanvasElement>) => {
    const el = e.currentTarget;
    const caja = el.getBoundingClientRect();
    return { x: ((e.clientX - caja.left) * el.width) / Math.max(caja.width, 1), y: ((e.clientY - caja.top) * el.height) / Math.max(caja.height, 1) };
  };

  const trazo = (e: PointerEvent<HTMLCanvasElement>, desde: { x: number; y: number }, hasta: { x: number; y: number }) => {
    const ctx = e.currentTarget.getContext('2d');
    if (!ctx) return;
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = (e.currentTarget.width / FIRMA_ANCHO) * 3;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(desde.x, desde.y);
    ctx.lineTo(hasta.x, hasta.y);
    ctx.stroke();
  };

  const abajo = (e: PointerEvent<HTMLCanvasElement>) => {
    if (deshabilitado) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    const p = punto(e);
    ultimo.current = p;
    trazo(e, p, p);
  };
  const mover = (e: PointerEvent<HTMLCanvasElement>) => {
    if (!ultimo.current) return;
    const p = punto(e);
    trazo(e, ultimo.current, p);
    ultimo.current = p;
  };
  const arriba = (e: PointerEvent<HTMLCanvasElement>) => {
    if (!ultimo.current) return;
    ultimo.current = null;
    try {
      const png = normalizarLienzo(e.currentTarget);
      setConTrazos(png !== null);
      onCambiar(png);
    } catch (error) {
      onError(error);
    }
  };

  const limpiar = () => {
    const el = ref.current;
    const ctx = el?.getContext('2d');
    if (el && ctx) {
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, el.width, el.height);
    }
    setConTrazos(false);
    onCambiar(null);
  };

  return (
    <Stack spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'flex-start' } }}>
      <Box
        component="canvas"
        ref={ref}
        role="img"
        aria-label={t('ficha.pep.firma.lienzo')}
        onPointerDown={abajo}
        onPointerMove={mover}
        onPointerUp={arriba}
        onPointerCancel={arriba}
        sx={(th) => ({
          width: '100%',
          maxWidth: FIRMA_ANCHO,
          aspectRatio: '3 / 1',
          touchAction: 'none',
          cursor: 'crosshair',
          bgcolor: '#fff',
          border: 1,
          borderColor: 'divider',
          borderRadius: `${String(th.radio.md)}px`,
        })}
      />
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Button variant="outlined" startIcon={<Eraser size={16} aria-hidden />} onClick={limpiar} disabled={deshabilitado || !conTrazos}>
          {t('ficha.pep.firma.limpiar')}
        </Button>
        <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('ficha.pep.firma.dibujaAyuda')}</Typography>
      </Stack>
    </Stack>
  );
}

interface PropsSubir {
  onCambiar: (png: string | null) => void;
  onError: (error: unknown) => void;
  deshabilitado: boolean;
}

/** Subir una imagen (PNG o JPG): el botón abre el selector de archivos, que queda oculto. */
function SubirImagen({ onCambiar, onError, deshabilitado }: PropsSubir) {
  const { t } = useTranslation();
  const entrada = useRef<HTMLInputElement>(null);

  const alElegir = async (e: ChangeEvent<HTMLInputElement>) => {
    const archivo = e.target.files?.[0];
    e.target.value = '';
    if (!archivo) return;
    try {
      onCambiar(await normalizarArchivo(archivo));
    } catch (error) {
      onError(error);
    }
  };

  return (
    <Stack spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'flex-start' } }}>
      <Button variant="outlined" startIcon={<FileUp size={16} aria-hidden />} onClick={() => entrada.current?.click()} disabled={deshabilitado}>
        {t('ficha.pep.firma.elegirImagen')}
      </Button>
      <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('ficha.pep.firma.imagenAyuda')}</Typography>
      <input ref={entrada} type="file" accept="image/png,image/jpeg" hidden onChange={(e) => void alElegir(e)} data-testid="entrada-firma" />
    </Stack>
  );
}
