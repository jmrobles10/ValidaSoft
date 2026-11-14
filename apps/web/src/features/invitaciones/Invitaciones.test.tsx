import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { InvitacionPorAprobarDto, InvitacionPorAprobarItemDto, LoteDto } from '@validasoft/api-client';
import { Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { montarPantalla } from '../../pruebas/montar';
import { ProblemaSimulado, simularApi } from '../../pruebas/simularApi';
import { AvisoInvitacionPorAprobar } from './AvisoInvitacionPorAprobar';
import { PaginaInvitacionesPorAprobar } from './PaginaInvitacionesPorAprobar';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>(['vinculos:leer', 'vinculos:escribir']) }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

function retenida(cambios: Partial<InvitacionPorAprobarDto> = {}): InvitacionPorAprobarDto {
  return {
    estado: 'Pendiente',
    correoDestino: 'ana@aceros.co',
    vigenciaHoras: 72,
    solicitadaEn: '2026-11-14T15:00:00Z',
    solicitadaPor: 'Usuario:u1',
    solicitadaPorNombre: 'Laura Compras',
    puedesDecidir: true,
    ...cambios,
  };
}

const ITEM: InvitacionPorAprobarItemDto = {
  vinculoId: 'v1',
  terceroId: 't1',
  terceroNombre: 'Aceros del Norte',
  terceroIdentificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456', digitoVerificacion: null },
  tipoVinculo: { id: 'tv1', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
  correoDestino: 'ana@aceros.co',
  solicitadaEn: '2026-11-14T15:00:00Z',
  solicitadaPor: 'Usuario:u1',
  solicitadaPorNombre: 'Laura Compras',
  puedesDecidir: true,
};

function lote(cambios: Partial<LoteDto> = {}): LoteDto {
  return {
    id: 'l1', nombre: 'Proveedores de noviembre', archivo: null, estado: 'PorAprobar', vigenciaHoras: 168, creadoPor: 'Usuario:u1',
    creadoEn: '2026-11-14T12:00:00Z', actualizadoEn: '2026-11-14T12:00:00Z', terminadoEn: null, reintentos: 0, version: 1,
    totales: { filas: 5, procesadas: 0, pendientes: 5, exitosas: 0, fallidas: 0, duplicadas: 0, invalidas: 0 },
    tiposPorAprobar: ['PROVEEDOR'], puedesDecidir: true,
    ...cambios,
  };
}

describe('aviso de la invitación retenida en el vínculo', () => {
  it('sin aprobación previa no muestra nada', () => {
    simularApi({});
    montarPantalla(<AvisoInvitacionPorAprobar vinculo={{ id: 'v1', version: 3, invitacionPorAprobar: null }} empresaId="emp" />);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /aprobar|rechazar/i })).not.toBeInTheDocument();
  });

  it('quien puede decidir aprueba con la versión que leyó y el aviso cuenta que el correo sale al aprobarla', async () => {
    const usuario = userEvent.setup();
    const peticiones = simularApi({ 'POST /api/v1/vinculos/v1/invitacion/aprobar': { id: 'v1' } });
    montarPantalla(<AvisoInvitacionPorAprobar vinculo={{ id: 'v1', version: 3, invitacionPorAprobar: retenida() }} empresaId="emp" />);

    expect(screen.getByText('La invitación a ana@aceros.co espera aprobación')).toBeInTheDocument();
    expect(screen.getByText(/Laura Compras la pidió el .*Hasta que otra persona la apruebe no sale el correo ni el enlace/)).toBeInTheDocument();
    expect(screen.getByText(/Puedes aprobarla o rechazarla/)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Aprobar y enviar' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/api/v1/vinculos/v1/invitacion/aprobar')).toBe(true));
    expect(peticiones[0]?.cabeceras.get('If-Match')).toBe('"3"');
    expect(peticiones[0]?.cabeceras.get('X-Empresa')).toBe('emp');
  });

  it('rechazar exige el motivo, lo manda y deja ver el error de la API sin cerrar el diálogo', async () => {
    const usuario = userEvent.setup();
    let intentos = 0;
    const peticiones = simularApi({
      'POST /api/v1/vinculos/v1/invitacion/rechazar': () => {
        intentos += 1;
        return intentos === 1 ? new ProblemaSimulado(403, 'separacion-de-funciones', 'Quien pidió la invitación no puede decidirla.') : { id: 'v1' };
      },
    });
    montarPantalla(<AvisoInvitacionPorAprobar vinculo={{ id: 'v1', version: 3, invitacionPorAprobar: retenida() }} empresaId="emp" />);

    await usuario.click(screen.getByRole('button', { name: 'Rechazar…' }));
    const dialogo = await screen.findByRole('dialog', { name: /Rechazar la invitación/ });
    const rechazar = within(dialogo).getByRole('button', { name: 'Rechazar' });
    expect(rechazar).toBeDisabled();
    await usuario.click(within(dialogo).getByRole('textbox', { name: /Motivo/ }));
    await usuario.paste('El proveedor no está autorizado');
    await usuario.click(rechazar);

    // La API lo rechazó: el diálogo sigue abierto y dice por qué con el texto de la invitación (no el de los expedientes).
    expect(await within(dialogo).findByText(/Quien pidió la invitación o creó el lote no puede aprobarlo ni rechazarlo/)).toBeInTheDocument();
    await usuario.click(rechazar);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const envios = peticiones.filter((p) => p.metodo === 'POST');
    expect(envios).toHaveLength(2);
    expect(envios[1]?.cuerpo).toEqual({ motivo: 'El proveedor no está autorizado' });
  });

  it('quien no la decide la ve retenida sin botones; aprobada o rechazada dice quién y cuándo', () => {
    simularApi({});
    const { unmount } = montarPantalla(
      <AvisoInvitacionPorAprobar vinculo={{ id: 'v1', version: 3, invitacionPorAprobar: retenida({ puedesDecidir: false }) }} empresaId="emp" />,
    );
    expect(screen.getByText(/La decide otra persona de las que aprueban/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /aprobar|rechazar/i })).not.toBeInTheDocument();
    unmount();

    const aprobada = montarPantalla(
      <AvisoInvitacionPorAprobar
        vinculo={{ id: 'v1', version: 4, invitacionPorAprobar: retenida({ estado: 'Aprobada', decididaPor: 'Usuario:u2', decididaPorNombre: 'Carlos Cumplimiento', decididaEn: '2026-11-14T16:00:00Z', puedesDecidir: null }) }}
        empresaId="emp"
      />,
    );
    expect(screen.getByText('Invitación aprobada')).toBeInTheDocument();
    expect(screen.getByText(/Carlos Cumplimiento la aprobó el .* y el correo salió a ana@aceros.co/)).toBeInTheDocument();
    aprobada.unmount();

    montarPantalla(
      <AvisoInvitacionPorAprobar
        vinculo={{ id: 'v1', version: 4, invitacionPorAprobar: retenida({ estado: 'Rechazada', decididaPorNombre: 'Carlos Cumplimiento', decididaEn: '2026-11-14T16:00:00Z', motivoDeRechazo: 'No autorizado.' }) }}
        empresaId="emp"
      />,
    );
    expect(screen.getByText(/Carlos Cumplimiento la rechazó el .* y el vínculo quedó anulado: No autorizado\./)).toBeInTheDocument();
  });
});

