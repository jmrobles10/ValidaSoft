import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, FormControlLabel, FormHelperText, FormLabel, Radio, RadioGroup, Stack,
  TextField, Typography, useMediaQuery, useTheme,
} from '@mui/material';
import { esErrorApi } from '@validasoft/api-client';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useCatalogoCompleto } from '../ficha/api';
import { PAISES } from '../ficha/paises';
import { SelectorUnico } from '../ficha/selectores';
import { useRegistrarCuenta } from '../shared/api';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { CAMPO_POR_CODIGO, cuerpoRegistro, DATOS_VACIOS, enmascararNumero, esCodigoDeCampo, esTipoCuenta, TIPOS_CUENTA, validarCuenta, type CampoCuenta, type CodigoCampoCuenta, type DatosCuenta } from './cuentas';

type CampoDeTexto = Exclude<CampoCuenta, 'tipoCuenta' | 'otroBanco' | 'bancoCodigo' | 'pais'>;

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
  const bancos = useCatalogoCompleto('bancos');
  const opcionesBanco = (bancos.data ?? []).map((b) => ({ codigo: b.codigo, nombre: b.nombre }));
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
  // Cambiar entre «de la lista» y «otro banco» no arrastra lo del otro modo.
  const alternarOtroBanco = () => setDatos((d) => ({ ...d, otroBanco: !d.otroBanco, banco: '', bancoCodigo: '', bancoSwift: '', pais: 'CO' }));

  const enviar = (evento: SubmitEvent) => {
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

  const campoDeTexto = (campo: CampoDeTexto, etiqueta: string, ayuda?: string, extra?: { inputMode?: 'numeric' | 'text'; autoComplete?: string; maxLength?: number; fila?: boolean }) => {
    const error = errorDe(campo);
    return (
      <TextField
        label={etiqueta}
        value={datos[campo]}
        onChange={(e) => cambiar(campo, e.target.value)}
        required={!['moneda', 'bancoSwift', 'pagosNombre', 'pagosCorreo', 'pagosTelefono'].includes(campo)}
        error={Boolean(error)}
        helperText={error ?? ayuda}
        sx={extra?.fila ? { gridColumn: '1 / -1' } : undefined}
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
            <Typography sx={{ color: 'text.secondary' }}>{t('cuentas.nueva.ayuda')}</Typography>
            {errorGeneral && <Alert severity="error">{errorGeneral}</Alert>}
            {/* Los campos cortos (número, moneda, correo y teléfono) van en dos columnas; banco, tipo y titular, uno por fila (UI-8). */}
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 2 }}>
              {datos.otroBanco ? (
                <>
                  {campoDeTexto('banco', t('cuentas.nueva.nombreBanco'), t('cuentas.nueva.nombreBancoAyuda'), { fila: true })}
                  <SelectorUnico opciones={PAISES} valor={datos.pais} onCambiar={(p) => cambiar('pais', p ?? 'CO')} etiqueta={t('cuentas.nueva.pais')} />
                  {campoDeTexto('bancoSwift', t('cuentas.nueva.swift'), t('cuentas.nueva.swiftAyuda'), { maxLength: 13 })}
                </>
              ) : (
                <Box sx={{ gridColumn: '1 / -1' }}>
                  <SelectorUnico
                    opciones={opcionesBanco}
                    valor={datos.bancoCodigo || null}
                    onCambiar={(codigo) => cambiar('bancoCodigo', codigo ?? '')}
                    etiqueta={t('cuentas.nueva.banco')}
                    ayuda={t('cuentas.nueva.bancoAyuda')}
                    mensaje={errorDe('banco')}
                    deshabilitado={bancos.isPending}
                  />
                </Box>
              )}
              <Box sx={{ gridColumn: '1 / -1' }}>
                <Button variant="text" size="small" onClick={alternarOtroBanco} sx={{ color: 'text.secondary' }}>
                  {datos.otroBanco ? t('cuentas.nueva.bancoDeLista') : t('cuentas.nueva.otroBanco')}
                </Button>
              </Box>
              <FormControl required error={Boolean(errorTipo)} sx={{ gridColumn: '1 / -1' }}>
                <FormLabel id="tipo-cuenta">{t('cuentas.nueva.tipo')}</FormLabel>
                <RadioGroup row aria-labelledby="tipo-cuenta" value={datos.tipoCuenta} onChange={(e) => cambiar('tipoCuenta', esTipoCuenta(e.target.value) ? e.target.value : '')}>
                  {TIPOS_CUENTA.map((tipo) => (
                    <FormControlLabel key={tipo} value={tipo} control={<Radio />} label={t(`cuentas.tipos.${tipo}`)} />
                  ))}
                </RadioGroup>
                {errorTipo && <FormHelperText>{errorTipo}</FormHelperText>}
              </FormControl>
              {campoDeTexto('numero', t('cuentas.nueva.numero'), t('cuentas.nueva.numeroAyuda'), { inputMode: 'numeric', maxLength: 40 })}
              {campoDeTexto('moneda', t('cuentas.nueva.moneda'), t('cuentas.nueva.monedaAyuda'), { maxLength: 3 })}
              {campoDeTexto('titular', t('cuentas.nueva.titular'), undefined, { autoComplete: 'name' })}
              {campoDeTexto('identificacionTitular', t('cuentas.nueva.identificacion'), t('cuentas.nueva.identificacionAyuda'), { maxLength: 30 })}
              <Box sx={{ gridColumn: '1 / -1' }}>
                <Typography component="h3" sx={{ fontWeight: 600, fontSize: 14 }}>
                  {t('cuentas.nueva.contactoPagos')}
                </Typography>
                <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cuentas.nueva.contactoPagosAyuda')}</Typography>
              </Box>
              {campoDeTexto('pagosNombre', t('cuentas.nueva.pagosNombre'), undefined, { autoComplete: 'off', fila: true })}
              {campoDeTexto('pagosCorreo', t('cuentas.nueva.pagosCorreo'), undefined, { autoComplete: 'off' })}
              {campoDeTexto('pagosTelefono', t('cuentas.nueva.pagosTelefono'), undefined, { inputMode: 'text', maxLength: 40 })}
            </Box>
            <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
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
