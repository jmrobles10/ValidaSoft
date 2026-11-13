import { llamar } from '../../soporte/api';
import { publicarFlujoDeFicha, soloPara, type RequisitoDeFicha } from '../../soporte/ficha';
import { crearEscenario, type Area, type Empresa, type Escenario } from '../../soporte/semilla';

/** Área de la empresa por su código (falla con un mensaje claro si no existe, en vez de un `undefined` más adelante). */
export function areaDe(empresa: Empresa, codigo: string): Area {
  const area = empresa.areas[codigo];
  if (!area) throw new Error(`La empresa ${empresa.nombre} no tiene el área «${codigo}».`);
  return area;
}

/**
 * Siembras compartidas por los recorridos (lo que NO es objeto del recorrido se crea por API). Agrega funciones nuevas al final; no
 * cambies las que ya usan otros recorridos.
 */

/** Las seis secciones de la ficha del tercero, en el orden en que las pide el flujo. */
export const SECCIONES_DE_LA_FICHA: RequisitoDeFicha[] = [
  { seccion: 'FICHA_GENERAL' },
  { seccion: 'FICHA_TRIBUTARIA' },
  { seccion: 'FICHA_LEGAL' },
  { seccion: 'FICHA_FINANCIERA' },
  { seccion: 'FICHA_SST' },
  { seccion: 'FICHA_ESPECIALIDADES' },
];

/**
 * Publica un flujo con `requisitos` para `tipoVinculo` de `empresa` y lo deja como el flujo POR DEFECTO de ese tipo (el que usan las
 * invitaciones nuevas). Devuelve el id del flujo.
 */
export async function publicarFlujoPorDefecto(
  e: Escenario,
  empresa: Empresa,
  area: Area,
  codigo: string,
  tipoVinculo: 'PROVEEDOR' | 'CLIENTE',
  requisitos: RequisitoDeFicha[],
): Promise<string> {
  const flujoId = await publicarFlujoDeFicha(e, empresa, codigo, area, tipoVinculo, requisitos);
  const flujo = await llamar<{ tipoVinculo: { id: string } }>('GET', `/api/v1/flujos/${flujoId}`, { llave: e.admin, empresa: empresa.id });
  await llamar('PUT', '/api/v1/flujos/por-defecto', {
    llave: e.admin,
    empresa: empresa.id,
    cuerpo: { tipoVinculoId: flujo.tipoVinculo.id, flujoId, empresaId: empresa.id },
  });
  return flujoId;
}

/**
 * Grupo con Constructora Sur pidiendo a sus PROVEEDORES el RUT, la Cámara de comercio (solo a personas jurídicas) y las seis secciones de
 * la ficha. El enlace de invitación de un proveedor pide código por correo.
 */
export async function escenarioConFichaCompleta(): Promise<Escenario> {
  const e = await crearEscenario();
  await publicarFlujoPorDefecto(e, e.sur, areaDe(e.sur, 'revision'), 'PROVEEDOR_FICHA', 'PROVEEDOR', [
    { tipoDocumento: 'RUT' },
    { tipoDocumento: 'CAMARA_COMERCIO', condicion: { tipo: 'Predicado', atributo: 'tercero.tipoPersona', operador: 'Igual', valores: ['Juridica'] } },
    ...SECCIONES_DE_LA_FICHA,
  ]);
  return e;
}

/**
 * Grupo con un flujo de CLIENTE de Sur que pide según el tipo de persona: RUT y cédula (solo natural) o Cámara de comercio (solo jurídica),
 * y las seis secciones de la ficha. El enlace de invitación de un cliente no pide código por correo.
 */
export async function escenarioConFlujoDeClientePorTipo(): Promise<Escenario> {
  const e = await crearEscenario();
  await publicarFlujoDeFicha(e, e.sur, 'CLIENTES_POR_TIPO', areaDe(e.sur, 'revision'), 'CLIENTE', [
    { tipoDocumento: 'RUT' },
    { tipoDocumento: 'CEDULA', condicion: soloPara('Natural') },
    { tipoDocumento: 'CAMARA_COMERCIO', condicion: soloPara('Juridica') },
    ...SECCIONES_DE_LA_FICHA,
  ]);
  return e;
}
