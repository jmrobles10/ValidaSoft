import { ErrorApi, type ExencionDeVinculoDto } from '@validasoft/api-client';
import i18n from 'i18next';
import { describe, expect, it } from 'vitest';
import { admiteExencion, alcanceDe, aPeticion, cierreDe, errorDeExencion, exencionesVigentes, faltaAlEximir, formularioInicial, nombreDeObjetivo, resumenDeInsignia, vigenciaDe } from './modelo';

const t = i18n.t.bind(i18n);

function exencion(parcial: Partial<ExencionDeVinculoDto> = {}): ExencionDeVinculoDto {
  return {
    id: 'ex1',
    todos: false,
    objetivos: [{ tipo: 'Documento', tipoDocumentoTenantId: 'tipo-rut', codigo: 'RUT', nombre: 'Registro Único Tributario' }],
    justificacion: 'La renueva Contabilidad.',
    hasta: null,
    por: 'u1',
    creadaEn: '2026-11-10T15:00:00Z',
    estado: 'Vigente',
    ...parcial,
  };
}

describe('exención del vínculo: modelo de la pantalla', () => {
  it('solo se exime un vínculo aprobado que no esté bloqueado ni terminado', () => {
    expect(['Activo', 'PorVencer', 'Vencido'].every((e) => admiteExencion(e as never))).toBe(true);
    expect(['Invitado', 'EnValidacion', 'Bloqueado', 'Desvinculado', 'Anulado'].some((e) => admiteExencion(e as never))).toBe(false);
  });

  it('las vigentes son las que la API dice vigentes a hoy (el estado ya viene resuelto)', () => {
    const lista = [exencion(), exencion({ id: 'ex2', estado: 'Revocada' }), exencion({ id: 'ex3', estado: 'Vencida' })];
    expect(exencionesVigentes(lista).map((e) => e.id)).toEqual(['ex1']);
    expect(exencionesVigentes(null)).toEqual([]);
    expect(exencionesVigentes(undefined)).toEqual([]);
  });

  it('el alcance dice los nombres que tenían al eximir, o «todo»', () => {
    expect(alcanceDe(exencion(), t)).toBe('Registro Único Tributario');
    expect(
      alcanceDe(exencion({ objetivos: [{ tipo: 'Documento', nombre: 'RUT' }, { tipo: 'Seccion', seccion: 'FICHA_GENERAL', codigo: 'FICHA_GENERAL', nombre: 'Información general' }] }), t),
    ).toBe('RUT, Información general');
    expect(alcanceDe(exencion({ todos: true, objetivos: [] }), t)).toBe('Todos los requisitos y la vigencia del vínculo');
    expect(nombreDeObjetivo({ tipo: 'Seccion', seccion: 'FICHA_SST' })).toBe('FICHA_SST');
    expect(nombreDeObjetivo({ tipo: 'Documento' })).toBe('—');
  });

  it('la vigencia dice hasta cuándo o que rige hasta revocarla', () => {
    expect(vigenciaDe(exencion({ hasta: '2026-12-31' }), t)).toBe('Hasta el 31/12/2026');
    expect(vigenciaDe(exencion(), t)).toBe('Hasta que se revoque');
  });

  it('la insignia solo aparece con exenciones vigentes: «Exento» o «Exento de todo»', () => {
    expect(resumenDeInsignia([exencion({ estado: 'Vencida' })], t)).toBeNull();
    expect(resumenDeInsignia(undefined, t)).toBeNull();
    expect(resumenDeInsignia([exencion({ hasta: '2026-12-31' })], t)).toEqual({
      etiqueta: 'Exento',
      ayuda: 'Exento: Registro Único Tributario. Hasta el 31/12/2026',
    });
    expect(resumenDeInsignia([exencion({ todos: true, objetivos: [] })], t)?.etiqueta).toBe('Exento de todo');
    expect(resumenDeInsignia([exencion(), exencion({ id: 'ex2', objetivos: [{ tipo: 'Seccion', nombre: 'Información general' }] })], t)?.ayuda).toBe(
      'Exento: Registro Único Tributario y más. Hasta que se revoque',
    );
  });

  it('el formulario pide qué se exime y la justificación, y arma la petición', () => {
    const vacio = formularioInicial();
    expect(faltaAlEximir(vacio, t)).toBe('Elige al menos un documento o una sección, o exime de todo.');
    expect(faltaAlEximir({ ...vacio, secciones: new Set(['FICHA_GENERAL']) }, t)).toBe('Escribe la justificación.');
    expect(faltaAlEximir({ ...vacio, todos: true, justificacion: '  ' }, t)).toBe('Escribe la justificación.');
    expect(faltaAlEximir({ ...vacio, todos: true, justificacion: 'Estratégico' }, t)).toBeNull();

    expect(
      aPeticion({ ...vacio, documentos: new Set(['tipo-rut']), secciones: new Set(['FICHA_GENERAL']), justificacion: '  La renueva Contabilidad.  ', hasta: '2026-12-31' }),
    ).toEqual({
      todos: false,
      objetivos: [
        { tipo: 'Documento', tipoDocumentoTenantId: 'tipo-rut' },
        { tipo: 'Seccion', seccion: 'FICHA_GENERAL' },
      ],
      justificacion: 'La renueva Contabilidad.',
      hasta: '2026-12-31',
    });
    // De todo no manda objetivos aunque se hubieran marcado antes; sin fecha, hasta que se revoque.
    expect(aPeticion({ ...vacio, todos: true, documentos: new Set(['tipo-rut']), justificacion: 'Estratégico' })).toEqual({
      todos: true,
      objetivos: null,
      justificacion: 'Estratégico',
      hasta: null,
    });
    expect(formularioInicial('tipo-camara').documentos).toEqual(new Set(['tipo-camara']));
  });

  it('los códigos de la exención tienen su texto; los demás, el mensaje común', () => {
    const problema = (codigo: string, estado = 409) => new ErrorApi(estado, codigo, 'detalle', null, null);
    expect(errorDeExencion(problema('exencion-superpuesta'), t)).toBe('Ya hay una exención vigente que cubre algo de esto: revócala antes de crear otra.');
    expect(errorDeExencion(problema('vinculo-sin-aprobacion'), t)).toBe('Solo se exime un vínculo que ya fue aprobado: este nunca se validó.');
    // La fecha de la exención no es la del bloqueo.
    expect(errorDeExencion(problema('fecha-invalida', 422), t)).toBe('La fecha final debe ser de hoy en adelante y no pasar de diez años.');
    expect(errorDeExencion(problema('estado-terminal'), t)).toContain('ya terminó');
  });

  it('una exención revocada o vencida dice cómo terminó; una vigente, nada', () => {
    const nombreDe = (id: string) => (id === 'u2' ? 'Ana Gómez' : 'una persona');
    expect(cierreDe(exencion(), nombreDe, t)).toBeNull();
    expect(
      cierreDe(exencion({ estado: 'Revocada', revocadaPor: 'u2', revocadaEn: '2026-11-12T15:00:00Z', justificacionDeRevocacion: 'Ya no aplica.' }), nombreDe, t),
    ).toMatch(/^Revocada por Ana Gómez el .*: Ya no aplica\.$/);
    expect(cierreDe(exencion({ estado: 'Vencida', hasta: '2026-11-30' }), nombreDe, t)).toBe('Venció el 30/11/2026');
  });
});
