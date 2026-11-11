import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { expediente } from '../../expedientes/datosPrueba';
import { adjuntoDePrueba, coincidencia, estudio, listas, matrizDePrueba } from '../datosPrueba';
import { problema, renderizar, simularApi, type LlamadaApi } from '../pruebasApi';
import { PanelVerificaciones } from './PanelVerificaciones';

const OFICIAL = new Set([SCOPES.expedientesLeer, SCOPES.verificacionesEscribir, SCOPES.cumplimientoDecidir, SCOPES.documentosDescargar]);
const E1 = '/api/v1/expedientes/e1';

function montar(datos: ExpedienteDto, scopes: ReadonlySet<string> = OFICIAL, manejadores: Record<string, (llamada: LlamadaApi) => unknown> = {}): LlamadaApi[] {
  const llamadas = simularApi({
    [`GET /api/v1/configuracion/matriz-riesgo`]: () => matrizDePrueba({ activa: false }),
    [`POST ${E1}/verificaciones/ListasRestrictivas/coincidencias/c1/resolver`]: () => datos,
    ...manejadores,
  });
  renderizar(<PanelVerificaciones expediente={datos} empresaId="emp" scopes={scopes} nombres={new Map([['u-ofi', 'Carlos Cumplimiento']])} />);
  return llamadas;
}

afterEach(() => {
  reiniciarCliente();
  vi.unstubAllGlobals();
});

describe('Panel de verificaciones del expediente (H7)', () => {
  it('muestra la coincidencia de la persona relacionada y pide justificación para descartarla', async () => {
    const datos = { ...expediente('EnRevision'), verificaciones: [listas(), estudio()], esperandoVerificaciones: true };
    const llamadas = montar(datos);

    const seccion = within(screen.getByRole('region', { name: 'Listas restrictivas' }));
    expect(seccion.getByText('Coincidencia por analizar')).toBeInTheDocument();
    const fila = within(seccion.getByRole('listitem', { name: 'Ana Coincidencia · OFAC (SDN)' }));
    expect(fila.getByText(/Representante legal · CC 1020304050/)).toBeInTheDocument();

    await userEvent.click(fila.getByRole('button', { name: 'Descartar' }));
    const dialogo = within(screen.getByRole('dialog'));
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar decisión' }));
    expect(dialogo.getByText('Escribe la justificación.')).toBeInTheDocument();
    expect(llamadas).toHaveLength(0);

    await userEvent.type(dialogo.getByLabelText(/Justificación/), 'Homónimo: otra cédula.');
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar decisión' }));
    const resolver = llamadas.find((l) => l.ruta.endsWith('/coincidencias/c1/resolver'));
    expect(resolver?.cuerpo).toEqual({ decision: 'Descartada', justificacion: 'Homónimo: otra cédula.' });
  });

  it('sin cumplimiento:decidir no ofrece resolver ni registrar el estudio; una decisión heredada dice hasta cuándo vale', () => {
    const resuelta = coincidencia({ decision: 'Descartada', heredada: true, vigenteHasta: '2027-11-10T14:00:00Z', justificacion: 'Homónimo.' });
    montar({ ...expediente('EnRevision'), verificaciones: [listas({ estado: 'Satisfecha', coincidencias: [resuelta] }), estudio()] }, new Set([SCOPES.expedientesLeer]));

    expect(screen.queryByRole('button', { name: 'Descartar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar estudio' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Reconsultar/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar consulta hecha por fuera' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rechazar por cumplimiento' })).not.toBeInTheDocument();
    expect(screen.getByText(/Decisión de una consulta anterior, vigente hasta el 10\/11\/2027/)).toBeInTheDocument();
    expect(screen.getByText('Falta el estudio del oficial de cumplimiento.')).toBeInTheDocument();
  });

  it('el estudio exige debida diligencia intensificada con riesgo alto antes de enviarlo', async () => {
    const llamadas = montar({ ...expediente('EnRevision'), verificaciones: [estudio()] });
    await userEvent.click(screen.getByRole('button', { name: 'Registrar estudio' }));
    const dialogo = within(await screen.findByRole('dialog'));
    await userEvent.click(await dialogo.findByRole('button', { name: '3 · Alto' }));
    await userEvent.click(dialogo.getByRole('combobox', { name: 'Concepto' }));
    await userEvent.click(screen.getByRole('option', { name: 'Favorable' }));
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(dialogo.getByText('Con riesgo alto la debida diligencia debe ser intensificada.')).toBeInTheDocument();
    expect(llamadas.some((l) => l.metodo === 'POST')).toBe(false);
  });

  it('un expediente sin verificaciones no muestra el panel', () => {
    montar({ ...expediente('EnRevision'), verificaciones: [] });
    expect(screen.queryByRole('region', { name: 'Verificaciones' })).not.toBeInTheDocument();
  });
});

