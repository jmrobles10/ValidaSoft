import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { CLAVE_PLATAFORMA, llamar, nitAleatorio } from '../../soporte/api';
import { crearEscenario } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

const RUTA = '/api/v1/plataforma/importacion/portal';
const CABECERAS = { 'Content-Type': 'application/json', 'X-Api-Key': '<llave de Plataforma>' };

interface Aviso {
  clave: string;
  campo: string;
  codigo: string;
  mensaje: string;
}

interface CorridaImportada {
  id: string;
  estado: string;
  dryRun: boolean;
  error: string | null;
  resumen: {
    tercerosLeidos: number;
    tercerosCreados: number;
    tercerosEncontrados: number;
    tercerosRechazados: number;
    aceptaciones: number;
    sinVinculo: number;
    advertencias: number;
    rechazos: number;
    detalleAdvertencias: Aviso[];
    detalleRechazos: Aviso[];
  };
}

/** JSON de muestra con la forma del Portal (fixture de pruebas con datos ficticios): tres terceros y sus catálogos. */
function archivoDePrueba(): Record<string, unknown> {
  const base = 7_000_000 + Math.floor(Math.random() * 1_000_000);
  const valores: Record<string, string> = {
    TERID_J: String(base),
    TERID_N: String(base + 1),
    TERID_E: String(base + 2),
    NIT_J: nitAleatorio().numero,
    CC_N: String(10_000_000 + Math.floor(Math.random() * 80_000_000)),
    NIT_E: nitAleatorio().numero,
    TELEFONO_J: '6015551234',
    DIRECCION_J: 'Calle 45 # 12-08',
  };
  const plantilla = readFileSync(resolve(import.meta.dirname, '../../../tests/Validasoft.Integracion.Tests/Fixtures/portal-terceros.json'), 'utf8');
  return JSON.parse(plantilla.replace(/\{\{(\w+)\}\}/g, (_, clave: string) => valores[clave] ?? '')) as Record<string, unknown>;
}

async function esperarCorrida(id: string): Promise<CorridaImportada> {
  const limite = Date.now() + 60_000;
  let corrida = await llamar<CorridaImportada>('GET', `${RUTA}/${id}`, { llave: CLAVE_PLATAFORMA });
  while (corrida.estado !== 'Completada' && corrida.estado !== 'Fallida' && Date.now() < limite) {
    await new Promise((r) => setTimeout(r, 300));
    corrida = await llamar<CorridaImportada>('GET', `${RUTA}/${id}`, { llave: CLAVE_PLATAFORMA });
  }
  expect(corrida.estado).toBe('Completada');
  return corrida;
}

