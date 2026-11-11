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

const CAMARA: PropuestaDeSeccionDto['documento'] = { documentoId: 'd-camara', version: 2, tipoDocumento: 'CAMARA_COMERCIO', nombreDocumento: 'Certificado de existencia y representación legal', cargadaEn: '2026-11-09T16:00:00Z' };

describe('Actualizar desde mi RUT / Cámara… (F5)', () => {
  it('en Mi información avisa una sola vez por documento, con las secciones que afecta, y lleva a la primera', async () => {
    montarFicha('/ficha', {
      estado: estadoFicha({ secciones: [filaEstado('FICHA_GENERAL', { version: 1, completa: true })] }),
      propuestas: [
        CON_LECTURA,
        { ...SIN_LECTURA, seccion: 'FICHA_TRIBUTARIA', nombreSeccion: 'Información tributaria' },
        { ...CON_LECTURA, documento: CAMARA, seccion: 'FICHA_LEGAL', nombreSeccion: 'Información legal' },
        { ...CON_LECTURA, documento: CAMARA },
      ],
    });

    const aviso = await screen.findByRole('alert', { name: 'Tienes documentos más nuevos que tu información' });
    // Dos documentos (RUT y Cámara) aunque haya cuatro propuestas (sección x documento): una acción por documento.
    expect(within(aviso).getAllByRole('link', { name: /^Actualizar desde mi/ })).toHaveLength(2);
    // Cada documento es su propio bloque: nombre y versión, y su estado en chips cortos.
    const bloqueRut = within(aviso).getByText('Tu RUT').closest('li')!;
    expect(bloqueRut).toHaveTextContent('Tu RUT · versión 2');
    expect(within(bloqueRut).getByText('1 dato distinto')).toBeInTheDocument();
    expect(within(bloqueRut).getByText('No pudimos leerlo')).toBeInTheDocument();
    const bloqueCamara = within(aviso).getByText('Tu Cámara de Comercio').closest('li')!;
    expect(bloqueCamara).toHaveTextContent('Tu Cámara de Comercio · versión 2');
    expect(within(bloqueCamara).getByText('2 datos distintos')).toBeInTheDocument();
    expect(within(bloqueCamara).queryByText('No pudimos leerlo')).not.toBeInTheDocument();
    expect(within(bloqueRut).queryByText('Tu Cámara de Comercio')).not.toBeInTheDocument();
    // El subtítulo es una línea corta, y la acción dice solo «Actualizar» (el bloque ya nombra el documento).
    expect(within(aviso).getByText('Revisa los cambios y decide cuáles usar.')).toBeInTheDocument();
    expect(within(bloqueRut).getByRole('link', { name: /^Actualizar desde mi RUT/ })).toHaveTextContent(/^Actualizar$/);
    const rut = within(aviso).getByRole('link', { name: 'Actualizar desde mi RUT: Información general, Información tributaria' });
    expect(rut).toHaveAttribute('href', '/ficha/general');
    // Cada sección afectada es un chip que lleva a su formulario.
    const secciones = within(aviso).getByRole('list', { name: 'Secciones que cambian según tu RUT' });
    expect(within(secciones).getByRole('link', { name: 'Información tributaria' })).toHaveAttribute('href', '/ficha/tributaria');
    expect(aviso).not.toHaveTextContent('automáticamente');
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

    const propuesta = await screen.findByRole('alert', { name: 'Tienes documentos más nuevos que esta sección' });
    // Un bloque por documento; los cambios campo por campo se despliegan a pedido.
    expect(propuesta).toHaveTextContent('Tu RUT · versión 2');
    expect(within(propuesta).getByText('1 dato distinto')).toBeInTheDocument();
    expect(within(propuesta).queryByRole('table')).not.toBeInTheDocument();
    await usuario.click(within(propuesta).getByRole('button', { name: 'Ver los cambios de mi RUT' }));
    const tabla = within(propuesta).getByRole('table', { name: 'Datos que cambian según tu RUT' });
    expect(within(tabla).getAllByRole('columnheader').map((c) => c.textContent)).toEqual(['Dato', 'Lo que tienes', 'Lo que dice tu RUT']);
    expect(within(tabla).getAllByRole('row')[1]).toHaveTextContent('DirecciónCalle 45 # 12-08Cl 80 # 10-20');
    // Antes de confirmar el formulario sigue con lo guardado.
    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Calle 45 # 12-08');
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await usuario.click(within(propuesta).getByRole('button', { name: 'Usar los valores de mi RUT' }));

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
    await usuario.click(await screen.findByRole('button', { name: 'Usar los valores de mi RUT' }));
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
    await usuario.click(await screen.findByRole('button', { name: 'Ahora no usar mi RUT' }));

    expect(screen.queryByRole('alert', { name: 'Tienes documentos más nuevos que esta sección' })).not.toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Calle 45 # 12-08');
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  it('sin lectura automática no propone valores: avisa y deja marcar el documento como soporte', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', { estado: estadoFicha(), secciones: { FICHA_GENERAL: GENERAL }, propuestas: [SIN_LECTURA] });

    const aviso = await screen.findByRole('alert', { name: 'Tienes documentos más nuevos que esta sección' });
    expect(aviso).toHaveTextContent('Tu RUT · versión 2');
    expect(within(aviso).getByText('No pudimos leerlo')).toBeInTheDocument();
    expect(aviso).not.toHaveTextContent('automáticamente');
    expect(within(aviso).queryByRole('table')).not.toBeInTheDocument();
    expect(within(aviso).queryByRole('button', { name: /^Usar los valores/ })).not.toBeInTheDocument();

    await usuario.click(within(aviso).getByRole('button', { name: 'Usar mi RUT como soporte' }));
    expect(within(aviso).getByText(/Quedó como soporte/)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Calle 45 # 12-08');
    expect(screen.getByText('Tienes cambios sin guardar')).toBeInTheDocument();
  });

  it('en la sección, dos documentos más nuevos (RUT y Cámara) van en un solo aviso con un bloque por documento', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      propuestas: [CON_LECTURA, { ...CON_LECTURA, documento: CAMARA, campos: [{ campo: 'razon_social', etiqueta: 'Razón social', actual: 'Aceros del Norte S.A.S.', propuesto: 'ACEROS DEL NORTE Y CIA' }] }],
    });

    const avisos = await screen.findAllByRole('alert', { name: 'Tienes documentos más nuevos que esta sección' });
    expect(avisos).toHaveLength(1);
    const aviso = avisos[0]!;
    const lineas = within(aviso).getAllByRole('listitem');
    expect(lineas).toHaveLength(2);
    expect(lineas[0]).toHaveTextContent('Tu RUT · versión 2');
    expect(within(lineas[0]!).getByText('1 dato distinto')).toBeInTheDocument();
    expect(lineas[1]).toHaveTextContent('Tu Cámara de Comercio · versión 2');
    expect(within(lineas[1]!).getByText('1 dato distinto')).toBeInTheDocument();

    // «Ahora no» descarta solo la línea de ese documento.
    await usuario.click(within(lineas[0]!).getByRole('button', { name: 'Ahora no usar mi RUT' }));
    expect(within(aviso).getAllByRole('listitem')).toHaveLength(1);
    expect(aviso).not.toHaveTextContent('Tu RUT');
  });
});
