// Esquemas OpenAPI (3.1) a algo legible: filas aplanadas de campos y ejemplos de JSON construidos del esquema. Funciones puras.

export const nombreDeRef = (ref) => String(ref).split('/').pop();

/**
 * Sigue `$ref`, `oneOf`/`anyOf` con `null` y `type: [..., "null"]` hasta el esquema real. Devuelve `{ esquema, nombre, nulo }`; `nombre` es
 * el del último `$ref` (el tipo con nombre), si lo hubo.
 */
export function normalizar(esquema, componentes, nombre = null) {
  let actual = esquema ?? {};
  let nulo = false;
  let nombreFinal = nombre;
  for (let vueltas = 0; vueltas < 12; vueltas++) {
    if (actual.$ref) {
      nombreFinal = nombreDeRef(actual.$ref);
      actual = componentes[nombreFinal] ?? {};
      continue;
    }
    const alternativas = actual.oneOf ?? actual.anyOf;
    if (alternativas) {
      const sinNulo = alternativas.filter((a) => !(a.type === 'null' && Object.keys(a).length === 1));
      if (sinNulo.length < alternativas.length) nulo = true;
      if (sinNulo.length === 1) {
        actual = { ...sinNulo[0], ...(actual.description ? { description: actual.description } : {}) };
        continue;
      }
      return { esquema: { ...actual, oneOf: sinNulo }, nombre: nombreFinal, nulo };
    }
    if (actual.allOf) {
      const fusion = { type: 'object', properties: {}, required: [] };
      for (const parte of actual.allOf) {
        const n = normalizar(parte, componentes);
        Object.assign(fusion.properties, n.esquema.properties ?? {});
        fusion.required.push(...(n.esquema.required ?? []));
      }
      actual = { ...actual, ...fusion, allOf: undefined };
      continue;
    }
    break;
  }
  let tipo = actual.type;
  if (Array.isArray(tipo)) {
    if (tipo.includes('null')) nulo = true;
    const restantes = tipo.filter((t) => t !== 'null');
    tipo = restantes.length === 1 ? restantes[0] : restantes;
    actual = { ...actual, type: tipo };
  }
  return { esquema: actual, nombre: nombreFinal, nulo };
}

const FORMATOS = { uuid: 'uuid', 'date-time': 'fecha y hora', date: 'fecha', time: 'hora', uri: 'url', email: 'correo', binary: 'archivo', byte: 'texto base64', duration: 'duración' };

/** Tipo legible de un esquema ya normalizado. */
export function tipoLegible(normal, componentes) {
  const { esquema, nombre } = normal;
  if (esquema.enum) return nombre ? `${nombre} (texto)` : 'texto (enum)';
  if (esquema.oneOf) return 'uno de: ' + esquema.oneOf.map((a) => tipoLegible(normalizar(a, componentes), componentes)).join(' | ');
  const tipo = esquema.type;
  if (tipo === 'array') {
    const item = normalizar(esquema.items, componentes);
    return `lista de ${tipoLegible(item, componentes)}`;
  }
  if (tipo === 'string') return FORMATOS[esquema.format] ?? 'texto';
  if (tipo === 'integer') return 'entero';
  if (tipo === 'number') return 'número';
  if (tipo === 'boolean') return 'booleano';
  if (tipo === 'object' || esquema.properties) {
    if (nombre) return nombre;
    if (esquema.additionalProperties && typeof esquema.additionalProperties === 'object') {
      return `mapa de texto → ${tipoLegible(normalizar(esquema.additionalProperties, componentes), componentes)}`;
    }
    return esquema.properties ? 'objeto' : 'objeto libre';
  }
  if (Array.isArray(tipo)) return tipo.join(' | ');
  return nombre ?? 'cualquiera';
}

