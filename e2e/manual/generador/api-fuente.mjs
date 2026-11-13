// Lo que el contrato OpenAPI no trae pero el código de los endpoints sí: la descripción (comentario XML), el permiso (`[Authorize]`), si exige
// la empresa activa (`X-Empresa`) y los códigos de error de negocio. Se lee con expresiones regulares: sin compilar y sin ningún modelo.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

const FALLAS = {
  Validacion: 422,
  NoEncontrado: 404,
  Conflicto: 409,
  Prohibido: 403,
  NoAutenticado: 401,
  Expirado: 410,
  DemasiadosIntentos: 429,
  NoDisponible: 503,
};

const RUTA_WOLVERINE = /Wolverine(Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"/;

/** `{id:guid}` → `{id}` (el contrato OpenAPI no lleva restricciones). */
export const normalizarRuta = (ruta) => ruta.replace(/\{(\w+)(:[^}]*)?\??\}/g, '{$1}');

const entidades = { '&lt;': '<', '&gt;': '>', '&amp;': '&', '&quot;': '"', '&apos;': "'" };

/** Comentario XML a texto plano (con `código` y **negrita** de Markdown). */
export function xmlATexto(xml) {
  return xml
    .replace(/<c>([\s\S]*?)<\/c>/g, '`$1`')
    .replace(/<code>([\s\S]*?)<\/code>/g, '`$1`')
    .replace(/<b>([\s\S]*?)<\/b>/g, '**$1**')
    .replace(/<see\s+cref="([^"]+)"\s*\/>/g, (_, c) => `\`${c.split('.').pop()}\``)
    .replace(/<see\s+langword="([^"]+)"\s*\/>/g, '`$1`')
    .replace(/<paramref\s+name="([^"]+)"\s*\/>/g, '`$1`')
    .replace(/<\/?(para|remarks|i|em|strong|list|item|description)[^>]*>/g, ' ')
    .replace(/<[^>]+>/g, '')
    .replace(/&(lt|gt|amp|quot|apos);/g, (m) => entidades[m])
    .replace(/\s+/g, ' ')
    .trim();
}

function datosDelComentario(lineas) {
  const texto = lineas.join(' ');
  const resumen = /<summary>([\s\S]*?)<\/summary>/.exec(texto)?.[1];
  const parametros = {};
  for (const m of texto.matchAll(/<param\s+name="(\w+)">([\s\S]*?)<\/param>/g)) parametros[m[1]] = xmlATexto(m[2]);
  return { resumen: resumen ? xmlATexto(resumen) : null, parametros };
}

/** Cómo se autoriza un grupo de atributos: `{ tipo, politica }` o `null` si no dice nada. */
export function accesoDeAtributos(atributos) {
  if (/\bAllowAnonymous\b/.test(atributos)) return { tipo: 'publico', politica: null };
  const politica = /Authorize\(\s*Policy\s*=\s*([\w.]+)\s*\)/.exec(atributos)?.[1];
  if (politica) return { tipo: 'politica', politica };
  if (/\bAuthorize\b/.test(atributos)) return { tipo: 'sesion', politica: null };
  return null;
}

/** Cuerpo del método que empieza en la línea `desde`: hasta la llave que cierra o el `;` de un método con `=>`. */
function cuerpoDelMetodo(lineas, desde) {
  const texto = lineas.slice(desde, desde + 400).join('\n');
  let profundidadParentesis = 0;
  let visto = false;
  let i = 0;
  for (; i < texto.length; i++) {
    const c = texto[i];
    if (c === '(') {
      profundidadParentesis++;
      visto = true;
    } else if (c === ')') {
      profundidadParentesis--;
      if (visto && profundidadParentesis === 0) {
        i++;
        break;
      }
    }
  }
  const firma = texto.slice(0, i);
  const resto = texto.slice(i);
  const flecha = resto.indexOf('=>');
  const llave = resto.indexOf('{');
  if (flecha >= 0 && (llave < 0 || flecha < llave)) {
    const fin = resto.indexOf(';', flecha);
    return { firma, cuerpo: resto.slice(flecha, fin < 0 ? resto.length : fin) };
  }
  if (llave < 0) return { firma, cuerpo: '' };
  let profundidad = 0;
  for (let j = llave; j < resto.length; j++) {
    if (resto[j] === '{') profundidad++;
    else if (resto[j] === '}') {
      profundidad--;
      if (profundidad === 0) return { firma, cuerpo: resto.slice(llave, j + 1) };
    }
  }
  return { firma, cuerpo: resto.slice(llave) };
}

