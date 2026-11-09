import {
  Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, FormControlLabel, FormHelperText, FormLabel, Radio, RadioGroup, Stack,
  TextField, Typography, useMediaQuery, useTheme,
} from '@mui/material';
import { esErrorApi } from '@validasoft/api-client';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useRegistrarCuenta } from '../shared/api';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { CAMPO_POR_CODIGO, cuerpoRegistro, DATOS_VACIOS, enmascararNumero, esCodigoDeCampo, esTipoCuenta, TIPOS_CUENTA, validarCuenta, type CampoCuenta, type CodigoCampoCuenta, type DatosCuenta } from './cuentas';

type CampoDeTexto = Exclude<CampoCuenta, 'tipoCuenta'>;

/**
 * Agregar una cuenta (CB-1). Valida como el dominio antes de enviar; si la API rechaza un dato, el error va en su campo.
 * Los errores aparecen al intentar guardar, no mientras se escribe. En el celular ocupa toda la pantalla.
 */
export function DialogoAgregarCuenta({ onCerrar }: { onCerrar: () => void }) {
  const { t } = useTranslation();
  const tema = useTheme();
  const pantallaCompleta = useMediaQuery(tema.breakpoints.down('sm'));
  const mostrar = useAvisos((s) => s.mostrar);
  const registro = useRegistrarCuenta();
  const [datos, setDatos] = useState<DatosCuenta>(DATOS_VACIOS);
  const [intentado, setIntentado] = useState(false);
  const [errorApi, setErrorApi] = useState<CodigoCampoCuenta | null>(null);
  const locales = validarCuenta(datos);
  const errorDe = (campo: CampoCuenta): string | null => {
    const codigo = (intentado ? locales[campo] : undefined) ?? (errorApi && CAMPO_POR_CODIGO[errorApi] === campo ? errorApi : undefined);
    return codigo ? t(`errores.${codigo}`) : null;
  };

  const cambiar = <K extends CampoCuenta>(campo: K, valor: DatosCuenta[K]) => {
    setDatos((d) => ({ ...d, [campo]: valor }));
    if (errorApi && CAMPO_POR_CODIGO[errorApi] === campo) setErrorApi(null);
  };

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (Object.keys(locales).length > 0) return;
    registro.mutate(cuerpoRegistro(datos), {
      onSuccess: () => {
        mostrar(t('cuentas.nueva.exito', { numero: enmascararNumero(datos.numero) }));
        onCerrar();
      },
      onError: (error) => setErrorApi(esErrorApi(error) && esCodigoDeCampo(error.codigo) ? error.codigo : null),
    });
  };

  const campoDeTexto = (campo: CampoDeTexto, etiqueta: string, ayuda?: string, extra?: { inputMode?: 'numeric' | 'text'; autoComplete?: string; maxLength?: number }) => {
    const error = errorDe(campo);
    return (
      <TextField
        label={etiqueta}
        value={datos[campo]}
        onChange={(e) => cambiar(campo, e.target.value)}
        required={campo !== 'moneda'}
        error={Boolean(error)}
        helperText={error ?? ayuda}
        slotProps={{ htmlInput: { inputMode: extra?.inputMode, autoComplete: extra?.autoComplete ?? 'off', maxLength: extra?.maxLength ?? 120 } }}
      />
    );
  };

  const errorTipo = errorDe('tipoCuenta');
  // Un error de la API que no es de un campo (sesión, red…) va arriba del formulario.
  const errorGeneral = registro.isError && !errorApi ? mensajeDeError(registro.error, t) : null;

  return (
    <Dialog open onClose={registro.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" fullScreen={pantallaCompleta} aria-labelledby="titulo-nueva-cuenta">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-nueva-cuenta">{t('cuentas.nueva.titulo')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography color="text.secondary">{t('cuentas.nueva.ayuda')}</Typography>
            {errorGeneral && <Alert severity="error">{errorGeneral}</Alert>}
            {campoDeTexto('banco', t('cuentas.nueva.banco'), t('cuentas.nueva.bancoAyuda'))}
            <FormControl required error={Boolean(errorTipo)}>
              <FormLabel id="tipo-cuenta">{t('cuentas.nueva.tipo')}</FormLabel>
              <RadioGroup row aria-labelledby="tipo-cuenta" value={datos.tipoCuenta} onChange={(e) => cambiar('tipoCuenta', esTipoCuenta(e.target.value) ? e.target.value : '')}>
                {TIPOS_CUENTA.map((tipo) => (
                  <FormControlLabel key={tipo} value={tipo} control={<Radio />} label={t(`cuentas.tipos.${tipo}`)} />
                ))}
              </RadioGroup>
              {errorTipo && <FormHelperText>{errorTipo}</FormHelperText>}
            </FormControl>
            {campoDeTexto('numero', t('cuentas.nueva.numero'), t('cuentas.nueva.numeroAyuda'), { inputMode: 'numeric', maxLength: 40 })}
            {campoDeTexto('titular', t('cuentas.nueva.titular'), undefined, { autoComplete: 'name' })}
            {campoDeTexto('identificacionTitular', t('cuentas.nueva.identificacion'), t('cuentas.nueva.identificacionAyuda'), { maxLength: 30 })}
            {campoDeTexto('moneda', t('cuentas.nueva.moneda'), t('cuentas.nueva.monedaAyuda'), { maxLength: 3 })}
            <Typography color="text.secondary" sx={{ fontSize: 13, lineHeight: '18px' }}>
              {t('cuentas.nueva.protegido')}
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={registro.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={registro.isPending}>
            {t('cuentas.nueva.guardar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