describe('Estudio guardado con matriz y archivos', () => {
  it('muestra el nivel, la matriz con su ajuste y los archivos del oficial', () => {
    const guardado = estudio({
      estado: 'Satisfecha',
      estudio: {
        verificadoEnListas: true,
        conflictoDeInteres: false,
        debidaDiligencia: 'Ampliada',
        nivelRiesgo: 3,
        observaciones: null,
        evidencias: [],
        concepto: 'Favorable',
        oficial: 'u-ofi',
        fecha: '2026-11-10T14:00:00Z',
        vigenteHasta: '2026-12-10T14:00:00Z',
        justificacionDeNivel: 'Opera con efectivo en frontera.',
        adjuntos: [adjuntoDePrueba({ nombre: 'soporte-estudio.pdf' })],
        matriz: {
          puntaje: 42.5,
          nivel: 2,
          nivelPorPuntaje: 2,
          forzadoPor: null,
          debidaDiligencia: 'Ordinaria',
          mesesRevision: 12,
          versionMatriz: 3,
          desglose: [],
        },
      },
    });
    montar({ ...expediente('EnRevision'), verificaciones: [guardado] });
    const seccion = within(screen.getByRole('region', { name: 'Estudio de cumplimiento' }));
    expect(seccion.getByText('Intensificada')).toBeInTheDocument();
    expect(seccion.getByText('42,5 de 100')).toBeInTheDocument();
    expect(seccion.getByText('Ajustado de 2 · Medio a 3 · Alto')).toBeInTheDocument();
    expect(seccion.getByText(/Opera con efectivo en frontera\./)).toBeInTheDocument();
    expect(seccion.getByText('soporte-estudio.pdf')).toBeInTheDocument();
    expect(seccion.getByRole('button', { name: 'Abrir soporte-estudio.pdf' })).toBeInTheDocument();
  });
});

