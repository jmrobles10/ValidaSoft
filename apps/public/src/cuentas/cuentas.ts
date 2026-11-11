import type { CuentaBancariaPublicaDto, DesignacionDeCuentaDto, RegistrarCuentaBancariaDto, VinculoPublicoDto } from '@validasoft/api-client';
import { situacionDelVinculo } from '../vinculos/situacion';

export type TipoCuenta = 'Ahorros' | 'Corriente';
export type EstadoDesignacion = 'Propuesta' | 'Aprobada' | 'Rechazada';

export const TIPOS_CUENTA: readonly TipoCuenta[] = ['Ahorros', 'Corriente'];

export function esTipoCuenta(valor: string): valor is TipoCuenta {
  return valor === 'Ahorros' || valor === 'Corriente';
}

/** Solo los estados que la API devuelve al tercero; otro (p. ej. `Reemplazada`) no se muestra. */
export function estadoDesignacion(estado: string): EstadoDesignacion | null {
  return estado === 'Propuesta' || estado === 'Aprobada' || estado === 'Rechazada' ? estado : null;
}

export const esActiva = (cuenta: CuentaBancariaPublicaDto) => cuenta.estado === 'Activa';

/** «Bancolombia ****5678»: cómo se nombra una cuenta en los textos (el número nunca va completo). */
export const nombreCuenta = (cuenta: Pick<CuentaBancariaPublicaDto, 'banco' | 'numeroEnmascarado'>) => `${cuenta.banco} ${cuenta.numeroEnmascarado}`;

/**
 * Lo que el tercero escribe en «Agregar cuenta», tal cual. El banco se elige del catálogo (`bancoCodigo`: código de compensación local,
 * CENIT en Colombia); si el suyo no está (`otroBanco`) escribe su nombre, su país y, si lo sabe, su SWIFT. El contacto de pagos es opcional.
 */
export interface DatosCuenta {
  banco: string;
  bancoCodigo: string;
  otroBanco: boolean;
  bancoSwift: string;
  pais: string;
  tipoCuenta: TipoCuenta | '';
  numero: string;
  titular: string;
  identificacionTitular: string;
  moneda: string;
  pagosNombre: string;
  pagosCorreo: string;
  pagosTelefono: string;
}

export type CampoCuenta = keyof DatosCuenta;

/** Códigos de validación de la API (CB-1) y el campo que señalan; los mismos sirven para la validación local. */
export const CAMPO_POR_CODIGO = {
  'banco-requerido': 'banco',
  'tipo-cuenta-invalido': 'tipoCuenta',
  'numero-cuenta-invalido': 'numero',
  'titular-requerido': 'titular',
  'identificacion-titular-requerida': 'identificacionTitular',
  'moneda-invalida': 'moneda',
  'banco-no-encontrado': 'banco',
  'swift-invalido': 'bancoSwift',
  'contacto-pagos-nombre-requerido': 'pagosNombre',
  'correo-invalido': 'pagosCorreo',
} as const satisfies Record<string, CampoCuenta>;

export type CodigoCampoCuenta = keyof typeof CAMPO_POR_CODIGO;

export function esCodigoDeCampo(codigo: string): codigo is CodigoCampoCuenta {
  return codigo in CAMPO_POR_CODIGO;
}

export const DATOS_VACIOS: DatosCuenta = {
  banco: '',
  bancoCodigo: '',
  otroBanco: false,
  bancoSwift: '',
  pais: 'CO',
  tipoCuenta: '',
  numero: '',
  titular: '',
  identificacionTitular: '',
  moneda: 'COP',
  pagosNombre: '',
  pagosCorreo: '',
  pagosTelefono: '',
};

const SWIFT = /^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$/;
const CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Las mismas reglas del dominio (`CuentaBancaria.Registrar`), para no esperar a la API: número de 6 a 20 dígitos con
 * espacios o guiones, banco (del catálogo, o su nombre si es otro), titular e identificación obligatorios, moneda de tres letras o
 * vacía (COP), SWIFT de 8 u 11 caracteres si se escribe y, si hay contacto de pagos, su nombre y un correo válido.
 */
