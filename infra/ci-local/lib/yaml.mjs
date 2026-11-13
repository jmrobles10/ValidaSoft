// Lector de YAML mínimo, solo lo que usan `.github/workflows/ci.yml`, `.github/workflows/e2e.yml` e `infra/compose/compose.yaml`: mapas y listas por
// indentación, escalares planos o entre comillas, listas de una línea (`[a, b]`) y bloques `|` y `>` (con `-`/`+`).
// Todo escalar queda como texto (o `null` si está vacío). Cualquier construcción que no entienda lanza un error en vez de
// leerla mal: así un cambio de formato en el flujo rompe la réplica local a la vista (y la prueba de sincronía), no en silencio.
//
// Sin dependencias: la réplica local debe poder correr en una máquina que solo tenga Node y Docker.

/** @param {string} texto @returns {unknown} */
export function leerYaml(texto) {
  const lineas = texto.replace(/\r\n/g, '\n').split('\n');
  let pos = 0;

  const error = (mensaje) => new Error(`YAML no soportado (línea ${String(pos + 1)}): ${mensaje}\n  ${lineas[pos] ?? ''}`);
  const sangria = (linea) => linea.length - linea.trimStart().length;
  const esVacia = (linea) => linea.trim() === '' || linea.trimStart().startsWith('#');
  const esItem = (contenido) => contenido.startsWith('- ') || contenido === '-';

  function saltarVacias() {
    while (pos < lineas.length && esVacia(lineas[pos])) pos++;
  }

  /** Quita un comentario final (` # …`) de un escalar plano o de una línea con comillas. */
  function sinComentario(valor) {
    let comilla = null;
    for (let i = 0; i < valor.length; i++) {
      const c = valor[i];
      if (comilla) {
        if (c === '\\' && comilla === '"') i++;
        else if (c === comilla) comilla = null;
      } else if (c === '"' || c === "'") {
        if (i === 0) comilla = c;
      } else if (c === '#' && (i === 0 || /\s/.test(valor[i - 1]))) {
        return valor.slice(0, i).trimEnd();
      }
    }
    return valor.trimEnd();
  }

  function escalar(valor) {
    const v = sinComentario(valor.trim());
    if (v === '') return null;
    if (v.startsWith('"')) {
      if (!v.endsWith('"') || v.length < 2) throw error('cadena con comillas dobles sin cerrar en una sola línea');
      return v.slice(1, -1).replace(/\\(["\\nt/])/g, (_, c) => ({ n: '\n', t: '\t' })[c] ?? c);
    }
    if (v.startsWith("'")) {
      if (!v.endsWith("'") || v.length < 2) throw error('cadena con comillas simples sin cerrar en una sola línea');
      return v.slice(1, -1).replace(/''/g, "'");
    }
    if (v.startsWith('[')) {
      if (!v.endsWith(']')) throw error('lista de una línea sin cerrar');
      const interior = v.slice(1, -1).trim();
      return interior === '' ? [] : interior.split(',').map((x) => escalar(x));
    }
    if (v.startsWith('{') || v.startsWith('&') || v.startsWith('*') || v.startsWith('!')) {
      throw error('mapas de una línea, anclas y etiquetas no están soportados');
    }
    return v;
  }

  /** Bloque `|` o `>` cuya cabecera ya se consumió (la línea anterior a `pos`). */
  function bloqueDeTexto(cabecera, sangriaPadre) {
    const m = /^([|>])([+-]?)(\d*)$/.exec(cabecera);
    if (!m) throw error(`cabecera de bloque desconocida «${cabecera}»`);
    const estilo = m[1];
    const chomp = m[2];
    const filas = [];
    let sangriaBloque = null;
    while (pos < lineas.length) {
      const linea = lineas[pos];
      if (linea.trim() === '') {
        filas.push('');
        pos++;
        continue;
      }
      const s = sangria(linea);
      if (s <= sangriaPadre) break;
      sangriaBloque ??= s;
      if (s < sangriaBloque) throw error('el bloque tiene una línea con menos sangría que su primera línea');
      filas.push(linea.slice(sangriaBloque));
      pos++;
    }
    while (filas.length > 0 && filas[filas.length - 1] === '') filas.pop();
    let texto;
    if (estilo === '|') {
      texto = filas.join('\n');
    } else {
      // Plegado: las líneas consecutivas se unen con un espacio; una línea en blanco es un salto; las que empiezan con espacio se conservan.
      texto = '';
      for (let i = 0; i < filas.length; i++) {
        const fila = filas[i];
        if (i === 0) texto = fila;
        else if (fila === '') texto += '\n';
        else if (filas[i - 1] === '') texto += fila;
        else if (/^\s/.test(fila) || /^\s/.test(filas[i - 1])) texto += '\n' + fila;
        else texto += ' ' + fila;
      }
    }
    if (chomp === '+') return texto + '\n';
    return chomp === '-' ? texto : texto + '\n';
  }

  function valorDe(resto, sangriaClave) {
    const r = sinComentario(resto.trim());
    if (r === '') {
      saltarVacias();
      if (pos >= lineas.length) return null;
      const s = sangria(lineas[pos]);
      if (s > sangriaClave || (s === sangriaClave && esItem(lineas[pos].trimStart()))) return bloque(s);
      return null;
    }
    if (r.startsWith('|') || r.startsWith('>')) return bloqueDeTexto(r, sangriaClave);
    return escalar(r);
  }

  const REGEX_CLAVE = /^("[^"]+"|'[^']+'|[^\s:#'"[{][^:]*?)\s*:(?:\s+(.*))?$/;

  function mapa(indent) {
    const resultado = {};
    for (;;) {
      saltarVacias();
      if (pos >= lineas.length) break;
      const linea = lineas[pos];
      const s = sangria(linea);
      if (s < indent) break;
      if (s > indent) throw error('sangría inesperada en un mapa');
      const contenido = linea.trimStart();
      if (esItem(contenido)) break;
      const m = REGEX_CLAVE.exec(contenido);
      if (!m) throw error('se esperaba «clave: valor»');
      const clave = m[1].replace(/^["']|["']$/g, '');
      pos++;
      resultado[clave] = valorDe(m[2] ?? '', indent);
    }
    return resultado;
  }

  function lista(indent) {
    const resultado = [];
    for (;;) {
      saltarVacias();
      if (pos >= lineas.length) break;
      const linea = lineas[pos];
      const s = sangria(linea);
      if (s < indent) break;
      if (s > indent) throw error('sangría inesperada en una lista');
      const contenido = linea.trimStart();
      if (!esItem(contenido)) break;
      const resto = contenido === '-' ? '' : contenido.slice(2);
      if (resto.trim() === '') {
        pos++;
        saltarVacias();
        resultado.push(pos < lineas.length && sangria(lineas[pos]) > indent ? bloque(sangria(lineas[pos])) : null);
      } else if (REGEX_CLAVE.test(resto.trim()) && !/^["'].*["']$/.test(resto.trim())) {
        // «- clave: valor»: el elemento es un mapa cuyas demás claves siguen en la misma columna que la primera.
        const desplazamiento = contenido.length - resto.length;
        lineas[pos] = ' '.repeat(indent + desplazamiento) + resto;
        resultado.push(mapa(indent + desplazamiento));
      } else {
        pos++;
        resultado.push(escalar(resto));
      }
    }
    return resultado;
  }

  function bloque(indent) {
    saltarVacias();
    return esItem(lineas[pos].trimStart()) ? lista(indent) : mapa(indent);
  }

  saltarVacias();
  if (pos >= lineas.length) return null;
  const raiz = bloque(sangria(lineas[pos]));
  saltarVacias();
  if (pos < lineas.length) throw error('contenido sin interpretar al final del documento');
  return raiz;
}
