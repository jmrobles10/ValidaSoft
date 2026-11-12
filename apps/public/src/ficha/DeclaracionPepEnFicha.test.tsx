import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { estadoFicha, guardada, json, montarFicha, seccion, ORDEN_NATURAL } from './pruebas/montar';

// jsdom no tiene lienzo: la firma se simula con un botón.
vi.mock('./pep/FirmaPad', () => ({
  FirmaPad: ({ onCambiar }: { onCambiar: (png: string | null) => void }) => (
    <button type="button" onClick={() => onCambiar('iVBORw0KGgo=')}>
      Simular firma
    </button>
  ),
}));

afterEach(() => reiniciarCliente());

const PREGUNTA = '¿Eres una persona expuesta políticamente (PEP)?';

/** General de una persona natural ya guardada, con dirección y sin declarar PEP. */
const GENERAL_NATURAL = seccion('FICHA_GENERAL', {
  version: 1,
  versiones: 1,
  datos: { nombres: 'Ana María', apellidos: 'Gómez', tipo_persona: 'Natural', tipo_identificacion: 'CC', numero: '1020304050', correo: 'ana@correo.co', pais: 'CO', direccion: 'Carrera 7 # 32-16', pep: false },
  completitud: { completa: false, faltantes: [], reglaVersion: 1 },
});

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

const montarNatural = (general = GENERAL_NATURAL, alGuardar?: () => Response) =>
  montarFicha('/ficha/general', {
    estado: estadoFicha({ tipoPersona: 'Natural', orden: ORDEN_NATURAL }),
    secciones: { FICHA_GENERAL: general },
    alGuardar: alGuardar ?? (() => json(200, guardada('FICHA_GENERAL', [], 'FICHA_TRIBUTARIA'))),
  });

const respuesta = (valor: 'Sí' | 'No') => within(screen.getByRole('radiogroup', { name: PREGUNTA })).getByRole('radio', { name: valor });

