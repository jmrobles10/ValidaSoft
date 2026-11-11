import { Alert, Box, Button, Link, Stack } from '@mui/material';
import { EsqueletoFormulario } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { lazy, Suspense, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MarcoMinimo } from '../sesion/MarcoMinimo';
import { aplicarSesionDirecta } from '../sesion/sesionDirecta';
import type { EstadoSesion } from '../sesion/sesionStore';
import { PasoCodigo } from './PasoCodigo';
import { PasoCorreo } from './PasoCorreo';
import { PasoCuenta } from './PasoCuenta';
import { PasoRegistrarPasskey, PasoVerificarPasskey } from './PasoPasskey';
import { resolverPaso, type PasoIngreso } from './estado';
import type { PasoDeIngresoDto } from '@validasoft/api-client';

// En el build de producción esta rama se elimina y el formulario (con la firma de aserciones) no se empaqueta.
const FormularioSesionDev = import.meta.env.DEV ? lazy(() => import('../sesion/FormularioSesionDev').then((m) => ({ default: m.FormularioSesionDev }))) : null;
const LLAVE_DESARROLLO = 'validasoft.dev.sesion';
const BANDEJA_DE_DESARROLLO = 'http://localhost:8025';

/** En desarrollo se recuerda si se usó la sesión simulada en esta pestaña, para no repetir un clic en cada recarga. */
function empezarEnDesarrollo(): boolean {
  if (!FormularioSesionDev) return false;
  try {
    return window.sessionStorage.getItem(LLAVE_DESARROLLO) === '1';
  } catch {
    return false;
  }
}

function recordarDesarrollo(valor: boolean): void {
  try {
    if (valor) window.sessionStorage.setItem(LLAVE_DESARROLLO, '1');
    else window.sessionStorage.removeItem(LLAVE_DESARROLLO);
  } catch {
    // Sin almacenamiento solo se pierde la comodidad.
  }
}

/** Por qué terminó la sesión anterior, si hubo una: la persona la cerró, se agotó o la cerraron otros. */
function avisoDeSalida(estado: EstadoSesion, t: TFunction): { severidad: 'info' | 'warning'; titulo: string; texto: string } | null {
  switch (estado) {
    case 'cerrada':
      return { severidad: 'info', titulo: t('ingreso.cerradaTitulo'), texto: t('ingreso.cerradaTexto') };
    case 'terminada':
      return { severidad: 'warning', titulo: t('ingreso.vencidaTitulo'), texto: t('ingreso.vencidaTexto') };
    case 'revocada':
      return { severidad: 'warning', titulo: t('ingreso.revocadaTitulo'), texto: t('ingreso.revocadaTexto') };
    default:
      return null;
  }
}

/**
 * Ingreso directo a Validasoft sin contraseñas (ING-1…ING-3): correo → código → (cuenta) → passkey cuando la cuenta la tiene o la exige, o solo
 * la passkey. Es lo que se ve fuera de un sistema integrado; dentro de uno, la sesión llega por `postMessage`. En desarrollo ofrece además la
 * sesión simulada de siempre.
 */
export function PantallaIngreso({ estado }: { estado: EstadoSesion }) {
  const { t } = useTranslation();
  const [paso, setPaso] = useState<PasoIngreso>({ paso: 'correo' });
  const [correo, setCorreo] = useState('');
  const [desarrollo, setDesarrollo] = useState(empezarEnDesarrollo);

  const alResultado = (dto: PasoDeIngresoDto) => {
    const resultado = resolverPaso(dto);
    if (resultado.tipo === 'sesion') aplicarSesionDirecta(resultado.sesion);
    else setPaso(resultado.paso);
  };
  const volver = () => setPaso({ paso: 'correo' });
  const cambiarDesarrollo = (valor: boolean) => {
    recordarDesarrollo(valor);
    setDesarrollo(valor);
  };

  if (FormularioSesionDev && desarrollo) {
    return (
      <MarcoMinimo>
        <Suspense fallback={<EsqueletoFormulario campos={3} conTarjeta sx={{ maxWidth: 420, mx: 'auto', mt: 4 }} />}>
          <FormularioSesionDev vencida={estado === 'vencida'} />
        </Suspense>
        <Box sx={{ textAlign: 'center', pb: 4 }}>
          <Button variant="text" size="small" onClick={() => cambiarDesarrollo(false)}>
            {t('ingreso.desarrolloVolver')}
          </Button>
        </Box>
      </MarcoMinimo>
    );
  }

  const aviso = avisoDeSalida(estado, t);

  return (
    <MarcoMinimo>
      <Box sx={{ display: 'flex', justifyContent: 'center', px: 2, pt: { xs: 4, sm: 10 }, pb: 4 }}>
        <Stack spacing={2} sx={{ width: '100%', maxWidth: 420 }}>
          {aviso && (
            <Alert severity={aviso.severidad} role="status">
              <strong>{aviso.titulo}.</strong> {aviso.texto}
            </Alert>
          )}
          <Box
            sx={(th) => ({ p: 3, border: 1, borderColor: 'divider', borderRadius: `${String(th.radio.md)}px`, bgcolor: 'background.paper' })}
            data-testid="tarjeta-ingreso"
          >
            {paso.paso === 'correo' && (
              <PasoCorreo
                correoInicial={correo}
                alCodigo={(escrito, respuesta) => {
                  setCorreo(escrito);
                  setPaso({ paso: 'codigo', correo: escrito, desafio: respuesta.desafio, expira: new Date(respuesta.expira) });
                }}
                alResultado={alResultado}
              />
            )}
            {paso.paso === 'codigo' && (
              <PasoCodigo
                key={paso.desafio}
                correo={paso.correo}
                desafio={paso.desafio}
                expira={paso.expira}
                alResultado={alResultado}
                alReenviar={(respuesta) => setPaso({ ...paso, desafio: respuesta.desafio, expira: new Date(respuesta.expira) })}
                alVolver={volver}
              />
            )}
            {paso.paso === 'cuenta' && <PasoCuenta desafio={paso.desafio} cuentas={paso.cuentas} alResultado={alResultado} />}
            {paso.paso === 'passkey' && <PasoVerificarPasskey desafio={paso.desafio} opciones={paso.opciones} alResultado={alResultado} alVolver={volver} />}
            {paso.paso === 'registrar' && <PasoRegistrarPasskey desafio={paso.desafio} opciones={paso.opciones} alResultado={alResultado} alVolver={volver} />}
          </Box>
          {FormularioSesionDev && (
            <Stack direction="row" spacing={2} sx={{ justifyContent: 'center' }}>
              <Button variant="text" size="small" onClick={() => cambiarDesarrollo(true)}>
                {t('ingreso.desarrollo')}
              </Button>
              <Link href={BANDEJA_DE_DESARROLLO} target="_blank" rel="noreferrer" sx={{ alignSelf: 'center', fontSize: 13 }}>
                {t('ingreso.desarrolloCorreos')}
              </Link>
            </Stack>
          )}
        </Stack>
      </Box>
    </MarcoMinimo>
  );
}