/** Códigos de error de negocio que el cuerpo lanza directamente, con su estado HTTP. */
export function erroresDelCuerpo(cuerpo) {
  const errores = new Map();
  for (const m of cuerpo.matchAll(/ReglaDeNegocioException\s*\.\s*(\w+)\(\s*\$?"([a-z0-9-]+)"/g)) {
    const estado = FALLAS[m[1]];
    if (estado) errores.set(`${String(estado)} ${m[2]}`, { estado, codigo: m[2] });
  }
  if (/ReglaDeNegocioException\s*\.\s*DemasiadoGrande\(/.test(cuerpo)) errores.set('413 archivo-demasiado-grande', { estado: 413, codigo: 'archivo-demasiado-grande' });
  if (/ReglaDeNegocioException\s*\.\s*TipoNoSoportado\(/.test(cuerpo)) errores.set('415 tipo-archivo-no-soportado', { estado: 415, codigo: 'tipo-archivo-no-soportado' });
  return [...errores.values()].sort((a, b) => a.estado - b.estado || a.codigo.localeCompare(b.codigo));
}

/**
 * Analiza un archivo C# de endpoints. Devuelve `{ clase, resumenDeClase, endpoints }` (varios por archivo) o `null` si no tiene ninguno.
 * Cada endpoint: `{ metodo, ruta, resumen, parametros, acceso, alcanceEmpresa, errores }`.
 */
export function analizarCSharp(texto) {
  const lineas = texto.split(/\r?\n/);
  const espacio = /^\s*namespace\s+([\w.]+)/m.exec(texto)?.[1] ?? '';
  let doc = [];
  let atributos = [];
  let clase = null;
  let resumenDeClase = null;
  let accesoDeClase = null;
  const endpoints = [];

  for (let i = 0; i < lineas.length; i++) {
    const t = lineas[i].trim();
    if (t.startsWith('///')) {
      doc.push(t.slice(3).trim());
      continue;
    }
    if (t.startsWith('[')) {
      atributos.push(t);
      continue;
    }
    if (t === '') continue;
    const declaraClase = /^(?:public|internal)\s+(?:static\s+)?(?:partial\s+)?class\s+(\w+)/.exec(t);
    if (declaraClase) {
      clase = declaraClase[1];
      resumenDeClase = datosDelComentario(doc).resumen;
      accesoDeClase = accesoDeAtributos(atributos.join(' '));
      doc = [];
      atributos = [];
      continue;
    }
    const ruta = RUTA_WOLVERINE.exec(atributos.join(' '));
    if (ruta) {
      const { firma, cuerpo } = cuerpoDelMetodo(lineas, i);
      const datos = datosDelComentario(doc);
      endpoints.push({
        metodo: ruta[1].toUpperCase(),
        ruta: normalizarRuta(ruta[2]),
        resumen: datos.resumen,
        parametros: datos.parametros,
        acceso: accesoDeAtributos(atributos.join(' ')) ?? accesoDeClase,
        alcanceEmpresa: /\bSesionDeEmpresa\b/.test(firma),
        errores: erroresDelCuerpo(cuerpo),
      });
    }
    doc = [];
    atributos = [];
  }
  if (endpoints.length === 0) return null;
  return { clase: espacio ? `${espacio}.${clase ?? ''}` : (clase ?? ''), resumenDeClase, endpoints };
}

/** Valores de `public const string X = "valor"` de `Permisos.cs`: `Permisos.X` → `valor`. */
export function leerPermisos(texto) {
  const permisos = new Map();
  for (const m of texto.matchAll(/public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"/g)) permisos.set(`Permisos.${m[1]}`, m[2]);
  return permisos;
}

function archivosCs(carpeta, acumulado = []) {
  for (const nombre of readdirSync(carpeta)) {
    if (nombre === 'bin' || nombre === 'obj' || nombre === 'node_modules') continue;
    const ruta = join(carpeta, nombre);
    if (statSync(ruta).isDirectory()) archivosCs(ruta, acumulado);
    else if (nombre.endsWith('.cs')) acumulado.push(ruta);
  }
  return acumulado;
}

/**
 * Lee los endpoints de `src/Validasoft.Aplicacion` (y los permisos). Devuelve `{ endpoints: Map('MÉTODO ruta' → endpoint con clase), clases, permisos }`.
 * Si la carpeta no existe devuelve mapas vacíos: la referencia se genera igual, solo sin descripciones ni permisos.
 */
export function leerFuentes(raizSrc) {
  const resultado = { endpoints: new Map(), clases: new Map(), permisos: new Map() };
  let archivos;
  try {
    archivos = archivosCs(raizSrc);
  } catch {
    return resultado;
  }
  for (const archivo of archivos) {
    const texto = readFileSync(archivo, 'utf8');
    if (/class\s+Permisos\b/.test(texto)) for (const [k, v] of leerPermisos(texto)) resultado.permisos.set(k, v);
    if (!texto.includes('Wolverine')) continue;
    const analizado = analizarCSharp(texto);
    if (!analizado) continue;
    resultado.clases.set(analizado.clase, analizado.resumenDeClase);
    for (const e of analizado.endpoints) resultado.endpoints.set(`${e.metodo} ${e.ruta}`, { ...e, clase: analizado.clase });
  }
  return resultado;
}

/** Texto legible del acceso de un endpoint: `{ etiqueta, scope }`. */
export function describirAcceso(acceso, permisos) {
  if (!acceso) return { etiqueta: null, scope: null };
  if (acceso.tipo === 'publico') return { etiqueta: 'Pública (sin sesión)', scope: null };
  if (acceso.tipo === 'sesion') return { etiqueta: 'Cualquier sesión autenticada', scope: null };
  if (acceso.politica === 'Permisos.PlataformaTenants') return { etiqueta: 'Operador de Plataforma', scope: 'plataforma:tenants' };
  if (/ReclamosTercero/.test(acceso.politica)) return { etiqueta: 'Sesión del tercero', scope: null };
  const scope = permisos.get(acceso.politica) ?? null;
  return { etiqueta: scope ? `Permiso ${scope}` : acceso.politica, scope };
}