recorrido(
  {
    dominio: 'integracion',
    actor: 'Plataforma',
    titulo: 'Importa terceros desde el Portal de proveedores',
    resumen:
      'El operador de Plataforma lanza una importación desde el Portal de proveedores: primero una simulación que no escribe nada y luego la importación real. ' +
      'Al terminar, la corrida muestra cuántos terceros se crearon, cuántos ya existían y qué datos quedaron con advertencia o rechazados.',
    precondiciones: [
      'Un grupo con Constructora Norte, para mapear la unidad 7 del Portal a su empresa.',
      'La llave de Plataforma y un JSON de muestra con la forma del Portal (datos ficticios; no hay conexión real).',
    ],
    relacionados: ['integracion/sistema-integrador', 'integracion/invitar-por-api'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const unidades = [{ unidad: '7', empresaId: e.norte.id }];
    const archivo = archivoDePrueba();

    const simulacion = { fuente: 'Archivo', dryRun: true, tamanoLote: 2, unidades, archivo };
    const encolada = await llamar<{ id: string }>('POST', RUTA, { llave: CLAVE_PLATAFORMA, cuerpo: simulacion });
    await mostrarCodigo(page, {
      titulo: 'Lanza la simulación de la importación',
      peticion: `POST ${RUTA}`,
      cabeceras: CABECERAS,
      cuerpo: { fuente: 'Archivo', dryRun: true, tamanoLote: 2, unidades, archivo: '{ "catalogos": { … }, "terceros": [ … ] }' },
      respuesta: { estado: 202, cuerpo: encolada },
    });
    const peticion = page.locator('#peticion');
    await paso(page, {
      titulo: 'Lanza una simulación desde el Portal',
      explicacion:
        'Con la llave de Plataforma envías el JSON del Portal a POST /api/v1/plataforma/importacion/portal. Con «dryRun» en verdadero la corrida solo calcula lo que haría: no crea ni cambia terceros. ' +
        'La respuesta es 202 con el identificador de la corrida, que se procesa en segundo plano por lotes de «tamanoLote» terceros.',
      resaltar: peticion,
      variantes: [
        { cuando: '«dryRun» está en falso', entonces: 'La corrida escribe de verdad: crea los terceros nuevos y la versión importada de cada sección.' },
        { cuando: 'La fuente es «Conectado»', entonces: 'El Portal se lee con la conexión configurada en el servidor; sin ella la API responde 409 «origen-no-configurado».' },
      ],
    });

    const simulada = await esperarCorrida(encolada.id);
    await mostrarCodigo(page, {
      titulo: 'Consulta el resultado de la simulación',
      peticion: `GET ${RUTA}/${encolada.id}`,
      cabeceras: { 'X-Api-Key': '<llave de Plataforma>' },
      respuesta: { estado: 200, cuerpo: simulada },
    });
    await paso(page, {
      titulo: 'Revisa lo que haría la importación',
      explicacion:
        'El resumen cuenta los terceros leídos, los que se crearían, los que ya existían y los rechazados. «aceptaciones» son las invitaciones aceptadas que se asocian a las empresas del mapa de unidades. ' +
        'Como es una simulación, la base de datos no cambió: puedes repetirla cuantas veces quieras antes de la importación real.',
      resaltar: page.locator('#respuesta'),
    });

    const real = await llamar<{ id: string }>('POST', RUTA, {
      llave: CLAVE_PLATAFORMA,
      cuerpo: { fuente: 'Archivo', dryRun: false, tamanoLote: 2, unidades, archivo },
    });
    const terminada = await esperarCorrida(real.id);
    await mostrarCodigo(page, {
      titulo: 'Ejecuta la importación real',
      peticion: `GET ${RUTA}/${real.id}`,
      cabeceras: { 'X-Api-Key': '<llave de Plataforma>' },
      respuesta: { estado: 200, cuerpo: terminada },
    });
    await paso(page, {
      titulo: 'Revisa el resultado de la importación real',
      explicacion:
        'Con «dryRun» en falso, los terceros nuevos se crean con su versión importada de cada sección, y los que ya existían por su número de identificación no se duplican. ' +
        'Si la corrida se interrumpe, se reanuda desde su último lote con POST …/{id}/reanudar, sin volver a procesar lo ya importado.',
      resaltar: page.locator('#respuesta'),
      variantes: [
        { cuando: 'La corrida ya terminó', entonces: 'Una corrida completada no vuelve a hacer nada: reanudarla responde 409 «corrida-completada».' },
      ],
    });

    await mostrarCodigo(page, {
      titulo: 'Revisa las advertencias y los rechazos',
      peticion: `GET ${RUTA}/${real.id}`,
      cabeceras: { 'X-Api-Key': '<llave de Plataforma>' },
      respuesta: {
        estado: 200,
        cuerpo: { advertencias: terminada.resumen.detalleAdvertencias.slice(0, 2), rechazos: terminada.resumen.detalleRechazos.slice(0, 2) },
      },
    });
    await paso(page, {
      titulo: 'Revisa los datos que no se pudieron importar',
      explicacion:
        'Un dato con problema no detiene la corrida: el tercero queda rechazado con su motivo, o se importa con una advertencia (por ejemplo, un dígito de verificación que se recalculó). ' +
        'Cada aviso trae la clave del tercero en el Portal, el campo y el código del problema. El detalle completo se descarga como CSV desde …/{id}/rechazos.',
      resaltar: page.locator('#respuesta'),
    });
  },
);
