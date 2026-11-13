import { existsSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  ARCO_DE_CARGA,
  ARO_ABIERTO,
  CAJA_V,
  CHECK_RELLENO,
  coloresDeLogo,
  FAVICON,
  GUIA_CHECK,
  LONGITUD_GUIA,
  MARGEN_V,
  TIEMPOS_CARGA,
  TRANSFORMACION_CHECK_ICONO,
} from '../packages/ui/src/marca/geometria';

/**
 * Sincronía de la marca (MA-1): los recursos que el generador deja en `marca/` y las copias que usan las apps, los correos y el PDF no se
 * pueden separar de la geometría de `@validasoft/ui` ni entre sí. El generador necesita un navegador; estas pruebas solo leen archivos.
 */
const repo = resolve(import.meta.dirname, '..');
const leer = (...ruta: string[]) => readFileSync(join(repo, ...ruta));
const texto = (...ruta: string[]) => leer(...ruta).toString('utf8');
const dimensionesPng = (png: Buffer) => ({ ancho: png.readUInt32BE(16), alto: png.readUInt32BE(20) });

describe('recursos de marca generados', () => {
  it('los PNG tienen el tamaño de su nombre', () => {
    const esperados: Record<string, number> = { 'favicon-16.png': 16, 'favicon-32.png': 32, 'favicon-48.png': 48, 'apple-touch-icon.png': 180, 'icon-192.png': 192, 'icon-512.png': 512, 'icon-maskable-512.png': 512, 'avatar-512.png': 512 };
    for (const [nombre, lado] of Object.entries(esperados)) {
      const png = leer('marca', 'png', nombre);
      expect(png.subarray(1, 4).toString('ascii'), nombre).toBe('PNG');
      expect(dimensionesPng(png), nombre).toEqual({ ancho: lado, alto: lado });
    }
  });

  it('los logotipos PNG están al doble de su tamaño (el correo los muestra a la mitad)', () => {
    for (const modo of ['claro', 'oscuro']) {
      const { ancho, alto } = dimensionesPng(leer('marca', 'png', `logotipo-${modo}@2x.png`));
      expect(ancho % 2).toBe(0);
      expect(alto % 2).toBe(0);
      expect(ancho / alto).toBeGreaterThan(3);
    }
  });

  it('los GIF son GIF89a animados, sin fin, de 2× el tamaño de la escena y de peso razonable', () => {
    for (const nombre of ['exito-claro', 'exito-oscuro', 'cargando-claro', 'cargando-oscuro']) {
      const gif = leer('marca', 'gif', `${nombre}.gif`);
      expect(gif.subarray(0, 6).toString('ascii'), nombre).toBe('GIF89a');
      expect(gif.readUInt16LE(6), nombre).toBe(gif.readUInt16LE(8)); // cuadrado
      expect(gif.includes(Buffer.from('NETSCAPE2.0')), nombre).toBe(true);
      expect(gif.length, nombre).toBeLessThan(150_000);
      expect(gif.at(-1)).toBe(0x3b);
    }
  });

  it('los SVG llevan la geometría aprobada y los colores de cada modo', () => {
    for (const [nombre, modo] of [['claro', 'light'], ['oscuro', 'dark']] as const) {
      const c = coloresDeLogo(modo);
      const logotipo = texto('marca', 'svg', `logotipo-${nombre}.svg`);
      expect(logotipo).toContain(CHECK_RELLENO);
      expect(logotipo).toContain(`translate(${String(-CAJA_V.x)} ${String(-CAJA_V.y)})`);
      for (const color of [c.check, c.texto, c.soft]) expect(logotipo).toContain(color);
      const icono = texto('marca', 'svg', `icono-${nombre}.svg`);
      expect(icono).toContain(ARO_ABIERTO);
      expect(icono).toContain(CHECK_RELLENO);
      expect(icono).toContain(TRANSFORMACION_CHECK_ICONO);
      expect(icono).toContain(c.aro);
      expect(icono).toContain(c.check);
    }
  });

  it('el favicon SVG es el círculo verde con el check blanco y cambia de verde con el modo oscuro del sistema', () => {
    const svg = texto('marca', 'svg', 'favicon.svg');
    expect(svg).toContain(CHECK_RELLENO);
    expect(svg).toContain(FAVICON.transformacion);
    expect(svg).toContain(`r="${String(FAVICON.radio)}"`);
    expect(svg).toContain(coloresDeLogo('light').check);
    expect(svg).toMatch(/@media \(prefers-color-scheme: dark\)[^}]*\{[^}]*#2BE29A/);
  });
});

