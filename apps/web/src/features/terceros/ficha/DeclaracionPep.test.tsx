import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { guardarArchivo } from '../../vencimientos/api';
import { DatosDeSeccion } from './DatosDeSeccion';

vi.mock('../../vencimientos/api', () => ({ guardarArchivo: vi.fn() }));

const DECLARACION = {
  relacion_empresa: 'Socio',
  nombre: 'Jorge Iván Castro Pinto',
  tipo_identificacion: 'CC',
  numero: '79555111',
  nacionalidad: 'CO',
  direccion: 'Calle 45 # 12-08',
  cargo: 'Alcalde',
  institucion: 'Alcaldía de Soacha',
  pais: 'CO',
  fecha_vinculacion: '2020-01-01',
  vigente: true,
  cuentas_exterior: true,
  paises_cuentas_exterior: ['US', 'ES'],
  conyuge: { nombre: 'Luisa Pérez', tipo_identificacion: 'CC', numero: '52000111' },
  familiares: [{ nombre: 'Luis Castro', parentesco: 'Hermano', tipo_identificacion: 'CC', numero: '80111222' }],
  sociedades: [{ nombre: 'Inversiones Andinas S.A.S.', identificacion: '800111222', calidad: 'Otra', calidad_otra: 'Fideicomitente suplente' }],
  vinculo_con_pep: {
    tiene: true,
    vinculos: [
      { tipo: 'Familiar', relacion: 'Padre / Madre · 1.° consanguinidad', nombre: 'Rosa Pinto', cargo: 'Senadora', entidad: 'Senado', pais: 'CO', fecha_vinculacion: '2018-05-05', vigente: true },
      { tipo: 'AsociadoCercano', relacion: 'Socio(a) o asociado(a) comercial', nombre: 'Tomás Rey', cargo: 'Alcalde', entidad: 'Alcaldía de Sopó', pais: 'CO', fecha_vinculacion: '2019-01-01', fecha_desvinculacion: '2021-01-01', vigente: false },
    ],
  },
  acepta_juramento: true,
  firma_en_calidad: 'RepresentanteLegal',
  firmante_nombre: 'Pedro Pérez',
  firmante_tipo_identificacion: 'CC',
  firmante_numero: '80123456',
  firma_png: 'iVBORw0KGgo=',
  firma_huella: 'abcdef0123456789abcdef',
  fecha_firma: '2026-11-01',
  firma_ip: '10.0.0.1',
};

const PERSONAS = {
  personas: [
    { rol: 'Socio', tipo_identificacion: 'CC', numero: '79555111', nombres: 'Jorge Iván', apellidos: 'Castro Pinto', porcentaje: 60, pep: true, vinculo_pep: false, declaracion_pep: DECLARACION },
    { rol: 'RepresentanteLegal', tipo_identificacion: 'CC', numero: '1020304050', nombres: 'Ana', apellidos: 'Gómez', pep: false, vinculo_pep: false, declaracion_pep: { ...DECLARACION, cargo: 'Concejal' } },
  ],
};