export function validarCuenta(datos: DatosCuenta): Partial<Record<CampoCuenta, CodigoCampoCuenta>> {
  const errores: Partial<Record<CampoCuenta, CodigoCampoCuenta>> = {};
  if (datos.otroBanco ? !datos.banco.trim() : !datos.bancoCodigo.trim()) errores.banco = 'banco-requerido';
  const swift = datos.bancoSwift.replace(/\s/g, '').toUpperCase();
  if (swift && !SWIFT.test(swift)) errores.bancoSwift = 'swift-invalido';
  if (datos.pagosNombre.trim() || datos.pagosCorreo.trim() || datos.pagosTelefono.trim()) {
    if (!datos.pagosNombre.trim()) errores.pagosNombre = 'contacto-pagos-nombre-requerido';
    if (!CORREO.test(datos.pagosCorreo.trim())) errores.pagosCorreo = 'correo-invalido';
  }
  if (!datos.tipoCuenta) errores.tipoCuenta = 'tipo-cuenta-invalido';
  const numero = datos.numero.trim();
  const digitos = numero.replace(/[\s-]/g, '');
  if (!/^[\d\s-]*$/.test(numero) || digitos.length < 6 || digitos.length > 20) errores.numero = 'numero-cuenta-invalido';
  if (!datos.titular.trim()) errores.titular = 'titular-requerido';
  if (!datos.identificacionTitular.trim()) errores.identificacionTitular = 'identificacion-titular-requerida';
  const moneda = datos.moneda.trim();
  if (moneda && !/^[A-Za-z]{3}$/.test(moneda)) errores.moneda = 'moneda-invalida';
  return errores;
}

/**
 * «****5678» a partir de lo escrito, como lo enmascara la API. `POST …/cuentas-bancarias` responde 201 con la cuenta, pero
 * el cliente generado la tipa como `IResult` (el endpoint devuelve `Results.Created`): el aviso no depende de ese cuerpo.
 */
export const enmascararNumero = (numero: string) => `****${numero.replace(/\D/g, '').slice(-4)}`;

export function cuerpoRegistro(datos: DatosCuenta): RegistrarCuentaBancariaDto {
  const hayPagos = Boolean(datos.pagosNombre.trim() || datos.pagosCorreo.trim() || datos.pagosTelefono.trim());
  return {
    // Con el banco del catálogo el nombre sale del catálogo (la API lo ignora); con otro banco va el nombre y su país.
    banco: datos.otroBanco ? datos.banco.trim() : null,
    bancoCodigo: datos.otroBanco ? null : datos.bancoCodigo.trim(),
    bancoSwift: datos.bancoSwift.replace(/\s/g, '').toUpperCase() || null,
    pais: datos.otroBanco ? datos.pais || null : null,
    contactoPagos: hayPagos ? { nombre: datos.pagosNombre.trim(), correo: datos.pagosCorreo.trim(), telefono: datos.pagosTelefono.trim() || null } : null,
    tipoCuenta: datos.tipoCuenta,
    numero: datos.numero.trim(),
    titular: datos.titular.trim(),
    identificacionTitular: datos.identificacionTitular.trim(),
    moneda: datos.moneda.trim().toUpperCase() || null,
  };
}

/** Con quién puede designar: vínculo vigente (ni suspendido ni terminado) y con la invitación aceptada. */
export function vinculosParaDesignar(vinculos: readonly VinculoPublicoDto[]): VinculoPublicoDto[] {
  return vinculos.filter((v) => v.invitacionAceptada && situacionDelVinculo(v.estado) === 'vigente');
}

/** La designación de una cuenta con un vínculo, con la cuenta a la que pertenece. */
export interface DesignacionConCuenta {
  cuenta: CuentaBancariaPublicaDto;
  estado: EstadoDesignacion;
  motivoRechazo: string | null;
}

/** Lo que una empresa tiene de tus cuentas: la aprobada, la que espera su aprobación y la última que rechazó. */
export function designacionesDelVinculo(vinculoId: string, cuentas: readonly CuentaBancariaPublicaDto[]): Partial<Record<EstadoDesignacion, DesignacionConCuenta>> {
  const resultado: Partial<Record<EstadoDesignacion, DesignacionConCuenta>> = {};
  for (const cuenta of cuentas) {
    for (const d of cuenta.designadaEn) {
      const estado = estadoDesignacion(d.estado);
      if (d.vinculoId === vinculoId && estado) resultado[estado] = { cuenta, estado, motivoRechazo: d.motivoRechazo ?? null };
    }
  }
  return resultado;
}

/** Designaciones que se muestran en la tarjeta de la cuenta: primero las vigentes, luego las rechazadas. */
export function designacionesVisibles(cuenta: CuentaBancariaPublicaDto): (DesignacionDeCuentaDto & { estado: EstadoDesignacion })[] {
  const orden: Record<EstadoDesignacion, number> = { Aprobada: 0, Propuesta: 1, Rechazada: 2 };
  return cuenta.designadaEn
    .flatMap((d) => {
      const estado = estadoDesignacion(d.estado);
      return estado ? [{ ...d, estado }] : [];
    })
    .sort((a, b) => orden[a.estado] - orden[b.estado] || a.empresa.localeCompare(b.empresa, 'es'));
}