describe('Consulta de listas hecha por fuera (CU-16)', () => {
  const sinCoincidencias = { ...expediente('EnRevision'), verificaciones: [listas({ estado: 'Satisfecha', coincidencias: [] }), estudio()] };
  const personas = {
    [`GET ${E1}/verificaciones/ListasRestrictivas/personas`]: () => ({
      personas: [
        { rol: 'Tercero', nombre: 'Aceros del Norte S.A.S.', identificacion: 'NIT 900123456' },
        { rol: 'RepresentanteLegal', nombre: 'Ana Coincidencia', identificacion: 'CC 1020304050' },
      ],
      claves: ['TERCERO|NIT|900123456', 'RL|CC|1020304050'],
    }),
    [`PUT ${E1}/cumplimiento/adjuntos`]: () => ({ cargaId: 'carga-1', carga: { metodo: 'PUT', url: 'https://almacen.test/subida', cabeceras: {}, expira: '2026-11-11T16:00:00Z' } }),
    [`POST ${E1}/cumplimiento/adjuntos/carga-1/confirmar`]: () => adjuntoDePrueba({ id: 'adj-9', nombre: 'consulta.pdf', proposito: 'ConsultaManual' }),
    [`POST ${E1}/verificaciones/ListasRestrictivas/consulta-manual`]: () => ({ ...sinCoincidencias, verificaciones: [listas({ proveedor: 'Manual · Infolaft web' }), estudio()] }),
  };

  async function abrirYSubir() {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(null, { status: 200 }))));
    await userEvent.click(screen.getByRole('button', { name: 'Registrar consulta hecha por fuera' }));
    const dialogo = within(await screen.findByRole('dialog', { name: /Consulta de listas hecha por fuera/ }));
    await userEvent.upload(screen.getByTestId('entrada-ConsultaManual'), new File(['%PDF-1.4'], 'consulta.pdf', { type: 'application/pdf' }));
    await dialogo.findByText(/consulta\.pdf/);
    return dialogo;
  }

  it('el botón es de quien tiene verificaciones:escribir y el expediente está abierto', () => {
    montar(sinCoincidencias, new Set([SCOPES.expedientesLeer, SCOPES.cumplimientoDecidir]));
    expect(screen.queryByRole('button', { name: 'Registrar consulta hecha por fuera' })).not.toBeInTheDocument();
  });

  it('sin coincidencias: sube el PDF, dice la fuente y la fecha y registra la consulta', async () => {
    const llamadas = montar(sinCoincidencias, OFICIAL, personas);
    const dialogo = await abrirYSubir();
    expect(dialogo.getByText('Sin coincidencias, las listas quedan satisfechas. Queda registrado con tu nombre y el PDF.')).toBeInTheDocument();

    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar consulta' }));
    expect(dialogo.getByText('Indica dónde la hiciste (de 3 a 100 caracteres).')).toBeInTheDocument();
    await userEvent.type(dialogo.getByRole('textbox', { name: /Dónde la hiciste/ }), 'Infolaft web');
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar consulta' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const registro = llamadas.find((l) => l.ruta.endsWith('/consulta-manual'));
    expect(registro?.cuerpo).toMatchObject({ adjuntoId: 'adj-9', fuente: 'Infolaft web', coincidencias: [], mesesVigencia: null });
    expect((registro?.cuerpo as { consultadaEn: string }).consultadaEn).toMatch(/^\d{4}-\d{2}-\d{2}T/);
  });

  it('con coincidencias: cada una lleva la persona de la consulta, la lista y el nombre encontrado', async () => {
    const llamadas = montar(sinCoincidencias, OFICIAL, personas);
    const dialogo = await abrirYSubir();
    await userEvent.type(dialogo.getByRole('textbox', { name: /Dónde la hiciste/ }), 'Infolaft web');
    await userEvent.click(dialogo.getByRole('button', { name: 'Sí, hubo' }));
    expect(dialogo.getByText('Con coincidencias, las listas quedan por analizar: tú decides si cada una se descarta o se confirma.')).toBeInTheDocument();

    const fila = within(await dialogo.findByRole('listitem', { name: 'Coincidencia 1' }));
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar consulta' }));
    expect(dialogo.getByText('Completa la persona, la lista y el nombre de cada coincidencia.')).toBeInTheDocument();
    expect(llamadas.some((l) => l.ruta.endsWith('/consulta-manual'))).toBe(false);

    await userEvent.click(fila.getByRole('combobox', { name: 'Persona' }));
    await userEvent.click(screen.getByRole('option', { name: /Ana Coincidencia · Representante legal · CC 1020304050/ }));
    await userEvent.type(fila.getByRole('textbox', { name: 'Lista' }), 'OFAC');
    await userEvent.type(fila.getByRole('textbox', { name: 'Nombre encontrado' }), 'ANA COINCIDENCIA');
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar consulta' }));

    await waitFor(() => expect(llamadas.some((l) => l.ruta.endsWith('/consulta-manual'))).toBe(true));
    const registro = llamadas.find((l) => l.ruta.endsWith('/consulta-manual'));
    expect((registro?.cuerpo as { coincidencias: unknown[] }).coincidencias).toEqual([
      { clavePersona: 'RL|CC|1020304050', lista: 'OFAC', nombreEncontrado: 'ANA COINCIDENCIA', detalle: null, puntaje: null },
    ]);
  }, 60_000);

  it('se distingue en la lista: «Manual · fuente» con el PDF subido por el oficial, también entre las anteriores', () => {
    const manual = listas({
      estado: 'Satisfecha',
      coincidencias: [],
      proveedor: 'Manual · Infolaft web',
      registradaPor: 'u-ofi',
      anteriores: [{ proveedor: 'Manual · Otro sitio', referenciaExterna: 'manual-1', resultado: 'SinCoincidencias', fecha: '2026-10-20T14:00:00Z', coincidencias: 0, conEvidencia: true }],
    });
    montar({ ...expediente('EnRevision'), verificaciones: [manual, estudio()] });
    const seccion = within(screen.getByRole('region', { name: 'Listas restrictivas' }));
    expect(seccion.getByText('Manual · Infolaft web')).toBeInTheDocument();
    expect(seccion.getByText('PDF subido por Carlos Cumplimiento')).toBeInTheDocument();
    expect(seccion.getAllByText('Manual')).toHaveLength(2);
    expect(seccion.getByText('1 consulta anterior')).toBeInTheDocument();
  });

  it('el error de la API (fuera de plazo) se muestra y el diálogo sigue abierto', async () => {
    montar(sinCoincidencias, OFICIAL, { ...personas, [`POST ${E1}/verificaciones/ListasRestrictivas/consulta-manual`]: () => problema(422, 'consulta-fuera-de-plazo') });
    const dialogo = await abrirYSubir();
    await userEvent.type(dialogo.getByRole('textbox', { name: /Dónde la hiciste/ }), 'Infolaft web');
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar consulta' }));
    expect(await dialogo.findByText('La consulta debe ser de los últimos 30 días y no futura; si es más vieja, hazla de nuevo.')).toBeInTheDocument();
  }, 60_000);
});