function montar(datos: Record<string, unknown>, extra: { seccion?: string; version?: number | null; terceroId?: string } = {}) {
  const peticiones: { url: string; empresa: string | null }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const cabeceras = new Headers(init?.headers);
      peticiones.push({ url, empresa: cabeceras.get('X-Empresa') });
      return Promise.resolve(new Response(new Uint8Array([37, 80, 68, 70]), { headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename="declaracion-pep-79555111.pdf"' } }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <DatosDeSeccion datos={datos} empresaId="emp" terceroId={extra.terceroId ?? 't1'} seccion={extra.seccion ?? 'FICHA_LEGAL'} version={extra.version ?? 3} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  vi.mocked(guardarArchivo).mockClear();
});

describe('Declaración jurada PEP en los datos de la sección (empresa)', () => {
  it('aparece solo en la persona con «Sí» en PEP: la que respondió «No» no la muestra aunque la conserve inactiva', () => {
    montar(PERSONAS);
    const socio = screen.getByRole('listitem', { name: 'Jorge Iván Castro Pinto · Socio o accionista' });
    expect(within(socio).getByRole('region', { name: 'Declaración jurada PEP' })).toBeInTheDocument();
    const representante = screen.getByRole('listitem', { name: 'Ana Gómez · Representante legal' });
    expect(within(representante).queryByRole('region', { name: 'Declaración jurada PEP' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Descargar el PDF de la declaración PEP/ })).toHaveLength(1);
    // Lo inactivo no se vuelca en la lista genérica.
    expect(screen.queryByText('Concejal')).not.toBeInTheDocument();
  });

  it('resume las cuatro secciones y la firma, sin mostrar la imagen ni la IP', () => {
    montar(PERSONAS);
    const bloque = screen.getByRole('region', { name: 'Declaración jurada PEP' });
    expect(bloque).toHaveTextContent('Firmada el 01/11/2026');
    expect(bloque).toHaveTextContent('Relación con la empresaSocio');
    expect(bloque).toHaveTextContent('Cargo desempeñadoAlcalde');
    expect(bloque).toHaveTextContent('InstituciónAlcaldía de Soacha');
    expect(bloque).toHaveTextContent('Vinculación01/01/2020 – Vigente');
    expect(bloque).toHaveTextContent('Cuentas financieras en el exteriorSí · Estados Unidos (US), España (ES)');
    expect(bloque).toHaveTextContent('Luisa Pérez · CC 52000111');
    expect(bloque).toHaveTextContent('Luis Castro · Hermano · CC 80111222');
    expect(bloque).toHaveTextContent('Inversiones Andinas S.A.S. · 800111222 · Fideicomitente suplente');
    expect(bloque).toHaveTextContent('Familiar: Padre / Madre · 1.° consanguinidad · Rosa Pinto · Senadora, Senado, Colombia (CO) · 05/05/2018 – Vigente');
    expect(bloque).toHaveTextContent('Asociado cercano: Socio(a) o asociado(a) comercial · Tomás Rey · Alcalde, Alcaldía de Sopó, Colombia (CO) · 01/01/2019 – 01/01/2021');
    expect(bloque).toHaveTextContent('Como representante legal: Pedro Pérez (CC 80123456) · Huella abcdef012345…');
    expect(bloque.textContent).not.toContain('iVBORw0KGgo=');
    expect(bloque.textContent).not.toContain('10.0.0.1');
  });

  it('«Descargar PDF» pide el PDF de esa persona (su posición) y la versión que se está viendo, con la empresa', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(PERSONAS, { version: 3 });
    await usuario.click(screen.getByRole('button', { name: 'Descargar el PDF de la declaración PEP de Jorge Iván Castro Pinto' }));

    await waitFor(() => expect(guardarArchivo).toHaveBeenCalledTimes(1));
    expect(peticiones).toHaveLength(1);
    expect(peticiones[0]?.url).toBe('/api/v1/terceros/t1/declaraciones-pep/pdf?seccion=FICHA_LEGAL&persona=0&version=3');
    expect(peticiones[0]?.empresa).toBe('emp');
    expect(vi.mocked(guardarArchivo).mock.calls[0]?.[0].nombre).toBe('declaracion-pep-79555111.pdf');
  });

  it('en la General de una persona natural la declaración va como bloque propio y el PDF no lleva «persona»', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar({ nombres: 'Jorge Iván', apellidos: 'Castro Pinto', pep: true, vinculo_pep: false, declaracion_pep: DECLARACION }, { seccion: 'FICHA_GENERAL', version: 2 });
    expect(screen.getByRole('region', { name: 'Declaración jurada PEP' })).toHaveTextContent('Cargo desempeñadoAlcalde');
    await usuario.click(screen.getByRole('button', { name: /^Descargar el PDF de la declaración PEP/ }));
    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]?.url).toBe('/api/v1/terceros/t1/declaraciones-pep/pdf?seccion=FICHA_GENERAL&version=2');
  });

  it('con «No» en PEP la General no muestra la declaración conservada', () => {
    montar({ nombres: 'Jorge Iván', pep: false, declaracion_pep: DECLARACION }, { seccion: 'FICHA_GENERAL' });
    expect(screen.queryByRole('region', { name: 'Declaración jurada PEP' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Descargar/ })).not.toBeInTheDocument();
  });

  it('un PEP sin declaración (dato heredado) solo muestra el «Sí», sin bloque ni PDF', () => {
    montar({ personas: [{ rol: 'Socio', nombres: 'Jorge', pep: true }] });
    expect(screen.queryByRole('region', { name: 'Declaración jurada PEP' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Descargar/ })).not.toBeInTheDocument();
  });
});
