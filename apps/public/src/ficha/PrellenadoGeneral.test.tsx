import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { prellenadoDe } from './useEditorSeccion';
import { estadoFicha, guardada, json, montarFicha, ORDEN_NATURAL, seccion } from './pruebas/montar';

afterEach(() => reiniciarCliente());

/** Lo que la API ofrece de una General sin ninguna versión guardada (FD-1): su registro y el correo de la invitación. */
const VACIA_CON_SUGERENCIA = seccion('FICHA_GENERAL', {
  prellenado: {
    razon_social: 'Aceros del Norte S.A.S.',
    correo: 'compras@aceros.co',
    tipo_persona: 'Juridica',
    tipo_identificacion: 'NIT',
    numero: '900373913',
    dv: '6',
  },
});

describe('Mi información: la General se ofrece prellenada (FD-1)', () => {
  it('arranca con lo que ya se sabe del tercero, lo dice una vez y «Guardar» queda habilitado sin tocar nada', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: VACIA_CON_SUGERENCIA },
      alGuardar: () => json(200, guardada('FICHA_GENERAL', [{ campo: 'telefono', regla: null, mensaje: 'Falta «Teléfono».' }], 'FICHA_GENERAL')),
    });

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('Prellenamos tu información');
    expect(aviso).toHaveTextContent('Revísalo y guárdalo.');
    expect(screen.getByRole('textbox', { name: 'Razón social' })).toHaveValue('Aceros del Norte S.A.S.');
    expect(screen.getByRole('textbox', { name: 'Correo electrónico' })).toHaveValue('compras@aceros.co');
    // La identificación sale con su DV (antes decía «Se completa con tu registro al guardar»).
    expect(screen.getByRole('textbox', { name: 'Identificación' })).toHaveValue('NIT 900373913-6');
    // No hay cambios del tercero y aun así puede guardar lo sugerido.
    expect(screen.queryByText('Tienes cambios sin guardar')).not.toBeInTheDocument();
    const guardar = screen.getByRole('button', { name: 'Guardar' });
    expect(guardar).toBeEnabled();

    await usuario.click(guardar);
    // Lo que fija el registro no viaja; lo sugerido, sí.
    expect(guardados[0]?.cuerpo).toEqual({ datos: { razon_social: 'Aceros del Norte S.A.S.', correo: 'compras@aceros.co' }, soportes: [] });
    // Guardada la sugerencia deja de ser una sugerencia.
    expect(await screen.findByRole('alert', { name: 'Para completar esta sección falta 1 dato' })).toHaveTextContent('Falta «Teléfono».');
    expect(screen.queryByText('Prellenamos tu información')).not.toBeInTheDocument();
  });

  it('con una versión guardada no se ofrece nada prellenado', async () => {
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: {
        FICHA_GENERAL: seccion('FICHA_GENERAL', {
          version: 1,
          versiones: 1,
          datos: { razon_social: 'Otra S.A.S.', tipo_persona: 'Juridica', tipo_identificacion: 'NIT', numero: '900373913', dv: '6' },
          prellenado: { razon_social: 'Aceros del Norte S.A.S.' },
        }),
      },
    });
    expect(await screen.findByRole('textbox', { name: 'Razón social' })).toHaveValue('Otra S.A.S.');
    expect(screen.queryByText('Prellenamos tu información')).not.toBeInTheDocument();
  });

  it('prellenadoDe solo toma un objeto de una sección sin versión ni datos', () => {
    expect(prellenadoDe({ version: null, datos: null, prellenado: { correo: 'a@b.co' } })).toEqual({ correo: 'a@b.co' });
    expect(prellenadoDe({ version: null, datos: null, prellenado: null })).toBeNull();
    expect(prellenadoDe({ version: null, datos: null, prellenado: ['x'] })).toBeNull();
    expect(prellenadoDe({ version: null, datos: null })).toBeNull();
    expect(prellenadoDe({ version: 2, datos: { a: 1 }, prellenado: { correo: 'a@b.co' } })).toBeNull();
  });
});

describe('Mi información: lo que difiere entre persona natural y jurídica (FD-8)', () => {
  const NATURAL = seccion('FICHA_GENERAL', {
    prellenado: { nombres: 'Juan Carlos', apellidos: 'Pérez Gómez', tipo_persona: 'Natural', tipo_identificacion: 'CC', numero: '79123456' },
  });

  it('la natural declara la expedición de su documento y su PEP; los envía al guardar', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/general', {
      estado: estadoFicha({ tipoPersona: 'Natural', orden: ORDEN_NATURAL }),
      secciones: { FICHA_GENERAL: NATURAL },
      alGuardar: () => json(200, guardada('FICHA_GENERAL', [], 'FICHA_TRIBUTARIA')),
    });

    expect(await screen.findByRole('textbox', { name: 'Nombres' })).toHaveValue('Juan Carlos');
    expect(screen.getByRole('textbox', { name: 'Apellidos' })).toHaveValue('Pérez Gómez');
    expect(screen.queryByRole('textbox', { name: 'Razón social' })).not.toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Identificación' })).toHaveValue('CC 79123456');

    await usuario.type(screen.getByRole('textbox', { name: 'Lugar de expedición' }), 'Bogotá D.C.');
    const pep = screen.getByRole('radiogroup', { name: '¿Eres una persona expuesta políticamente (PEP)?' });
    await usuario.click(within(pep).getByRole('radio', { name: 'No' }));
    const vinculo = screen.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' });
    await usuario.click(within(vinculo).getByRole('radio', { name: 'Sí' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(guardados[0]?.cuerpo).toMatchObject({ datos: { nombres: 'Juan Carlos', lugar_expedicion: 'Bogotá D.C.', pep: false, vinculo_pep: true } });
  });

  it('la jurídica no ve lo propio de la natural', async () => {
    montarFicha('/ficha/general', { estado: estadoFicha(), secciones: { FICHA_GENERAL: VACIA_CON_SUGERENCIA } });
    expect(await screen.findByRole('textbox', { name: 'Razón social' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Nombres' })).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Lugar de expedición' })).not.toBeInTheDocument();
    expect(screen.queryByRole('radiogroup', { name: /PEP/ })).not.toBeInTheDocument();
  });
});

describe('Mi información: un cambio sin documento nuevo (FM-15, FD-4)', () => {
  it('avisa qué datos cambiaron con el mismo documento de antes, mientras no haya nada sin guardar', async () => {
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: {
        FICHA_GENERAL: seccion('FICHA_GENERAL', {
          version: 3,
          versiones: 3,
          datos: { razon_social: 'Aceros del Sur S.A.S.', tipo_persona: 'Juridica', tipo_identificacion: 'NIT', numero: '900373913', dv: '6' },
          sinSoporte: { critico: true, cambios: [{ campo: 'razon_social', etiqueta: 'Razón social', anterior: 'Aceros del Norte S.A.S.', nuevo: 'Aceros del Sur S.A.S.', tipoCambio: 'Modificado' }] },
        }),
      },
    });
    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('Tu último cambio no trae un documento nuevo');
    expect(aviso).toHaveTextContent('Cambiaste Razón social con el mismo documento de antes.');
  });
});
