import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { escribir, estadoFicha, guardada, json, montarFicha, seccion } from './pruebas/montar';

afterEach(() => reiniciarCliente());

describe('Mi información: Información legal', () => {
  it('agrega una persona con el diálogo, la quita y envía las personas sin el DV', async () => {
    const usuario = userEvent.setup();
    const legal = seccion('FICHA_LEGAL', {
      version: 1,
      datos: { personas: [{ rol: 'Socio', tipo_identificacion: 'NIT', numero: '800111222', dv: '5', nombres: 'Inversiones Andinas S.A.S.', porcentaje: 60, pep: false, vinculo_pep: false }] },
      completitud: { completa: false, faltantes: [{ campo: null, regla: 'representante-legal-requerido', mensaje: 'Registra al menos un representante legal.' }], reglaVersion: 1 },
    });
    const guardados = montarFicha('/ficha/legal', { estado: estadoFicha(), secciones: { FICHA_LEGAL: legal }, alGuardar: () => json(200, guardada('FICHA_LEGAL', [], 'FICHA_FINANCIERA')) });

    // Los faltantes de la versión guardada se ven al abrirla.
    expect(await screen.findByRole('alert', { name: 'Para completar esta sección falta 1 dato' })).toHaveTextContent('Registra al menos un representante legal.');
    const personas = screen.getByRole('list', { name: 'Personas relacionadas' });
    expect(within(personas).getByRole('listitem', { name: 'Inversiones Andinas S.A.S.' })).toHaveTextContent('Socio o accionista · NIT 800111222 · 60 %');

    await usuario.click(screen.getByRole('button', { name: 'Agregar persona' }));
    const dialogo = screen.getByRole('dialog', { name: 'Agregar persona' });
    await usuario.click(within(dialogo).getByRole('combobox', { name: 'Rol' }));
    await usuario.click(screen.getByRole('option', { name: 'Representante legal' }));
    await usuario.click(within(dialogo).getByRole('combobox', { name: 'Tipo de identificación' }));
    await usuario.click(screen.getByRole('option', { name: 'CC' }));
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: 'Número de identificación' }), '1020304050');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: 'Nombres' }), 'Ana María');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: 'Apellidos' }), 'Gómez Ruiz');
    const pep = within(dialogo).getByRole('radiogroup', { name: '¿Es persona expuesta políticamente (PEP)?' });
    await usuario.click(within(pep).getByRole('radio', { name: 'No' }));
    await usuario.click(within(dialogo).getByRole('button', { name: 'Listo' }));

    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(within(personas).getByRole('listitem', { name: 'Ana María Gómez Ruiz' })).toHaveTextContent('Representante legal · CC 1020304050');

    await usuario.click(within(personas).getByRole('button', { name: 'Quitar a Inversiones Andinas S.A.S.' }));
    expect(within(personas).getAllByRole('listitem')).toHaveLength(1);

    await usuario.click(screen.getByRole('button', { name: 'Guardar y continuar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]?.cuerpo).toEqual({
      datos: { personas: [{ rol: 'RepresentanteLegal', tipo_identificacion: 'CC', numero: '1020304050', nombres: 'Ana María', apellidos: 'Gómez Ruiz', pep: false }] },
      soportes: [],
    });
    await vi.waitFor(() => expect(screen.getByTestId('ubicacion')).toHaveTextContent('/ficha/financiera'));
  });
});

describe('Mi información: Especialidades', () => {
  const CATALOGO = [
    { codigo: 'OBRA', nombre: 'Obra civil', nivel: 1, padre: null },
    { codigo: 'OBRA-EST', nombre: 'Estructuras', nivel: 2, padre: 'OBRA' },
    { codigo: 'OBRA-EST-CON', nombre: 'Concreto reforzado', nivel: 3, padre: 'OBRA-EST' },
    { codigo: 'OBRA-EST-MET', nombre: 'Estructura metálica', nivel: 3, padre: 'OBRA-EST' },
    { codigo: 'ACAB', nombre: 'Acabados', nivel: 1, padre: null },
    { codigo: 'ACAB-PIS', nombre: 'Pisos', nivel: 2, padre: 'ACAB' },
    { codigo: 'ACAB-PIS-CER', nombre: 'Cerámica', nivel: 3, padre: 'ACAB-PIS' },
  ];

  it('elige especialidades en el árbol de 3 niveles, busca sin tildes y quita desde la lista elegida', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/especialidades', {
      estado: estadoFicha(),
      catalogos: { especialidades: CATALOGO },
      alGuardar: () => json(200, guardada('FICHA_ESPECIALIDADES', [], null)),
    });

    expect(await screen.findByText('Aún no eliges especialidades.')).toBeInTheDocument();
    // Grupo → categoría → especialidad.
    await usuario.click(await screen.findByRole('button', { name: 'Obra civil' }));
    await usuario.click(screen.getByRole('button', { name: 'Estructuras' }));
    await usuario.click(screen.getByRole('checkbox', { name: 'Concreto reforzado' }));
    expect(screen.getByRole('button', { name: /Obra civil.*1 elegida/ })).toBeInTheDocument();

    // La búsqueda abre lo que coincide y esconde lo demás.
    await escribir(usuario, screen.getByRole('searchbox', { name: 'Buscar especialidad' }), 'ceramica');
    expect(screen.getByRole('checkbox', { name: 'Cerámica' })).toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: 'Concreto reforzado' })).not.toBeInTheDocument();
    await usuario.click(screen.getByRole('checkbox', { name: 'Cerámica' }));

    const elegidas = screen.getByRole('region', { name: 'Tus especialidades' });
    expect(elegidas).toHaveTextContent('Concreto reforzado');
    expect(elegidas).toHaveTextContent('Cerámica');
    await usuario.click(within(elegidas).getByLabelText('Quitar Concreto reforzado'));
    expect(elegidas).not.toHaveTextContent('Concreto reforzado');

    await usuario.click(screen.getByRole('button', { name: 'Guardar y continuar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]?.cuerpo).toEqual({ datos: { especialidades: ['ACAB-PIS-CER'] }, soportes: [] });
    // Sin más secciones por llenar, vuelve al resumen.
    await vi.waitFor(() => expect(screen.getByTestId('ubicacion')).toHaveTextContent(/^\/ficha$/));
  });
});
