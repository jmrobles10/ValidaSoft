import { ErrorApi } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { mensajeFallo, mensajeProblema } from '../boveda/mensajesSubida';
import { REGLAS_GLOBALES } from '../boveda/subida';
import i18n from '../i18n';
import { aceptaDe, aceptaFotos, enumerarFormatos, modoDe, reglasDe, sonPropias, textoReglas } from './reglas';

const t = i18n.t.bind(i18n);
const MB = 1024 * 1024;
const soloPdf = reglasDe({ formatos: ['application/pdf'], tamanoMaximoBytes: 5 * MB, paginasMaximas: 10, descripcion: 'Certificado Norte admite solo PDF de hasta 5 MB' });

describe('reglas de archivo del tipo de documento', () => {
  it('sin modo se entrega un archivo; los modos con campos se respetan', () => {
    expect(modoDe(null)).toBe('Archivo');
    expect(modoDe({ modo: null })).toBe('Archivo');
    expect(modoDe({ modo: 'Formulario' })).toBe('Formulario');
    expect(modoDe({ modo: 'ArchivoYFormulario' })).toBe('ArchivoYFormulario');
    expect(modoDe({ modo: 'Otro' })).toBe('Archivo');
  });

  it('toma las reglas efectivas; sin reglas, o vacías, quedan las de la bóveda', () => {
    expect(soloPdf).toEqual({ formatos: ['application/pdf'], tamanoMaximoBytes: 5 * MB, paginasMaximas: 10 });
    expect(reglasDe(null)).toBe(REGLAS_GLOBALES);
    expect(reglasDe({ formatos: ['image/heic'], tamanoMaximoBytes: 0, paginasMaximas: 0, descripcion: '' })).toEqual(REGLAS_GLOBALES);
    expect(sonPropias(soloPdf)).toBe(true);
    expect(sonPropias(REGLAS_GLOBALES)).toBe(false);
  });

  it('dice las reglas en palabras del tercero', () => {
    expect(textoReglas(soloPdf, t)).toBe('Solo PDF de hasta 5 MB y 10 páginas');
    expect(textoReglas(REGLAS_GLOBALES, t)).toBe('PDF, JPG, PNG o WebP de hasta 20 MB y 50 páginas');
    expect(textoReglas({ formatos: ['image/jpeg', 'image/png'], tamanoMaximoBytes: 512 * 1024, paginasMaximas: 1 }, t)).toBe('JPG o PNG de hasta 512 KB');
    expect(textoReglas({ ...soloPdf, paginasMaximas: 1 }, t)).toBe('Solo PDF de hasta 5 MB y 1 página');
    expect(enumerarFormatos(['application/pdf', 'image/webp'])).toBe('PDF o WebP');
  });

  it('el selector solo ofrece los formatos del tipo, y sin imágenes no hay cámara', () => {
    expect(aceptaDe(soloPdf)).toBe('application/pdf,.pdf');
    expect(aceptaDe({ ...soloPdf, formatos: ['application/pdf', 'image/jpeg'] })).toBe('application/pdf,.pdf,image/jpeg,.jpg,.jpeg');
    expect(aceptaFotos(soloPdf)).toBeNull();
    expect(aceptaFotos(REGLAS_GLOBALES)).toBe('image/jpeg,image/png,image/webp');
  });
});

describe('mensajes de la subida con las reglas del tipo', () => {
  it('lo que revisa el navegador dice el límite del tipo', () => {
    expect(mensajeProblema({ tipo: 'demasiado-grande', tamano: 8 * MB, maximo: 5 * MB }, t)).toBe(
      'Este archivo pesa 8 MB. El máximo es 5 MB: comprímelo o divídelo e inténtalo de nuevo.',
    );
    expect(mensajeProblema({ tipo: 'formato', mime: 'image/jpeg', formatos: ['application/pdf'] }, t)).toBe('Este formato no se acepta para este documento. Sube un archivo PDF.');
  });

  it('un 413/415 de un tipo con reglas propias muestra su descripción; si no, el texto propio', () => {
    const e415 = new ErrorApi(415, 'tipo-archivo-no-soportado', 'Certificado Norte admite solo PDF de hasta 5 MB.', null, null);
    expect(mensajeFallo('formato', e415, t, soloPdf)).toBe('Certificado Norte admite solo PDF de hasta 5 MB. Elige un archivo que cumpla y vuelve a intentarlo.');
    const e413 = new ErrorApi(413, 'archivo-demasiado-grande', 'El archivo supera el máximo de 20 MB.', null, null);
    expect(mensajeFallo('demasiado-grande', e413, t)).toBe('El archivo supera el máximo de 20 MB. Comprímelo o divídelo e inténtalo de nuevo.');
  });

  it('los 422 de modo dicen qué hacer', () => {
    expect(mensajeFallo('otro', new ErrorApi(422, 'archivo-no-permitido', 'x', null, null), t)).toMatch(/llenando un formulario/);
    expect(mensajeFallo('otro', new ErrorApi(422, 'archivo-requerido', 'x', null, null), t)).toMatch(/pide también un archivo/);
  });
});
