import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, SCOPES, type ActualizarMatrizDeRiesgoDto, type MatrizDeRiesgoDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { matrizDePrueba } from '../../cumplimiento/datosPrueba';
import { problema, renderizar, simularApi, type LlamadaApi } from '../../cumplimiento/pruebasApi';
import { PaginaMatrizRiesgo } from './PaginaMatrizRiesgo';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const RUTA = '/api/v1/configuracion/matriz-riesgo';
const ADMIN = new Set<string>([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);

/** Lo que el servidor devolvería al guardar: la matriz enviada con su versión nueva. */
function guardada(dto: ActualizarMatrizDeRiesgoDto, version = 1): MatrizDeRiesgoDto {
  return { ...dto, esPorDefecto: false, version, actualizadaEn: '2026-11-11T15:00:00Z' };
}

function montar(scopes: Set<string> = ADMIN, manejadores: Record<string, (llamada: LlamadaApi) => unknown> = {}, inicial: MatrizDeRiesgoDto = matrizDePrueba()): LlamadaApi[] {
  permisos.scopes = scopes;
  const llamadas = simularApi({ [`GET ${RUTA}`]: () => inicial, ...manejadores });
  renderizar(<PaginaMatrizRiesgo />, '/e/emp/matriz-riesgo');
  return llamadas;
}

const guardar = (llamadas: LlamadaApi[]) => llamadas.filter((l) => l.metodo === 'PUT');

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('Configuración › Matriz de riesgo (CU-17)', () => {
  it('sin configuracion:leer dice que no hay acceso y no pide nada', () => {
    const llamadas = montar(new Set());
    expect(screen.getByRole('alert')).toHaveTextContent('Matriz de riesgo');
    expect(llamadas).toHaveLength(0);
  });

  it('mientras carga muestra un esqueleto, no «Cargando…»', async () => {
    montar();
    expect(screen.getByRole('status', { name: 'Matriz de riesgo' })).toBeInTheDocument();
    expect(screen.queryByText(/Cargando…/)).not.toBeInTheDocument();
    await screen.findByText('Factores');
  });

  it('muestra los factores como tarjetas, los límites con sus tres tramos y qué exige cada nivel', async () => {
    montar();
    await screen.findByText('Factores');
    const sector = within(screen.getByRole('article', { name: 'Sector o actividad económica' }));
    expect(sector.getByText('Manual')).toBeInTheDocument();
    expect(sector.getByText('Pesa 55 % del puntaje')).toBeInTheDocument();
    expect(sector.getByRole('textbox', { name: 'Peso' })).toHaveValue('55');
    expect(sector.getAllByRole('textbox', { name: 'Nombre de la opción' })).toHaveLength(3);
    const jurisdiccion = within(screen.getByRole('article', { name: 'Jurisdicción del tercero' }));
    expect(jurisdiccion.getByText('Automático')).toBeInTheDocument();

    expect(screen.getByRole('img', { name: /Tramos del puntaje: Bajo menos de 34, Medio de 34 a menos de 67 y Alto desde 67\./ })).toBeInTheDocument();
    expect(screen.getByText('Menos de 34')).toBeInTheDocument();
    expect(screen.getByText('De 34 a menos de 67')).toBeInTheDocument();
    expect(screen.getByText('67 o más')).toBeInTheDocument();

    // La debida diligencia se nombra en español: simplificada, ordinaria e intensificada.
    const bajo = screen.getByRole('combobox', { name: /Debida diligencia · 1 · Bajo/ });
    expect(bajo).toHaveTextContent('Simplificada');
    expect(screen.getByRole('combobox', { name: /Debida diligencia · 3 · Alto/ })).toHaveTextContent('Intensificada');
    await userEvent.click(bajo);
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Simplificada', 'Ordinaria', 'Intensificada']);
  });

  it('en los automáticos no se quita el factor ni se agregan opciones; en los manuales sí', async () => {
    montar();
    await screen.findByText('Factores');
    const jurisdiccion = within(screen.getByRole('article', { name: 'Jurisdicción del tercero' }));
    expect(jurisdiccion.queryByRole('button', { name: /Quitar el factor/ })).not.toBeInTheDocument();
    expect(jurisdiccion.queryByRole('button', { name: 'Agregar opción' })).not.toBeInTheDocument();
    expect(jurisdiccion.queryByRole('button', { name: /Quitar la opción/ })).not.toBeInTheDocument();
    expect(jurisdiccion.getByRole('textbox', { name: 'Peso' })).toBeEnabled();

    const sector = within(screen.getByRole('article', { name: 'Sector o actividad económica' }));
    expect(sector.getByRole('button', { name: 'Agregar opción' })).toBeInTheDocument();
    await userEvent.click(sector.getByRole('button', { name: 'Quitar la opción Sector de riesgo medio' }));
    expect(sector.getAllByRole('textbox', { name: 'Nombre de la opción' })).toHaveLength(2);
    await userEvent.click(sector.getByRole('button', { name: 'Quitar el factor Sector o actividad económica' }));
    expect(screen.queryByRole('article', { name: 'Sector o actividad económica' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Agregar factor' }));
    expect(screen.getByRole('article', { name: 'factor sin nombre' })).toBeInTheDocument();
  });

  it('«Usar la plantilla» avisa qué pasará y la guarda activa, con los códigos del servidor', async () => {
    const llamadas = montar(ADMIN, { [`PUT ${RUTA}`]: (l) => guardada(l.cuerpo as ActualizarMatrizDeRiesgoDto) });
    await userEvent.click(await screen.findByRole('button', { name: 'Usar la plantilla' }));
    const dialogo = within(screen.getByRole('dialog'));
    expect(dialogo.getByText(/Se guarda la plantilla sugerida y queda activa\. Los próximos estudios usarán esta matriz; los ya registrados conservan su cálculo\./)).toBeInTheDocument();
    await userEvent.click(dialogo.getByRole('button', { name: 'Usar la plantilla' }));

    await waitFor(() => expect(guardar(llamadas)).toHaveLength(1));
    const dto = guardar(llamadas)[0]?.cuerpo as ActualizarMatrizDeRiesgoDto;
    expect(dto.activa).toBe(true);
    expect(dto.factores.map((f) => f.codigo)).toEqual(['jurisdiccion', 'coincidencias-listas', 'sector']);
    expect(dto.factores[1]?.opciones.map((o) => o.nivelMinimo)).toEqual([null, null, 2, 3]);
    expect(dto.niveles.map((n) => n.debidaDiligencia)).toEqual(['Simple', 'Ordinaria', 'Ampliada']);
    // Ya guardada: desaparece el aviso de la plantilla.
    await waitFor(() => expect(screen.queryByText('Aún no tienes una matriz propia')).not.toBeInTheDocument());
  });

  it('editar marca los cambios, avisa antes de guardar y deja descartarlos', async () => {
    const llamadas = montar(ADMIN, { [`PUT ${RUTA}`]: (l) => guardada(l.cuerpo as ActualizarMatrizDeRiesgoDto) });
    await screen.findByText('Factores');
    const guardarBoton = screen.getByRole('button', { name: 'Guardar matriz' });
    expect(guardarBoton).toBeDisabled();

    const peso = within(screen.getByRole('article', { name: 'Sector o actividad económica' })).getByRole('textbox', { name: 'Peso' });
    await userEvent.clear(peso);
    await userEvent.type(peso, '40');
    expect(screen.getByText('Hay cambios sin guardar.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('switch', { name: 'Calcular el riesgo con esta matriz' }));
    expect(screen.getByText('El nivel de riesgo y la debida diligencia salen de la matriz. El oficial solo puede ajustar el nivel con una justificación.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Descartar cambios' }));
    expect(within(screen.getByRole('article', { name: 'Sector o actividad económica' })).getByRole('textbox', { name: 'Peso' })).toHaveValue('55');
    expect(screen.getByRole('button', { name: 'Guardar matriz' })).toBeDisabled();

    const peso2 = within(screen.getByRole('article', { name: 'Sector o actividad económica' })).getByRole('textbox', { name: 'Peso' });
    await userEvent.clear(peso2);
    await userEvent.type(peso2, '40');
    await userEvent.click(screen.getByRole('switch', { name: 'Calcular el riesgo con esta matriz' }));
    await userEvent.click(screen.getByRole('button', { name: 'Guardar matriz' }));
    const dialogo = within(screen.getByRole('dialog'));
    expect(dialogo.getByText(/Los próximos estudios usarán esta matriz; los ya registrados conservan su cálculo\. La matriz queda activa/)).toBeInTheDocument();
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar matriz' }));
    await waitFor(() => expect(guardar(llamadas)).toHaveLength(1));
    expect((guardar(llamadas)[0]?.cuerpo as ActualizarMatrizDeRiesgoDto).factores[2]?.peso).toBe(40);
  }, 60_000);

  it('no deja guardar con un peso o límites inválidos y dice cuántas cosas corregir', async () => {
    const llamadas = montar();
    await screen.findByText('Factores');
    const peso = within(screen.getByRole('article', { name: 'Sector o actividad económica' })).getByRole('textbox', { name: 'Peso' });
    await userEvent.clear(peso);
    await userEvent.type(peso, '0');
    expect(screen.getByText('El peso va de 1 a 100.')).toBeInTheDocument();
    expect(screen.getByText('Corrige 1 cosa para guardar.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar matriz' })).toBeDisabled();

    const medio = screen.getByRole('textbox', { name: 'Medio si es menor que' });
    await userEvent.clear(medio);
    await userEvent.type(medio, '20');
    expect(screen.getByText('Los límites van de 1 a 100 y el de Bajo debe ser menor que el de Medio.')).toBeInTheDocument();
    expect(screen.getByText('Corrige 2 cosas para guardar.')).toBeInTheDocument();
    expect(guardar(llamadas)).toHaveLength(0);
  }, 40_000);

  it('un error 422 de la API se muestra en español', async () => {
    montar(ADMIN, { [`PUT ${RUTA}`]: () => problema(422, 'niveles-invalidos') });
    await userEvent.click(await screen.findByRole('button', { name: 'Usar la plantilla' }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Usar la plantilla' }));
    expect(await screen.findByText(/Revisa los niveles: la debida diligencia no baja al subir el riesgo, Alto exige intensificada/)).toBeInTheDocument();
  });

  it('sin configuracion:escribir se ve pero no se cambia', async () => {
    montar(new Set([SCOPES.configuracionLeer]));
    await screen.findByText('Factores');
    expect(screen.getByText(/Puedes ver la matriz; para cambiarla necesitas el permiso de escritura/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Guardar matriz' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Usar la plantilla' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Agregar factor' })).not.toBeInTheDocument();
    expect(within(screen.getByRole('article', { name: 'Sector o actividad económica' })).getByRole('textbox', { name: 'Peso' })).toBeDisabled();
    expect(screen.getByRole('switch', { name: 'Calcular el riesgo con esta matriz' })).toBeDisabled();
  });

  it('prueba la matriz con el puntaje y nivel de una elección', async () => {
    montar();
    await screen.findByText('Prueba la matriz');
    const prueba = within(screen.getByRole('region', { name: 'Prueba la matriz' }));
    await userEvent.click(prueba.getByRole('combobox', { name: 'Sector o actividad económica' }));
    await userEvent.click(screen.getByRole('option', { name: 'Sector de alto riesgo' }));
    // (55·100) / 100 = 55 → Medio.
    const resultado = within(prueba.getByRole('status', { name: 'Prueba la matriz' }));
    expect(resultado.getByText('55,0')).toBeInTheDocument();
    expect(resultado.getByText('2 · Medio')).toBeInTheDocument();
    expect(resultado.getByText('Revisión cada 12 meses')).toBeInTheDocument();

    await userEvent.click(prueba.getByRole('combobox', { name: 'Coincidencias en listas restrictivas' }));
    await userEvent.click(screen.getByRole('option', { name: 'Coincidencia confirmada' }));
    expect(resultado.getByText('3 · Alto')).toBeInTheDocument();
  }, 40_000);
});
