import { esErrorApi, type CampoFormularioDto } from '@validasoft/api-client';
import { claveDia } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { campoDelError, datosParaEnviar, erroresDelFormulario, faltaParaEnviar } from './formulario';

/** Error que la API marcó en un campo (422 con `campo`): se muestra hasta que el tercero cambie ese campo. */
interface ErrorDelServidor {
  campo: string;
  mensaje: string;
}

/**
 * Estado de un formulario configurable: valores, campos tocados (el error de cada campo aparece cuando ya se tocó), qué
 * falta para enviar y el error que la API marque en un campo. `archivo`: los textos hablan de subir el archivo.
 * `iniciales`: lo ya declarado en la versión vigente (solo se lee al montar).
 */
export function useFormulario(campos: readonly CampoFormularioDto[], archivo = false, iniciales: Readonly<Record<string, string>> = {}) {
  const { t } = useTranslation();
  const [valores, setValores] = useState<Readonly<Record<string, string>>>(iniciales);
  const [tocados, setTocados] = useState<ReadonlySet<string>>(() => new Set());
  const [delServidor, setDelServidor] = useState<ErrorDelServidor | null>(null);
  const errores = erroresDelFormulario(campos, valores, claveDia(new Date()));
  const etiquetaDelServidor = delServidor ? campos.find((c) => c.clave === delServidor.campo)?.etiqueta : undefined;

  const tocar = (clave: string) => setTocados((s) => (s.has(clave) ? s : new Set(s).add(clave)));

  const cambiar = (clave: string, valor: string) => {
    setValores((v) => ({ ...v, [clave]: valor }));
    setDelServidor((e) => (e?.campo === clave ? null : e));
  };

  /** Mensaje bajo el campo: el de la API, o el propio si ya se tocó. */
  const mensajeDe = (clave: string): string | null => {
    if (delServidor?.campo === clave) return delServidor.mensaje;
    const error = errores[clave];
    return error && tocados.has(clave) ? t(`formulario.errores.${error}`) : null;
  };

  /** Marca el campo de un 422 de la API; `false` si el error no es de un campo de este formulario. */
  const marcarError = (error: unknown): boolean => {
    const campo = campoDelError(error);
    if (!campo || !campos.some((c) => c.clave === campo)) return false;
    setDelServidor({ campo, mensaje: esErrorApi(error) ? error.detalle : '' });
    tocar(campo);
    return true;
  };

  const falta = etiquetaDelServidor
    ? t(archivo ? 'formulario.faltaCorregirArchivo' : 'formulario.faltaCorregir', { campos: etiquetaDelServidor })
    : faltaParaEnviar(campos, errores, t, archivo);

  return {
    valores,
    cambiar,
    tocar,
    mensajeDe,
    marcarError,
    /** Qué falta para enviar (junto al botón deshabilitado); `null` si está completo. */
    falta,
    completo: falta === null,
    datos: () => datosParaEnviar(campos, valores),
  };
}

export type EstadoFormulario = ReturnType<typeof useFormulario>;
