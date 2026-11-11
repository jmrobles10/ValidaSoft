// Cuentas de las pruebas a partir de la salida de cada herramienta (dotnet test, vitest, Playwright), para el resumen corto del final.
const sinColor = (texto) => texto.replace(/\u001b\[[0-9;]*[A-Za-z]/g, '');

/** `dotnet test`: una línea «Passed!/Failed! - Failed: n, Passed: n, Skipped: n, Total: n … - Ensamblado.dll» por proyecto de pruebas. */
export function cuentasDotnet(salida) {
  const proyectos = [];
  const regex = /(?:Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+),[^\n]*?-\s+(\S+?)\.dll/g;
  for (const m of sinColor(salida).matchAll(regex)) {
    proyectos.push({ proyecto: m[5], fallan: Number(m[1]), pasan: Number(m[2]), omitidas: Number(m[3]), total: Number(m[4]) });
  }
  if (proyectos.length === 0) return null;
  const suma = (campo) => proyectos.reduce((a, p) => a + p[campo], 0);
  return { proyectos, fallan: suma('fallan'), pasan: suma('pasan'), omitidas: suma('omitidas'), total: suma('total') };
}

/** `pnpm test` (vitest): «Test Files  n passed (n)» y «Tests  n failed | n passed | n skipped (n)». */
export function cuentasVitest(salida) {
  const texto = sinColor(salida);
  const archivos = /Test Files\s+(?:(\d+) failed \| )?(\d+) passed(?: \| (\d+) skipped)?/.exec(texto);
  const pruebas = /^\s*Tests\s+(?:(\d+) failed \| )?(\d+) passed(?: \| (\d+) skipped)?/m.exec(texto);
  if (!pruebas) return null;
  return {
    archivos: archivos ? Number(archivos[2]) + Number(archivos[1] ?? 0) + Number(archivos[3] ?? 0) : null,
    pasan: Number(pruebas[2]),
    fallan: Number(pruebas[1] ?? 0),
    omitidas: Number(pruebas[3] ?? 0),
  };
}

/** Playwright (reporter de lista): al final «n passed (3.1m)», «n failed», «n flaky», «n skipped», «n did not run». */
export function cuentasPlaywright(salida) {
  const cuentas = { pasan: 0, fallan: 0, inestables: 0, omitidas: 0, sinCorrer: 0 };
  const llaves = { passed: 'pasan', failed: 'fallan', flaky: 'inestables', skipped: 'omitidas', 'did not run': 'sinCorrer', interrupted: 'fallan' };
  let hay = false;
  for (const m of sinColor(salida).matchAll(/^\s+(\d+) (passed|failed|flaky|skipped|did not run|interrupted)\b/gm)) {
    cuentas[llaves[m[2]]] += Number(m[1]);
    hay = true;
  }
  return hay ? cuentas : null;
}

/** Texto corto para el resumen de un paso, según el comando que corrió. */
export function detalleDePaso(script, salida) {
  if (/^dotnet test\b/m.test(script)) {
    const c = cuentasDotnet(salida);
    if (!c) return null;
    return `${String(c.total)} pruebas en ${String(c.proyectos.length)} proyectos: ${String(c.pasan)} pasan, ${String(c.fallan)} fallan, ${String(c.omitidas)} omitidas`;
  }
  if (/^pnpm test\b/m.test(script)) {
    const c = cuentasVitest(salida);
    if (!c) return null;
    return `${String(c.pasan + c.fallan + c.omitidas)} pruebas${c.archivos === null ? '' : ` en ${String(c.archivos)} archivos`}: ${String(c.pasan)} pasan, ${String(c.fallan)} fallan, ${String(c.omitidas)} omitidas`;
  }
  return null;
}

/** Playwright va en un `docker run`, no en el script: se lee aparte. */
export function detalleDePlaywright(salida) {
  const c = cuentasPlaywright(salida);
  if (!c) return null;
  return `${String(c.pasan + c.fallan + c.inestables)} pruebas: ${String(c.pasan)} pasan, ${String(c.fallan)} fallan, ${String(c.inestables)} inestables, ${String(c.omitidas)} omitidas`;
}

export function aSegundos(ms) {
  const s = Math.round(ms / 1000);
  return s < 60 ? `${String(s)} s` : `${String(Math.floor(s / 60))} min ${String(s % 60).padStart(2, '0')} s`;
}

/** Nombres de lo que falló (pruebas de dotnet, vitest o Playwright), sin repetir. */
export function fallasDe(salida) {
  const texto = sinColor(salida);
  const nombres = new Set();
  for (const m of texto.matchAll(/^\s*Failed (\S+)(?: \[[^\]]*\])?\s*$/gm)) nombres.add(m[1]);
  for (const m of texto.matchAll(/^\s*FAIL\s+(.+)$/gm)) nombres.add(m[1].trim());
  for (const m of texto.matchAll(/^\s+\d+\) (\[.+)$/gm)) nombres.add(m[1].trim());
  return [...nombres];
}

/** Lo que se imprime de un paso en rojo: qué falló (hasta 8) y el final de la salida. */
export function resumenDeFalla(salida, lineasFinales = 15) {
  const fallas = fallasDe(salida);
  const partes = [];
  if (fallas.length > 0) {
    partes.push(`${String(fallas.length)} falla(s):`, ...fallas.slice(0, 8).map((f) => `  - ${f.slice(0, 200)}`));
    if (fallas.length > 8) partes.push(`  … y ${String(fallas.length - 8)} más (están en el log)`);
    partes.push('');
  }
  // Sin las anotaciones `::notice`/`::error` de GitHub Actions (ruido en un terminal).
  const limpio = sinColor(salida)
    .trimEnd()
    .split('\n')
    .filter((l) => !/^::(notice|error|warning|group|endgroup)/.test(l));
  partes.push(limpio.slice(-lineasFinales).join('\n'));
  return partes.join('\n');
}
