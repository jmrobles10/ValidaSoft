import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type SolicitudDeAccesoDePlataformaDto } from '@validasoft/api-client';
import { Route, Routes } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';
import { montarPantalla } from '../../pruebas/montar';
import { ProblemaSimulado, simularApi } from '../../pruebas/simularApi';
import { PaginaSolicitudesDeAcceso } from './PaginaSolicitudesDeAcceso';

afterEach(() => reiniciarCliente());

function solicitud(cambios: Partial<SolicitudDeAccesoDePlataformaDto> = {}): SolicitudDeAccesoDePlataformaDto {
  return {
    id: 's1',
    terceroId: 't1',
    tercero: 'Proveedores del Norte S.A.S.',
    identificacion: 'NIT 900123456',
    correo: 'gerente@norte.co',
    estado: 'Pendiente',
    desde: '2026-11-12T15:00:00Z',
    empresas: [{ empresa: 'Norte S.A.S.', grupo: 'Grupo Norte', tenantId: 'g1', fecha: '2026-11-12T15:00:00Z' }],
    contactos: [{ nombre: 'Creador', correo: 'creador@a.co', rol: 'Principal', verificacion: 'SinVerificar', medio: null, verificadoEn: null, esTuCorreo: false, desplazado: false }],
    correosOficiales: [],
    enCola: true,
    contactoDesplazado: false,
    decididoPor: null,
    decididoVia: null,
    decididoEn: null,
    motivo: null,
    ...cambios,
  };
}

function montar(rutas: Parameters<typeof simularApi>[0]) {
  const peticiones = simularApi(rutas);
  montarPantalla(
    <Routes>
      <Route path="/plataforma/accesos-a-terceros" element={<PaginaSolicitudesDeAcceso />} />
    </Routes>,
    '/plataforma/accesos-a-terceros',
  );
  return { peticiones, usuario: userEvent.setup() };
}