describe('copias que usan el producto', () => {
  const comunes = ['favicon.svg', 'favicon-16.png', 'favicon-32.png', 'favicon-48.png', 'apple-touch-icon.png', 'icon-192.png', 'icon-512.png', 'icon-maskable-512.png'];

  it.each(['apps/web/public', 'apps/public/public'])('%s es idéntica a lo generado y trae su manifest', (carpeta) => {
    for (const nombre of comunes) {
      const origen = nombre.endsWith('.svg') ? ['marca', 'svg', nombre] : ['marca', 'png', nombre];
      expect(leer(...carpeta.split('/'), nombre).equals(leer(...origen)), `${carpeta}/${nombre}`).toBe(true);
    }
    const manifest = JSON.parse(texto(...carpeta.split('/'), 'manifest.webmanifest')) as { name: string; icons: { src: string; sizes: string; purpose: string }[]; display: string; lang: string };
    expect(manifest.lang).toBe('es-CO');
    expect(manifest.display).toBe('standalone');
    expect(manifest.icons.map((i) => i.purpose)).toContain('maskable');
    for (const icono of manifest.icons) expect(existsSync(join(repo, carpeta, icono.src)), icono.src).toBe(true);
  });

  it.each(['apps/web', 'apps/public'])('el index.html de %s enlaza el favicon, los íconos y el manifest y lleva la carga de marca', (carpeta) => {
    const html = texto(...carpeta.split('/'), 'index.html');
    for (const enlace of ['/favicon.svg', '/favicon-32.png', '/favicon-16.png', '/apple-touch-icon.png', '/manifest.webmanifest']) expect(html, enlace).toContain(`href="${enlace}"`);
    // La carga de marca (entre las marcas del generador) usa la misma geometría y los mismos tiempos que `Cargando`.
    const carga = /<!-- marca:splash:inicio -->([\s\S]*)<!-- marca:splash:fin -->/.exec(html)?.[1] ?? '';
    expect(carga).toContain(CHECK_RELLENO);
    expect(carga).toContain(GUIA_CHECK);
    expect(carga).toContain(TRANSFORMACION_CHECK_ICONO);
    expect(carga).toContain(`stroke-dasharray="${String(ARCO_DE_CARGA.visible)} ${String(ARCO_DE_CARGA.hueco)}"`);
    expect(carga).toContain(`vs-girar ${String(TIEMPOS_CARGA.vuelta)}ms linear infinite`);
    expect(carga).toContain(`vs-trazar ${String(TIEMPOS_CARGA.check)}ms ease-in-out infinite`);
    expect(carga).toContain(`stroke-dasharray: ${String(LONGITUD_GUIA)} ${String(LONGITUD_GUIA)}`);
    expect(carga).toContain('role="status"');
    expect(carga).toContain('prefers-reduced-motion: reduce');
  });

  it('los correos incrustan el logotipo claro y el GIF de éxito claro que genera la marca', () => {
    const recursos = ['src', 'Validasoft.Aplicacion', 'Comun', 'RecursosDeMarca'];
    expect(leer(...recursos, 'logotipo.png').equals(leer('marca', 'png', 'logotipo-claro@2x.png'))).toBe(true);
    expect(leer(...recursos, 'exito.gif').equals(leer('marca', 'gif', 'exito-claro.gif'))).toBe(true);
  });

  it('el PDF dibuja la misma «V», con el mismo verde, caja y margen que el logotipo', () => {
    const pdf = texto('src', 'Validasoft.Infraestructura', 'Ficha', 'LogosDePdf.cs');
    expect(pdf).toContain(CHECK_RELLENO);
    expect(pdf).toContain(`viewBox="${String(CAJA_V.x)} ${String(CAJA_V.y)} ${String(CAJA_V.ancho)} ${String(CAJA_V.alto)}"`);
    expect(pdf).toContain(`fill="${coloresDeLogo('light').check}"`);
    expect(pdf).toContain(`MargenDeLaV = ${String(MARGEN_V)};`);
    expect(pdf).toContain(`${String(CAJA_V.ancho)}.0 / ${String(CAJA_V.alto)}`);
    // Los colores de «alida» y «Soft» (#0A0F1C y #2F55D9) como los repite el PDF.
    expect(pdf).toContain('(10 / 255.0, 15 / 255.0, 28 / 255.0)');
    expect(pdf).toContain('(47 / 255.0, 85 / 255.0, 217 / 255.0)');
    expect(coloresDeLogo('light')).toMatchObject({ texto: '#0A0F1C', soft: '#2F55D9' });
  });
});