describe('bandeja «Invitaciones por aprobar»', () => {
  const RUTA = '/e/emp/invitaciones/por-aprobar';
  const pagina = () => (
    <Routes>
      <Route path="/e/:empresaId/invitaciones/por-aprobar" element={<PaginaInvitacionesPorAprobar />} />
    </Routes>
  );

  it('lista las invitaciones y los lotes que quien consulta decide, y aprueba una con un clic', async () => {
    const usuario = userEvent.setup();
    const peticiones = simularApi({
      'GET /api/v1/invitaciones/por-aprobar': { datos: [ITEM], siguienteCursor: null },
      'GET /api/v1/invitaciones/lotes': { datos: [lote()], siguienteCursor: null },
      'GET /api/v1/usuarios/resumen': [{ id: 'u1', nombre: 'Laura Compras' }],
      'POST /api/v1/vinculos/v1/invitacion/aprobar': { id: 'v1' },
    });
    montarPantalla(pagina(), RUTA);

    expect(await screen.findByText('Aceros del Norte')).toBeInTheDocument();
    expect(screen.getByText('NIT 900123456')).toBeInTheDocument();
    expect(screen.getByText('ana@aceros.co')).toBeInTheDocument();
    expect(screen.getByText('Laura Compras')).toBeInTheDocument();
    expect(await screen.findByText('Proveedores de noviembre')).toBeInTheDocument();
    expect(screen.getByText('PROVEEDOR', { selector: '.MuiChip-label' })).toBeInTheDocument();
    expect(peticiones.find((p) => p.ruta === '/api/v1/invitaciones/por-aprobar')?.url).toContain('soloLasMias=true');
    expect(peticiones.find((p) => p.ruta === '/api/v1/invitaciones/lotes')?.url).toContain('estado=PorAprobar');

    await usuario.click(screen.getByRole('button', { name: 'Aprobar la invitación a Aceros del Norte' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/api/v1/vinculos/v1/invitacion/aprobar')).toBe(true));
  });

  it('rechaza con motivo desde la fila y apaga el filtro para ver las que decide otra persona', async () => {
    const usuario = userEvent.setup();
    const ajena = { ...ITEM, vinculoId: 'v2', terceroNombre: 'Cementos del Sur', puedesDecidir: false };
    const peticiones = simularApi({
      'GET /api/v1/invitaciones/por-aprobar': (p) => ({ datos: p.url.includes('soloLasMias=false') ? [ITEM, ajena] : [ITEM], siguienteCursor: null }),
      'GET /api/v1/invitaciones/lotes': { datos: [lote({ puedesDecidir: false })], siguienteCursor: null },
      'GET /api/v1/usuarios/resumen': [],
      'POST /api/v1/vinculos/v1/invitacion/rechazar': { id: 'v1' },
    });
    montarPantalla(pagina(), RUTA);

    // Con el filtro por omisión no se ven los lotes que decide otra persona.
    expect(await screen.findByText('Aceros del Norte')).toBeInTheDocument();
    expect(screen.queryByText('Proveedores de noviembre')).not.toBeInTheDocument();
    expect(screen.queryByText('Cementos del Sur')).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Rechazar la invitación a Aceros del Norte' }));
    const dialogo = await screen.findByRole('dialog', { name: /Rechazar la invitación/ });
    await usuario.click(within(dialogo).getByRole('textbox', { name: /Motivo/ }));
    await usuario.paste('No procede');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Rechazar' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/api/v1/vinculos/v1/invitacion/rechazar')).toBe(true));
    expect(peticiones.find((p) => p.ruta === '/api/v1/vinculos/v1/invitacion/rechazar')?.cuerpo).toEqual({ motivo: 'No procede' });

    await usuario.click(screen.getByRole('switch', { name: 'Solo las que puedo decidir' }));
    expect(await screen.findByText('Cementos del Sur')).toBeInTheDocument();
    expect(await screen.findByText('Proveedores de noviembre')).toBeInTheDocument();
    // Lo que no decide quien consulta no trae botones.
    expect(screen.queryByRole('button', { name: 'Aprobar la invitación a Cementos del Sur' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aprobar el lote Proveedores de noviembre' })).not.toBeInTheDocument();
  });

  it('aprueba y rechaza un lote completo', async () => {
    const usuario = userEvent.setup();
    const peticiones = simularApi({
      'GET /api/v1/invitaciones/por-aprobar': { datos: [], siguienteCursor: null },
      'GET /api/v1/invitaciones/lotes': { datos: [lote()], siguienteCursor: null },
      'GET /api/v1/usuarios/resumen': [],
      'POST /api/v1/invitaciones/lotes/l1/aprobar': lote({ estado: 'Procesando' }),
      'POST /api/v1/invitaciones/lotes/l1/rechazar': lote({ estado: 'Cancelado' }),
    });
    montarPantalla(pagina(), RUTA);

    expect(await screen.findByText('No tienes invitaciones por decidir.')).toBeInTheDocument();
    await usuario.click(await screen.findByRole('button', { name: 'Aprobar el lote Proveedores de noviembre' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/api/v1/invitaciones/lotes/l1/aprobar')).toBe(true));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el lote Proveedores de noviembre' }));
    const dialogo = await screen.findByRole('dialog', { name: /Rechazar el lote/ });
    await usuario.click(within(dialogo).getByRole('textbox', { name: /Motivo/ }));
    await usuario.paste('No están autorizados');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Rechazar' }));
    await waitFor(() => expect(peticiones.find((p) => p.ruta === '/api/v1/invitaciones/lotes/l1/rechazar')?.cuerpo).toEqual({ motivo: 'No están autorizados' }));
  });

  it('sin vinculos:leer no muestra la bandeja', () => {
    permisos.scopes = new Set();
    simularApi({});
    montarPantalla(pagina(), RUTA);
    expect(screen.queryByText('Aceros del Norte')).not.toBeInTheDocument();
    permisos.scopes = new Set(['vinculos:leer', 'vinculos:escribir']);
  });
});
