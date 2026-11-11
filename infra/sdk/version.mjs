// Versión única de los SDK (docs/08 §2.3): Validasoft.Contratos, Validasoft.Sdk (NuGet) y @validasoft/sdk (npm) salen juntos con la
// misma versión semántica, que vive en dos sitios (src/Empaquetado.props y packages/sdk/package.json) y en la cabecera del CHANGELOG.
//
//   node infra/sdk/version.mjs                    muestra la versión actual
//   node infra/sdk/version.mjs 1.2.0              la fija en los dos manifiestos (el CHANGELOG lo escribe una persona)
//   node infra/sdk/version.mjs --verificar sdk-v1.2.0
//                                                 falla (código 1) si el tag, los manifiestos y el CHANGELOG no dicen lo mismo
//   node infra/sdk/version.mjs --notas 1.2.0      imprime las notas de esa versión (su entrada del CHANGELOG)
//
// Sin dependencias: solo Node.
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const propiedades = join(raiz, 'src', 'Empaquetado.props');
const paquete = join(raiz, 'packages', 'sdk', 'package.json');
const cambiosNet = join(raiz, 'src', 'Validasoft.Sdk', 'CHANGELOG.md');
const cambiosNpm = join(raiz, 'packages', 'sdk', 'CHANGELOG.md');

const SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;
const PATRON_PROPIEDAD = /<VersionSdk>([^<]*)<\/VersionSdk>/;

const escapar = (version) => version.replace(/[.+*?^${}()|[\]\\]/g, '\\$&');

export function versionNet() {
  const coincidencia = PATRON_PROPIEDAD.exec(readFileSync(propiedades, 'utf8'));
  if (!coincidencia?.[1]) throw new Error('src/Empaquetado.props no tiene <VersionSdk>.');
  return coincidencia[1].trim();
}

export function versionNpm() {
  return JSON.parse(readFileSync(paquete, 'utf8')).version;
}

export function fijar(version) {
  if (!SEMVER.test(version)) throw new Error(`«${version}» no es una versión semántica (x.y.z o x.y.z-prerelease).`);
  writeFileSync(propiedades, readFileSync(propiedades, 'utf8').replace(PATRON_PROPIEDAD, `<VersionSdk>${version}</VersionSdk>`));
  const manifiesto = JSON.parse(readFileSync(paquete, 'utf8'));
  manifiesto.version = version;
  writeFileSync(paquete, `${JSON.stringify(manifiesto, null, 2)}\n`);
}

/** Problemas que impiden publicar `tag` (vacío = todo en orden). */
export function verificar(tag) {
  const problemas = [];
  const coincidencia = /^sdk-v(.+)$/.exec(tag);
  if (!coincidencia) return [`El tag «${tag}» no tiene la forma sdk-v<versión> (p. ej. sdk-v1.2.0).`];
  const version = coincidencia[1];
  if (!SEMVER.test(version)) problemas.push(`«${version}» no es una versión semántica.`);
  if (versionNet() !== version) problemas.push(`src/Empaquetado.props dice ${versionNet()} y el tag ${version}: corre «node infra/sdk/version.mjs ${version}».`);
  if (versionNpm() !== version) problemas.push(`packages/sdk/package.json dice ${versionNpm()} y el tag ${version}: corre «node infra/sdk/version.mjs ${version}».`);
  for (const archivo of [cambiosNet, cambiosNpm]) {
    if (!new RegExp(`^## \\[${escapar(version)}\\]`, 'm').test(readFileSync(archivo, 'utf8')))
      problemas.push(`${archivo.slice(raiz.length + 1)} no tiene la entrada «## [${version}]».`);
  }
  return problemas;
}

/** Notas de la versión: el texto de su entrada en el CHANGELOG de npm (idéntica a la de NuGet). */
export function notas(version) {
  const texto = readFileSync(cambiosNpm, 'utf8');
  const inicio = new RegExp(`^## \\[${escapar(version)}\\].*$`, 'm').exec(texto);
  if (!inicio) return '';
  const resto = texto.slice(inicio.index + inicio[0].length);
  const fin = resto.search(/^## \[/m);
  return (fin === -1 ? resto : resto.slice(0, fin)).trim();
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [argumento, valor] = process.argv.slice(2);
  try {
    if (argumento === '--verificar') {
      const problemas = verificar(valor ?? '');
      if (problemas.length > 0) {
        for (const p of problemas) console.error(`✗ ${p}`);
        process.exit(1);
      }
      console.log(`✓ ${valor}: manifiestos y CHANGELOG coinciden.`);
    } else if (argumento === '--notas') {
      console.log(notas(valor ?? ''));
    } else if (argumento) {
      fijar(argumento);
      console.log(`Versión ${argumento} en src/Empaquetado.props y packages/sdk/package.json. Falta la entrada «## [${argumento}]» en los dos CHANGELOG.`);
    } else {
      console.log(`NuGet ${versionNet()} · npm ${versionNpm()}`);
    }
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exit(1);
  }
}