function restricciones(esquema) {
  const partes = [];
  if (esquema.enum) {
    const valores = esquema.enum.map(String);
    partes.push(`valores: ${valores.slice(0, 14).join(' · ')}${valores.length > 14 ? ' …' : ''}`);
  }
  if (esquema.minimum !== undefined) partes.push(`mínimo ${String(esquema.minimum)}`);
  if (esquema.maximum !== undefined) partes.push(`máximo ${String(esquema.maximum)}`);
  if (esquema.minLength !== undefined) partes.push(`mín. ${String(esquema.minLength)} caracteres`);
  if (esquema.maxLength !== undefined) partes.push(`máx. ${String(esquema.maxLength)} caracteres`);
  if (esquema.minItems !== undefined) partes.push(`mín. ${String(esquema.minItems)} elementos`);
  if (esquema.maxItems !== undefined) partes.push(`máx. ${String(esquema.maxItems)} elementos`);
  if (esquema.pattern) partes.push(`patrón ${esquema.pattern}`);
  if (esquema.default !== undefined && esquema.default !== null) partes.push(`por omisión ${JSON.stringify(esquema.default)}`);
  if (esquema.description) partes.unshift(String(esquema.description));
  return partes.join(' · ');
}

/**
 * Filas de campos de un esquema, en orden y con su ruta (`etapas[].areas[].areaId`). Cada fila: `{ ruta, nivel, tipo, requerido, nulo,
 * detalle }`. Un tipo que ya está en la ruta (recursivo) no se vuelve a expandir. Una lista de objetos se expande con `[]`.
 */
export function aplanar(esquema, componentes, { maxProfundidad = 6, maxFilas = 300 } = {}) {
  const filas = [];
  const raiz = normalizar(esquema, componentes);
  // Tipo con nombre → ruta donde ya se expandió: un tipo repetido (publicada y borrador) no se vuelve a listar.
  const vistos = new Map();

  const recorrer = (normal, prefijo, nivel, pila) => {
    if (filas.length >= maxFilas) return;
    const { esquema: es } = normal;
    const propiedades = es.properties ?? {};
    const requeridos = new Set(es.required ?? []);
    for (const [clave, hijoCrudo] of Object.entries(propiedades)) {
      if (filas.length >= maxFilas) {
        filas.push({ ruta: '…', nivel, tipo: '', requerido: false, nulo: false, detalle: 'Se omiten los campos restantes: el esquema es muy extenso.' });
        return;
      }
      const hijo = normalizar(hijoCrudo, componentes);
      const ruta = prefijo ? `${prefijo}.${clave}` : clave;
      const fila = { ruta, nivel, tipo: tipoLegible(hijo, componentes), requerido: requeridos.has(clave) && !hijo.nulo, nulo: hijo.nulo, detalle: restricciones(hijo.esquema) };
      filas.push(fila);
      expandir(hijo, ruta, nivel + 1, pila, fila);
    }
    if (es.additionalProperties && typeof es.additionalProperties === 'object' && !es.properties) {
      const valor = normalizar(es.additionalProperties, componentes);
      expandir(valor, `${prefijo}{}`, nivel + 1, pila, null);
    }
  };

  const expandir = (normal, ruta, nivel, pila, fila) => {
    let objetivo = normal;
    let rutaActual = ruta;
    if (objetivo.esquema.type === 'array') {
      objetivo = normalizar(objetivo.esquema.items, componentes);
      rutaActual = `${ruta}[]`;
    }
    const esObjeto = objetivo.esquema.properties || (objetivo.esquema.additionalProperties && typeof objetivo.esquema.additionalProperties === 'object');
    if (!esObjeto) return;
    if (objetivo.nombre && pila.includes(objetivo.nombre)) {
      if (fila) fila.detalle = [fila.detalle, `recursivo: ver ${objetivo.nombre}`].filter(Boolean).join(' · ');
      return;
    }
    if (objetivo.nombre && vistos.has(objetivo.nombre)) {
      if (fila) fila.detalle = [fila.detalle, `mismos campos que \`${vistos.get(objetivo.nombre)}\``].filter(Boolean).join(' · ');
      return;
    }
    if (nivel >= maxProfundidad) {
      if (fila) fila.detalle = [fila.detalle, 'no se expande más: ver el esquema en el contrato'].filter(Boolean).join(' · ');
      return;
    }
    if (objetivo.nombre) vistos.set(objetivo.nombre, rutaActual);
    recorrer(objetivo, rutaActual, nivel, objetivo.nombre ? [...pila, objetivo.nombre] : pila);
  };

  if (raiz.esquema.properties || raiz.esquema.additionalProperties) recorrer(raiz, '', 0, raiz.nombre ? [raiz.nombre] : []);
  else if (raiz.esquema.type === 'array') {
    const fila = { ruta: '[]', nivel: 0, tipo: tipoLegible(raiz, componentes), requerido: true, nulo: false, detalle: restricciones(raiz.esquema) };
    filas.push(fila);
    expandir(raiz, '', 0, raiz.nombre ? [raiz.nombre] : [], fila);
  } else {
    filas.push({ ruta: '(valor)', nivel: 0, tipo: tipoLegible(raiz, componentes), requerido: true, nulo: raiz.nulo, detalle: restricciones(raiz.esquema) });
  }
  return filas;
}

