import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../shared/useAvisos';
import { estadoFicha, json, montarFicha, seccion, ORDEN_NATURAL, type Peticion } from './pruebas/montar';

// jsdom no tiene lienzo: la firma se simula con un botón.
vi.mock('./pep/FirmaPad', () => ({
  FirmaPad: () => <button type="button">Simular firma</button>,
}));

const PREGUNTA = '¿Eres una persona expuesta políticamente (PEP)?';
const COMPLETITUD = { completa: true, faltantes: [], reglaVersion: 1 };

const DECLARACION = {
  nacionalidad: 'CO',
  cargo: 'Alcalde',
  institucion: 'Alcaldía de Soacha',
  pais: 'CO',
  fecha_vinculacion: '2020-01-01',
  vigente: true,
  cuentas_exterior: false,
  vinculo_con_pep: { tiene: false },
  acepta_juramento: true,
  firma_en_calidad: 'NombrePropio',
  firma_png: 'iVBORw0KGgo=',
  firma_huella: 'abc123',
  fecha_firma: '2026-11-01',
  firma_ip: '10.0.0.1',
};

const GENERAL = (datos: object = {}) =>
  seccion('FICHA_GENERAL', {
    version: 3,
    versiones: 3,
    datos: { nombres: 'Ana María', apellidos: 'Gómez', tipo_persona: 'Natural', tipo_identificacion: 'CC', numero: '1020304050', correo: 'ana@correo.co', pais: 'CO', direccion: 'Carrera 7 # 32-16', pep: true, declaracion_pep: DECLARACION, ...datos },
    completitud: COMPLETITUD,
  });

// El cuerpo va como texto: con el `Blob` de jsdom, el `Response` de Node 22 (el del CI) falla con «object.stream is not a function».
const PDF = (nombre = 'declaracion-pep-ana-gomez-20261112.pdf') =>
  new Response('%PDF-1.7', { status: 200, headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': `attachment; filename="${nombre}"` } });

let guardados: { nombre: string; href: string }[] = [];

beforeEach(() => {
  guardados = [];
  useAvisos.getState().cerrar();
  // jsdom no crea URL de objetos ni navega: se anota el archivo que el navegador habría guardado.
  vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:declaracion'), revokeObjectURL: vi.fn() }));
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    guardados.push({ nombre: this.download, href: this.href });
  });
});

