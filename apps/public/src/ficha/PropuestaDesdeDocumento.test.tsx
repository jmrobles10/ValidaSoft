import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type PropuestaDeSeccionDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { documento, estadoFicha, filaEstado, guardada, json, montarFicha, seccion } from './pruebas/montar';

afterEach(() => reiniciarCliente());

const GENERAL = seccion('FICHA_GENERAL', {
  version: 1,
  versiones: 1,
  datos: { razon_social: 'Aceros del Norte S.A.S.', tipo_persona: 'Juridica', tipo_identificacion: 'NIT', numero: '900123456', dv: '8', correo: 'compras@aceros.co', pais: 'CO', direccion: 'Calle 45 # 12-08', telefono: '601 555 1234' },
  completitud: { completa: true, faltantes: [], reglaVersion: 1 },
});

const DOCUMENTO = { documentoId: 'd-rut', version: 2, tipoDocumento: 'RUT', nombreDocumento: 'Registro Único Tributario', cargadaEn: '2026-11-09T15:00:00Z' };

/** El RUT v2 lee otra dirección: propone cambiarla (y trae la sección ya cambiada para prellenar). */
const CON_LECTURA: PropuestaDeSeccionDto = {
  seccion: 'FICHA_GENERAL',
  nombreSeccion: 'Información general',
  periodo: null,
  documento: DOCUMENTO,
  disponible: true,
  motivo: null,
  campos: [{ campo: 'direccion', etiqueta: 'Dirección', actual: 'Calle 45 # 12-08', propuesto: 'Cl 80 # 10-20' }],
  datos: { ...GENERAL.datos, direccion: 'Cl 80 # 10-20' },
};

const SIN_LECTURA: PropuestaDeSeccionDto = { ...CON_LECTURA, disponible: false, motivo: 'sin-lectura-automatica', campos: [], datos: undefined };

describe('Actualizar desde mi RUT / Cámara… (F5)', () => {
  it('en Mi información ofrece actualizar desde el documento con los datos distintos y lleva a la sección', async () => {
    montarFicha('/ficha', {
      estado: estadoFicha({ secciones: [filaEstado('FICHA_GENERAL', { version: 1, completa: true })] }),
      propuestas: [CON_LECTURA, { ...SIN_LECTURA, seccion: 'FICHA_TRIBUTARIA', nombreSeccion: 'Información tributaria' }],
    });

    const aviso = await screen.findByRole('alert', { name: 'Tienes documentos más nuevos que tu información' });
    expect(aviso).toHaveTextContent('Tu RUT (versión 2) tiene 1 dato distinto');
    expect(aviso).toHaveTextContent('Tu RUT (versión 2) es más nuevo; no se pudo leer automáticamente');
    expect(within(aviso).getByRole('link', { name: 'Actualizar Información general desde mi RUT' })).toHaveAttribute('href', '/ficha/general');
    expect(within(aviso).getByRole('link', { name: 'Actualizar Información tributaria desde mi RUT' })).toHaveAttribute('href', '/ficha/tributaria');
  });

  it('sin propuestas no aparece nada', async () => {
    montarFicha('/ficha', { estado: estadoFicha({ secciones: [filaEstado('FICHA_GENERAL', { version: 1, completa: true })] }) });
    await screen.findByRole('list', { name: 'Mi información' });
    expect(screen.queryByText('Tienes documentos más nuevos que tu información')).not.toBeInTheDocument();
  });

  it('en la sección muestra la propuesta campo por campo; «Usar estos valores» solo llena el formulario y nada se guarda solo', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      propuestas: [CON_LECTURA],
      documentos: [documento('d-rut', 'RUT', 'Registro Único Tributario')],
      alGuardar: () => json(200, guardada('FICHA_GENERAL', [], 'FICHA_TRIBUTARIA')),
    });

    const propuesta = await screen.findByRole('alert', { name: 'Actualizar desde mi RUT' });
    const tabla = within(propuesta).getByRole('table', { name: 'Datos que cambian según tu RUT' });
    expect(within(tabla).getAllByRole('columnheader').map((c) => c.textContent)).toEqual(['Dato', 'Lo que tienes', 'Lo que dice tu RUT']);
    expect(within(tabla).getAllByRole('row')[1]).toHaveTextContent('DirecciónCalle 45 # 12-08Cl 80 # 10-20');
    // Antes de confirmar el formulario sigue con lo guardado.
    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Calle 45 # 12-08');
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await usuario.click(within(propuesta).getByRole('button', { name: 'Usar estos valores' }));

    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Cl 80 # 10-20');
    expect(within(propuesta).getByRole('status')).toHaveTextContent('todavía no se ha guardado nada');
    expect(screen.getByText('Tienes cambios sin guardar')).toBeInTheDocument();
    expect(guardados).toHaveLength(0);
    // El documento queda marcado como soporte de lo que se guarde.
    expect(await screen.findByRole('checkbox', { name: /Registro Único Tributario · versión 2/ })).toBeChecked();

    // Al guardar (el tercero decide) viaja la sección con el RUT v2 como soporte.
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]?.cuerpo).toMatchObject({ datos: { direccion: 'Cl 80 # 10-20' }, soportes: [{ documentoId: 'd-rut', version: 2 }] });
  });

  it('el tercero puede corregir a mano lo propuesto antes de guardar', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      propuestas: [CON_LECTURA],
      alGuardar: () => json(200, guardada('FICHA_GENERAL', [], null)),
    });
    await usuario.click(await screen.findByRole('button', { name: 'Usar estos valores' }));
    const direccion = screen.getByRole('textbox', { name: 'Dirección' });
    await usuario.clear(direccion);
    await usuario.type(direccion, 'Cl 80 # 10-21');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]?.cuerpo).toMatchObject({ datos: { direccion: 'Cl 80 # 10-21' } });
  });

  it('«Ahora no» oculta la propuesta sin tocar el formulario', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', { estado: estadoFicha(), secciones: { FICHA_GENERAL: GENERAL }, propuestas: [CON_LECTURA] });
    await usuario.click(await screen.findByRole('button', { name: 'Ahora no' }));

    expect(screen.queryByRole('alert', { name: 'Actualizar desde mi RUT' })).not.toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Calle 45 # 12-08');
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  it('sin lectura automática no propone valores: avisa y deja marcar el documento como soporte', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', { estado: estadoFicha(), secciones: { FICHA_GENERAL: GENERAL }, propuestas: [SIN_LECTURA] });

    const aviso = await screen.findByRole('alert', { name: 'Actualizar desde mi RUT' });
    expect(aviso).toHaveTextContent('No pudimos leer automáticamente tu RUT');
    expect(within(aviso).queryByRole('table')).not.toBeInTheDocument();
    expect(within(aviso).queryByRole('button', { name: 'Usar estos valores' })).not.toBeInTheDocument();

    await usuario.click(within(aviso).getByRole('button', { name: 'Usar como soporte' }));
    expect(within(aviso).getByText(/Quedó marcado como soporte/)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Calle 45 # 12-08');
    expect(screen.getByText('Tienes cambios sin guardar')).toBeInTheDocument();
  });
});