describe('Rechazo por cumplimiento (CU-18)', () => {
  const fallida = { ...expediente('EnRevision'), verificaciones: [listas({ estado: 'Fallida', coincidencias: [coincidencia({ decision: 'Confirmada', justificacion: 'Es la misma persona.', resueltaPor: 'u-ofi', resueltaEn: '2026-11-10T15:00:00Z' })] }), estudio()] };
  const motivos = {
    'GET /api/v1/motivos': () => [
      { id: 'm1', codigo: 'COINCIDENCIA', nombre: 'Coincidencia en listas restrictivas', categoria: 'Otro', aplicaA: 'Rechazo', textoAlTercero: 'Tu nombre aparece en una lista restrictiva.', base: true, activo: true },
      { id: 'm2', codigo: 'FALTA_RUT', nombre: 'Falta el RUT', categoria: 'Documento', aplicaA: 'Observacion', textoAlTercero: 'Falta tu RUT.', base: true, activo: true },
    ],
    [`POST ${E1}/cumplimiento/rechazar`]: () => ({ ...fallida, estado: 'Rechazado', rechazo: { areaId: '00000000-0000-0000-0000-000000000000', usuario: 'u-ofi', motivos: ['COINCIDENCIA'], justificacion: 'x', fecha: '2026-11-11T14:00:00Z', origen: 'Cumplimiento' } }),
  };

  it('solo se ofrece con una verificación fallida, el expediente en revisión y cumplimiento:decidir', () => {
    montar({ ...expediente('EnRevision'), verificaciones: [listas({ estado: 'CoincidenciaPorAnalizar' }), estudio()] });
    expect(screen.queryByRole('button', { name: 'Rechazar por cumplimiento' })).not.toBeInTheDocument();
  });

  it('con el hallazgo y el permiso: avisa que es definitivo, pide motivos del catálogo de rechazo y justificación, y rechaza', async () => {
    const llamadas = montar(fallida, OFICIAL, motivos);
    await userEvent.click(screen.getByRole('button', { name: 'Rechazar por cumplimiento' }));
    const dialogo = within(await screen.findByRole('dialog', { name: /Rechazar por cumplimiento/ }));
    expect(dialogo.getByText(/El rechazo es definitivo: el expediente se cierra, el vínculo vuelve a Invitado y el tercero recibe por correo/)).toBeInTheDocument();
    expect(dialogo.getByText(/Hallazgo que lo permite: una coincidencia confirmada en listas restrictivas/)).toBeInTheDocument();
    // Solo los motivos de rechazo: el de observación no aparece.
    const motivo = await dialogo.findByRole('checkbox', { name: /Coincidencia en listas restrictivas/ });
    expect(dialogo.queryByRole('checkbox', { name: /Falta el RUT/ })).not.toBeInTheDocument();
    const rechazar = dialogo.getByRole('button', { name: 'Rechazar expediente' });
    expect(rechazar).toBeDisabled();
    expect(dialogo.getByText('Elige al menos un motivo.')).toBeInTheDocument();

    await userEvent.click(motivo);
    await userEvent.type(dialogo.getByRole('textbox', { name: /Justificación/ }), 'Coincidencia confirmada con OFAC.');
    await userEvent.click(rechazar);
    await waitFor(() => expect(llamadas.some((l) => l.ruta.endsWith('/cumplimiento/rechazar'))).toBe(true));
    expect(llamadas.find((l) => l.ruta.endsWith('/cumplimiento/rechazar'))?.cuerpo).toEqual({ motivos: ['COINCIDENCIA'], justificacion: 'Coincidencia confirmada con OFAC.' });
  }, 60_000);

  it('un estudio desfavorable también habilita el rechazo', () => {
    montar({ ...expediente('Observado'), verificaciones: [listas({ estado: 'Satisfecha', coincidencias: [] }), estudio({ estado: 'Fallida' })] });
    expect(screen.getByRole('button', { name: 'Rechazar por cumplimiento' })).toBeInTheDocument();
  });
});