afterEach(() => {
  reiniciarCliente();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('Mi información: descargar la propia declaración PEP (persona natural)', () => {
  const montar = (general = GENERAL(), alDescargar: (p: Peticion) => Response = () => PDF()) => {
    const descargas: Peticion[] = [];
    montarFicha('/ficha/general', { estado: estadoFicha({ tipoPersona: 'Natural', orden: ORDEN_NATURAL }), secciones: { FICHA_GENERAL: general }, alDescargar, descargas });
    return descargas;
  };

  it('con «Sí» y la declaración guardada ofrece «Descargar PDF», lo pide al servidor y guarda el archivo con el nombre que propone', async () => {
    const usuario = userEvent.setup();
    const descargas = montar();
    await usuario.click(await screen.findByRole('button', { name: 'Descargar el PDF de tu declaración PEP' }));

    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]).toEqual({ nombre: 'declaracion-pep-ana-gomez-20261112.pdf', href: 'blob:declaracion' });
    expect(descargas).toHaveLength(1);
    expect(descargas[0]).toMatchObject({ metodo: 'GET', ruta: '/public/v1/ficha/FICHA_GENERAL/declaracion-pep/pdf' });
    expect(descargas[0]?.parametros.has('persona')).toBe(false);
  });

  it('sin declaración, o con una que el servidor aún no selló (sin fecha de firma), no hay botón', async () => {
    montar(GENERAL({ declaracion_pep: null }));
    await screen.findByRole('radiogroup', { name: PREGUNTA });
    expect(screen.queryByRole('button', { name: 'Descargar el PDF de tu declaración PEP' })).not.toBeInTheDocument();
  });

  it('una declaración sin la fecha de firma sellada por el servidor tampoco la ofrece', async () => {
    montar(GENERAL({ declaracion_pep: { ...DECLARACION, fecha_firma: null } }));
    await screen.findByRole('radiogroup', { name: PREGUNTA });
    expect(screen.getByText('Declaración lista: se guarda con la sección')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Descargar el PDF de tu declaración PEP' })).not.toBeInTheDocument();
  });

  it('al responder «No» la declaración queda inactiva y el botón desaparece', async () => {
    const usuario = userEvent.setup();
    montar();
    const boton = () => screen.queryByRole('button', { name: 'Descargar el PDF de tu declaración PEP' });
    await screen.findByRole('button', { name: 'Descargar el PDF de tu declaración PEP' });
    await usuario.click(within(screen.getByRole('radiogroup', { name: PREGUNTA })).getByRole('radio', { name: 'No' }));
    expect(boton()).not.toBeInTheDocument();
  });

  it('con cambios sin guardar el PDF no coincidiría con la pantalla: no se ofrece hasta guardar', async () => {
    const usuario = userEvent.setup();
    montar();
    await screen.findByRole('button', { name: 'Descargar el PDF de tu declaración PEP' });
    await usuario.type(screen.getByRole('textbox', { name: 'Teléfono' }), '3');
    await vi.waitFor(() => expect(screen.queryByRole('button', { name: 'Descargar el PDF de tu declaración PEP' })).not.toBeInTheDocument());
  });

  it('una sección sin ninguna versión guardada no ofrece el PDF aunque el formulario traiga una declaración', async () => {
    montar({ ...GENERAL(), version: null, versiones: 0 });
    await screen.findByRole('radiogroup', { name: PREGUNTA });
    expect(screen.queryByRole('button', { name: 'Descargar el PDF de tu declaración PEP' })).not.toBeInTheDocument();
  });

  it('si el servidor no la tiene avisa con su mensaje y no guarda ningún archivo', async () => {
    const usuario = userEvent.setup();
    const descargas = montar(GENERAL(), () => json(404, { status: 404, codigo: 'declaracion-pep-no-encontrada', detail: 'No tienes una declaración PEP activa en la versión vigente de esa sección.' }));
    await usuario.click(await screen.findByRole('button', { name: 'Descargar el PDF de tu declaración PEP' }));

    await vi.waitFor(() => expect(useAvisos.getState().actual?.severidad).toBe('error'));
    expect(useAvisos.getState().actual?.mensaje).toContain('No se pudo descargar la declaración.');
    expect(descargas).toHaveLength(1);
    expect(guardados).toHaveLength(0);
  });
});

describe('Mi información: descargar la declaración PEP de cada persona de la información legal', () => {
  const persona = { rol: 'Socio', tipo_identificacion: 'CC', numero: '79555111', nombres: 'Jorge Iván', apellidos: 'Castro Pinto', porcentaje: 50, pep: true, vinculo_pep: false };
  const montarLegal = (personas: object[]) => {
    const descargas: Peticion[] = [];
    montarFicha('/ficha/legal', {
      estado: estadoFicha(),
      secciones: { FICHA_LEGAL: seccion('FICHA_LEGAL', { version: 2, versiones: 2, datos: { personas }, completitud: COMPLETITUD }) },
      alDescargar: () => PDF('declaracion-pep-marta-rios-20261112.pdf'),
      descargas,
    });
    return descargas;
  };

  it('solo la persona con «Sí» y declaración lleva el botón y pide el PDF con su posición', async () => {
    const usuario = userEvent.setup();
    const descargas = montarLegal([
      { ...persona, declaracion_pep: undefined },
      { ...persona, rol: 'RepresentanteLegal', numero: '1020304050', nombres: 'Marta Lucía', apellidos: 'Ríos Duarte', declaracion_pep: DECLARACION },
      { ...persona, numero: '80111222', nombres: 'Luis', apellidos: 'Ruiz', pep: false, declaracion_pep: DECLARACION },
    ]);
    const lista = await screen.findByRole('list', { name: 'Personas relacionadas' });
    const botones = within(lista).getAllByRole('button', { name: /^Descargar el PDF/ });
    expect(botones).toHaveLength(1);
    expect(botones[0]).toHaveAccessibleName('Descargar el PDF de la declaración PEP de Marta Lucía Ríos Duarte');

    await usuario.click(botones[0]!);
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]?.nombre).toBe('declaracion-pep-marta-rios-20261112.pdf');
    expect(descargas[0]).toMatchObject({ metodo: 'GET', ruta: '/public/v1/ficha/FICHA_LEGAL/declaracion-pep/pdf' });
    expect(descargas[0]?.parametros.get('persona')).toBe('1');
  });
});