describe('Solicitudes de acceso a terceros (consola de Plataforma, decisión AV)', () => {
  it('lista la cola con la evidencia: quién invitó, los contactos del tercero con su verificación y los correos oficiales', async () => {
    montar({
      'GET /api/v1/plataforma/solicitudes-de-acceso': [
        solicitud({
          correosOficiales: [{ correo: 'gerente@norte.co', documento: 'RUT', version: 2 }],
          contactos: [
            { nombre: 'Creador', correo: 'creador@a.co', rol: 'Principal', verificacion: 'SinVerificar', medio: null, verificadoEn: null, esTuCorreo: false, desplazado: false },
            { nombre: 'Tesorería', correo: 'tesoreria@norte.co', rol: 'Contacto', verificacion: 'Verificado', medio: 'Importacion', verificadoEn: '2026-11-01T10:00:00Z', esTuCorreo: false, desplazado: false },
          ],
        }),
      ],
    });

    const tarjeta = await screen.findByRole('listitem', { name: 'gerente@norte.co' });
    expect(within(tarjeta).getByText('Pendiente')).toBeInTheDocument();
    expect(within(tarjeta).getByText(/Proveedores del Norte S\.A\.S\. · NIT 900123456 · pide acceso desde el 12\/11\/2026/)).toBeInTheDocument();
    expect(within(tarjeta).getByText('Norte S.A.S. (grupo Grupo Norte) · 12/11/2026')).toBeInTheDocument();
    const contactos = within(tarjeta).getByRole('region', { name: 'Contactos del tercero' });
    expect(within(contactos).getByText('creador@a.co')).toBeInTheDocument();
    expect(within(contactos).getByText('Sin verificar')).toBeInTheDocument();
    expect(within(contactos).getByText('Verificado')).toBeInTheDocument();
    expect(within(contactos).getByText('traído del sistema de origen')).toBeInTheDocument();
    expect(within(tarjeta).getByText('Correo del RUT (versión 2): gerente@norte.co')).toBeInTheDocument();
    expect(within(tarjeta).getByRole('button', { name: 'Aprobar el acceso de gerente@norte.co' })).toBeInTheDocument();
    expect(within(tarjeta).getByRole('button', { name: 'Rechazar el acceso de gerente@norte.co' })).toBeInTheDocument();
  });

  it('sin correo oficial ni contactos lo dice con claridad', async () => {
    montar({ 'GET /api/v1/plataforma/solicitudes-de-acceso': [solicitud({ contactos: [] })] });

    const tarjeta = await screen.findByRole('listitem', { name: 'gerente@norte.co' });
    expect(within(tarjeta).getByText('El tercero no tiene contactos: nadie de su lado puede decidir.')).toBeInTheDocument();
    expect(within(tarjeta).getByText(/no hay un correo oficial con qué compararlo/)).toBeInTheDocument();
  });

  it('pide la cola por omisión y cambia de lista con el filtro', async () => {
    const { peticiones, usuario } = montar({ 'GET /api/v1/plataforma/solicitudes-de-acceso': [solicitud()] });
    await screen.findByRole('listitem', { name: 'gerente@norte.co' });
    expect(peticiones.find((p) => p.ruta === '/api/v1/plataforma/solicitudes-de-acceso')?.url).toContain('estado=cola');

    await usuario.click(screen.getByRole('button', { name: 'Resueltas' }));
    await waitFor(() => expect(peticiones.filter((p) => p.ruta === '/api/v1/plataforma/solicitudes-de-acceso').some((p) => p.url.includes('estado=resueltas'))).toBe(true));
  });

  it('aprobar pide confirmación, dice qué pasa y recién entonces manda la aprobación', async () => {
    const { peticiones, usuario } = montar({
      'GET /api/v1/plataforma/solicitudes-de-acceso': [solicitud()],
      'POST /api/v1/plataforma/solicitudes-de-acceso/s1/aprobar': solicitud({ estado: 'Aprobada' }),
    });

    await usuario.click(await screen.findByRole('button', { name: 'Aprobar el acceso de gerente@norte.co' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Aprobar el acceso de gerente@norte.co' });
    expect(within(dialogo).getByText(/pasará a ser contacto verificado de Proveedores del Norte S\.A\.S\. \(verificado por la plataforma\)/)).toBeInTheDocument();
    expect(within(dialogo).getByText(/cualquier contacto sin verificar quedará en acceso aislado/)).toBeInTheDocument();
    expect(peticiones.some((p) => p.metodo === 'POST')).toBe(false);

    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, aprobar' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/api/v1/plataforma/solicitudes-de-acceso/s1/aprobar')).toBe(true));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('rechazar manda el motivo anotado, o nulo si se deja vacío', async () => {
    const { peticiones, usuario } = montar({
      'GET /api/v1/plataforma/solicitudes-de-acceso': [solicitud()],
      'POST /api/v1/plataforma/solicitudes-de-acceso/s1/rechazar': solicitud({ estado: 'Rechazada' }),
    });

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el acceso de gerente@norte.co' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Rechazar el acceso de gerente@norte.co' });
    await usuario.type(within(dialogo).getByLabelText('Motivo (opcional)'), '  El NIT no es de esa persona ');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, rechazar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'POST')?.cuerpo).toEqual({ motivo: 'El NIT no es de esa persona' });
  });

  it('si el servidor no deja decidir (ya hay un contacto verificado) lo dice dentro del diálogo y no lo cierra', async () => {
    const { usuario } = montar({
      'GET /api/v1/plataforma/solicitudes-de-acceso': [solicitud()],
      'POST /api/v1/plataforma/solicitudes-de-acceso/s1/aprobar': new ProblemaSimulado(409, 'decide-un-contacto', 'Este tercero ya tiene contactos verificados: ellos deciden los accesos.'),
    });

    await usuario.click(await screen.findByRole('button', { name: 'Aprobar el acceso de gerente@norte.co' }));
    const dialogo = await screen.findByRole('dialog');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, aprobar' }));

    expect(await within(dialogo).findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('un operador verifica un contacto sin verificar y lo manda con el correo del contacto', async () => {
    const { peticiones, usuario } = montar({
      'GET /api/v1/plataforma/solicitudes-de-acceso': [solicitud()],
      'POST /api/v1/plataforma/terceros/t1/contactos/verificar': {
        nombre: 'Creador', correo: 'creador@a.co', rol: 'Principal', verificacion: 'Verificado', medio: 'Plataforma', verificadoEn: '2026-11-14T10:00:00Z', esTuCorreo: false, desplazado: false,
      },
    });

    await usuario.click(await screen.findByRole('button', { name: 'Verificar el correo creador@a.co' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST')).toBe(true));
    const envio = peticiones.find((p) => p.metodo === 'POST');
    expect(envio?.ruta).toBe('/api/v1/plataforma/terceros/t1/contactos/verificar');
    expect(envio?.cuerpo).toEqual({ correo: 'creador@a.co' });
  });

  it('una solicitud pendiente que decide un contacto verificado no ofrece aprobar ni rechazar y lo explica', async () => {
    montar({ 'GET /api/v1/plataforma/solicitudes-de-acceso': [solicitud({ enCola: false })] });

    const tarjeta = await screen.findByRole('listitem', { name: 'gerente@norte.co' });
    expect(within(tarjeta).queryByRole('button', { name: /Aprobar el acceso/ })).not.toBeInTheDocument();
    expect(within(tarjeta).getByText(/Esta solicitud la decide un contacto verificado del tercero/)).toBeInTheDocument();
  });

  it('una resuelta dice quién la decidió y cómo, sin botones de decisión ni de verificación', async () => {
    const { usuario } = montar({
      'GET /api/v1/plataforma/solicitudes-de-acceso': [
        solicitud({ estado: 'Rechazada', enCola: false, decididoPor: 'plataforma:op', decididoVia: 'Plataforma', decididoEn: '2026-11-13T10:00:00Z', motivo: 'El NIT no es suyo' }),
      ],
    });

    await usuario.click(await screen.findByRole('button', { name: 'Resueltas' }));
    const tarjeta = await screen.findByRole('listitem', { name: 'gerente@norte.co' });
    expect(within(tarjeta).getByText('Rechazada')).toBeInTheDocument();
    expect(within(tarjeta).getByText(/Lo decidió plataforma:op \(la plataforma\) el 13\/11\/2026 · Motivo: El NIT no es suyo/)).toBeInTheDocument();
    expect(within(tarjeta).queryByRole('button', { name: /Aprobar|Rechazar|Verificar/ })).not.toBeInTheDocument();
  });

  it('sin solicitudes en la cola explica cuándo aparecerán', async () => {
    montar({ 'GET /api/v1/plataforma/solicitudes-de-acceso': [] });

    expect(await screen.findByText('No hay solicitudes en la cola')).toBeInTheDocument();
    expect(screen.getByText(/aparece aquí/)).toBeInTheDocument();
  });

  it('si no carga ofrece reintentar', async () => {
    montar({ 'GET /api/v1/plataforma/solicitudes-de-acceso': new ProblemaSimulado(500, 'error-interno', 'Falló') });

    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});