const UUID_EJEMPLO = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

/** Valor de ejemplo para un texto según el nombre de la propiedad (lo que da ejemplos legibles en vez de «string»). */
function textoDeEjemplo(clave) {
  const k = clave.toLowerCase();
  const tabla = [
    [/correo|email/, 'contacto@empresa.co'],
    [/telefono|celular/, '+57 601 555 1234'],
    [/^nombres$/, 'Marta Lucía'],
    [/^apellidos$/, 'Ríos Duarte'],
    [/razonsocial|^nombre$|nombrecomercial/, 'Constructora Norte S.A.S.'],
    [/nombre/, 'Nombre de ejemplo'],
    [/^codigo$/, 'CODIGO_EJEMPLO'],
    [/codigo/, 'codigo-de-ejemplo'],
    [/^pais$/, 'CO'],
    [/ciudad/, 'Bogotá'],
    [/direccion/, 'Calle 45 # 12-08'],
    [/url|enlace|link/, 'https://ejemplo.co/recurso'],
    [/numero|identificacion|nit/, '900123456'],
    [/token|llave|secreto|valor$/, 'vsk_prueba_ejemplo'],
    [/motivo|nota|justificacion|detalle|mensaje|descripcion|observacion|comentario|texto|contenido/, 'Texto de ejemplo.'],
    [/hash|sha256|huella/, 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'],
    [/zona/, 'America/Bogota'],
    [/region/, 'co'],
    [/slug/, 'grupo-ejemplo'],
    [/referencia|idexterno/, 'REF-0001'],
  ];
  return tabla.find(([patron]) => patron.test(k))?.[1] ?? 'texto';
}

/** JSON de ejemplo de un esquema (valores plausibles; primer valor de cada enum; una vez cada tipo recursivo). */
export function ejemplo(esquema, componentes, { clave = '', profundidad = 0, pila = [] } = {}) {
  const normal = normalizar(esquema, componentes);
  const es = normal.esquema;
  if (profundidad > 6) return null;
  if (es.example !== undefined) return es.example;
  if (es.default !== undefined && es.default !== null) return es.default;
  if (es.enum) return es.enum[0];
  if (es.oneOf?.length) return ejemplo(es.oneOf[0], componentes, { clave, profundidad: profundidad + 1, pila });
  const tipo = Array.isArray(es.type) ? es.type[0] : es.type;
  if (tipo === 'array') {
    const item = ejemplo(es.items, componentes, { clave, profundidad: profundidad + 1, pila });
    return item === undefined ? [] : [item];
  }
  if (tipo === 'object' || es.properties) {
    if (normal.nombre && pila.includes(normal.nombre)) return {};
    const resultado = {};
    const pilaHija = normal.nombre ? [...pila, normal.nombre] : pila;
    for (const [k, v] of Object.entries(es.properties ?? {})) {
      resultado[k] = ejemplo(v, componentes, { clave: k, profundidad: profundidad + 1, pila: pilaHija });
    }
    if (!es.properties && es.additionalProperties && typeof es.additionalProperties === 'object') {
      resultado.clave = ejemplo(es.additionalProperties, componentes, { clave: 'valor', profundidad: profundidad + 1, pila });
    }
    return resultado;
  }
  if (tipo === 'integer' || tipo === 'number') return es.minimum !== undefined ? es.minimum : 1;
  if (tipo === 'boolean') return true;
  if (tipo === 'string') {
    switch (es.format) {
      case 'uuid':
        return UUID_EJEMPLO;
      case 'date-time':
        return '2026-11-12T15:00:00Z';
      case 'date':
        return '2026-11-12';
      case 'time':
        return '15:00:00';
      case 'uri':
        return 'https://ejemplo.co/recurso';
      case 'email':
        return 'contacto@empresa.co';
      default:
        return /id$/i.test(clave) && clave.length > 2 ? UUID_EJEMPLO : textoDeEjemplo(clave);
    }
  }
  return null;
}