describe('Mi información: declaración PEP de la persona natural', () => {
  it('responder «Sí» abre la declaración y salir sin declarar devuelve la respuesta a «No»', async () => {
    const usuario = userEvent.setup();
    montarNatural();
    await screen.findByRole('radiogroup', { name: PREGUNTA });
    await usuario.click(respuesta('Sí'));

    const dialogo = await screen.findByRole('dialog', { name: 'Declaración jurada PEP' });
    expect(within(dialogo).getByRole('textbox', { name: 'Nombre completo o razón social' })).toHaveValue('Ana María Gómez');
    expect(within(dialogo).getByRole('textbox', { name: 'Dirección' })).toHaveValue('Carrera 7 # 32-16');

    await usuario.click(within(dialogo).getByRole('button', { name: 'Cerrar' }));
    await usuario.click(within(screen.getByRole('dialog', { name: 'Declaración incompleta' })).getByRole('button', { name: 'Salir sin declarar' }));
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(respuesta('No')).toBeChecked();
  });

  it('un «Sí» sin declaración avisa que falta y el guardado manda la respuesta sin declaración (el servidor lo rechaza)', async () => {
    const usuario = userEvent.setup();
    const guardados = montarNatural({ ...GENERAL_NATURAL, datos: { ...(GENERAL_NATURAL.datos as object), pep: true } }, () =>
      json(422, {
        status: 422,
        codigo: 'declaracion-pep-requerida',
        detail: 'Con «Sí» en PEP debes diligenciar la declaración jurada.',
        campo: 'declaracion_pep',
        errores: [{ campo: 'declaracion_pep', codigo: 'declaracion-pep-requerida', mensaje: 'Con «Sí» en PEP debes diligenciar la declaración jurada.' }],
      }),
    );
    await screen.findByRole('radiogroup', { name: PREGUNTA });
    expect(screen.getByText('Falta diligenciar la declaración PEP.')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Guardar y continuar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect((guardados[0]?.cuerpo as { datos: Record<string, unknown> }).datos).toMatchObject({ pep: true });
    // El mensaje del servidor queda en el aviso de la declaración.
    expect(await screen.findAllByText('Con «Sí» en PEP debes diligenciar la declaración jurada.')).not.toHaveLength(0);
  });

  it('con la declaración ya guardada muestra su firma, abre precargada y guarda los cambios sin los sellos', async () => {
    const usuario = userEvent.setup();
    const guardados = montarNatural({ ...GENERAL_NATURAL, datos: { ...(GENERAL_NATURAL.datos as object), pep: true, declaracion_pep: DECLARACION } });
    expect(await screen.findByText('Declaración firmada el 01/11/2026')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Ver o editar declaración' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Declaración jurada PEP' });
    const cargo = within(dialogo).getByRole('textbox', { name: 'Cargo desempeñado' });
    expect(cargo).toHaveValue('Alcalde');
    await usuario.click(cargo);
    await usuario.paste(' municipal');
    await usuario.click(within(dialogo).getByRole('checkbox', { name: /^Declaro bajo juramento/ }));
    await usuario.click(within(dialogo).getByRole('button', { name: 'Confirmar declaración' }));
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByText('Declaración lista: se guarda con la sección')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    const enviada = (guardados[0]?.cuerpo as { datos: { pep: boolean; declaracion_pep: Record<string, unknown> } }).datos;
    expect(enviada.pep).toBe(true);
    expect(enviada.declaracion_pep).toMatchObject({ cargo: 'Alcalde municipal', nombre: 'Ana María Gómez', direccion: 'Carrera 7 # 32-16', firma_png: 'iVBORw0KGgo=' });
    expect(enviada.declaracion_pep).not.toHaveProperty('firma_huella');
    expect(enviada.declaracion_pep).not.toHaveProperty('fecha_firma');
    expect(enviada.declaracion_pep).not.toHaveProperty('firma_ip');
  });

  it('pasar a «No» conserva la declaración en los datos (inactiva) y la pregunta de vínculo con una PEP no abre nada', async () => {
    const usuario = userEvent.setup();
    const guardados = montarNatural({ ...GENERAL_NATURAL, datos: { ...(GENERAL_NATURAL.datos as object), pep: true, declaracion_pep: DECLARACION } });
    await screen.findByRole('radiogroup', { name: PREGUNTA });
    await usuario.click(respuesta('No'));
    expect(screen.queryByText(/^Declaración firmada/)).not.toBeInTheDocument();
    await usuario.click(within(screen.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' })).getByRole('radio', { name: 'Sí' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect((guardados[0]?.cuerpo as { datos: Record<string, unknown> }).datos).toMatchObject({ pep: false, declaracion_pep: { cargo: 'Alcalde' } });
  });
});

describe('Mi información: declaración PEP de las personas de la información legal', () => {
  const persona = { rol: 'Socio', tipo_identificacion: 'CC', numero: '79555111', nombres: 'Jorge Iván', apellidos: 'Castro Pinto', porcentaje: 100, pep: true, vinculo_pep: false };
  const montarLegal = (personas: object[], alGuardar?: () => Response) =>
    montarFicha('/ficha/legal', {
      estado: estadoFicha(),
      secciones: { FICHA_LEGAL: seccion('FICHA_LEGAL', { version: 1, versiones: 1, datos: { personas }, completitud: { completa: false, faltantes: [], reglaVersion: 1 } }) },
      alGuardar,
    });

  it('la tarjeta de una persona PEP sin declaración dice que falta; con declaración, no', async () => {
    montarLegal([persona, { ...persona, rol: 'RepresentanteLegal', numero: '1020304050', nombres: 'Marta Lucía', apellidos: 'Ríos Duarte', declaracion_pep: DECLARACION }]);
    const lista = await screen.findByRole('list', { name: 'Personas relacionadas' });
    expect(within(within(lista).getByRole('listitem', { name: 'Jorge Iván Castro Pinto' })).getByText('Falta la declaración PEP')).toBeInTheDocument();
    expect(within(within(lista).getByRole('listitem', { name: 'Marta Lucía Ríos Duarte' })).queryByText('Falta la declaración PEP')).not.toBeInTheDocument();
  });

  it('al responder «Sí» en el diálogo de la persona se abre la declaración con su relación con la empresa', async () => {
    const usuario = userEvent.setup();
    montarLegal([{ ...persona, pep: false }]);
    await usuario.click(await screen.findByRole('button', { name: 'Editar a Jorge Iván Castro Pinto' }));
    const persona_ = screen.getByRole('dialog', { name: 'Editar persona' });
    await usuario.click(within(within(persona_).getByRole('radiogroup', { name: '¿Es persona expuesta políticamente (PEP)?' })).getByRole('radio', { name: 'Sí' }));

    const declaracion = await screen.findByRole('dialog', { name: 'Declaración jurada PEP' });
    const basicos = within(within(declaracion).getByRole('region', { name: '1. Datos básicos' }));
    expect(basicos.getByRole('textbox', { name: 'Relación con la empresa' })).toHaveValue('Socio');
    expect(basicos.getByRole('textbox', { name: 'Documento' })).toHaveValue('CC 79555111');
    // Una persona escribe su dirección en la declaración.
    expect(basicos.getByRole('textbox', { name: 'Dirección' })).not.toHaveAttribute('readonly');
  });

  it('un error del servidor dentro de la declaración de una persona cuenta como un dato por corregir en su tarjeta', async () => {
    const usuario = userEvent.setup();
    montarLegal([{ ...persona, declaracion_pep: DECLARACION }], () =>
      json(422, {
        status: 422,
        codigo: 'valor-invalido',
        detail: 'Falta «Cargo».',
        campo: 'personas[0].declaracion_pep.cargo',
        errores: [{ campo: 'personas[0].declaracion_pep.cargo', codigo: 'valor-invalido', mensaje: 'Falta «Cargo».' }],
      }),
    );
    await screen.findByRole('list', { name: 'Personas relacionadas' });
    await usuario.click(screen.getByRole('button', { name: 'Guardar y continuar' }));
    const tarjeta = await screen.findByRole('listitem', { name: 'Jorge Iván Castro Pinto' });
    expect(await within(tarjeta).findByText('Le falta 1 dato')).toBeInTheDocument();
  });
});
