import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type GrupoDto, type PlanIaEmpresaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { borradorEmpresaNuevo } from '../configuracion/empresas/modelo';
import {
  borradorDesdePlan,
  borradorGrupoNuevo,
  cuerpoAltaGrupo,
  cuerpoPlanIa,
  estadoPlanIa,
  slugDesdeNombre,
  validarGrupo,
  validarPlanIa,
  type BorradorGrupo,
} from './modelo';
import { PaginaPlanIa } from './PaginaPlanIa';

const PEM = '-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA\n-----END PUBLIC KEY-----';

const PLAN: PlanIaEmpresaDto = {
  empresaId: 'e1',
  configurado: true,
  validacionesIncluidasMes: 200,
  adicionalHabilitado: true,
  topeAdicionalMes: 50,
  llaveConfigurada: true,
  ultimosCuatro: 'WXYZ',
  modeloPorDefecto: 'gpt-6-luna',
  modeloEscalamiento: 'gpt-6.1-terra',
  umbralConfianza: 0.85,
  activo: true,
  actualizadoEn: '2026-11-09T15:00:00Z',
};

function grupoCompleto(): BorradorGrupo {
  return {
    ...borradorGrupoNuevo({ ...borradorEmpresaNuevo(), numero: '800197268', nombre: 'Andina de Obras S.A.S.', prefijo: 'ANDINA' }),
    nombre: 'Grupo Andino',
    slug: 'grupo-andino',
    adminNombre: 'Camila Admin',
    adminCorreo: 'camila@andina.co',
    sistemaCodigo: 'erp',
    sistemaNombre: 'ERP de Andina',
    sistemaClave: PEM,
    sistemaOrigenes: 'https://ERP.andina.co/\nhttps://erp.andina.co',
  };
}

describe('alta de grupo', () => {
  it('deriva el identificador del nombre sin tildes ni símbolos', () => {
    expect(slugDesdeNombre('Grupo Constructor Ñandú S.A.S.')).toBe('grupo-constructor-nandu-s-a-s');
    expect(slugDesdeNombre('  --Ácme  ')).toBe('acme');
    expect(slugDesdeNombre('x'.repeat(40))).toHaveLength(30);
  });

  it('valida cada parte en el orden del formulario', () => {
    expect(validarGrupo(grupoCompleto(), undefined)).toEqual([]);
    const vacio = borradorGrupoNuevo(borradorEmpresaNuevo());
    expect(validarGrupo(vacio, undefined)).toEqual([
      'grupo-nombre-requerido',
      'slug-invalido',
      'empresa-numero-requerido',
      'empresa-nombre-requerido',
      'empresa-prefijo-invalido',
      'admin-nombre-requerido',
      'admin-correo-invalido',
      'sistema-codigo-invalido',
      'sistema-nombre-requerido',
      'clave-publica-requerida',
    ]);
    expect(validarGrupo({ ...grupoCompleto(), sistemaOrigenes: 'http://erp.andina.co' }, undefined)).toEqual(['origen-invalido']);
    expect(validarGrupo({ ...grupoCompleto(), sistemaClave: '-----BEGIN PRIVATE KEY-----\nx\n-----END PRIVATE KEY-----' }, undefined)).toEqual([
      'clave-publica-requerida',
    ]);
  });

  it('arma el cuerpo con empresa, administrador y sistema normalizados', () => {
    const cuerpo = cuerpoAltaGrupo({ ...grupoCompleto(), adminIdExterno: '  camila ' });
    expect(cuerpo).toMatchObject({
      slug: 'grupo-andino',
      zonaHoraria: 'America/Bogota',
      empresa: { nombre: 'Andina de Obras S.A.S.', prefijoFolio: 'ANDINA', identificacion: { pais: 'CO', tipo: 'NIT', numero: '800197268' } },
      administrador: { nombre: 'Camila Admin', correo: 'camila@andina.co', idExterno: 'camila' },
      sistema: { codigo: 'ERP', origenesPermitidos: ['https://erp.andina.co'] },
    });
    expect(cuerpoAltaGrupo(grupoCompleto()).administrador?.idExterno).toBeNull();
  });
});

describe('plan de IA', () => {
  it('la llave se conserva, se reemplaza o se quita (nula, nueva o vacía)', () => {
    const b = borradorDesdePlan(PLAN);
    expect(cuerpoPlanIa(b).apiKeyOpenAi).toBeNull();
    expect(cuerpoPlanIa({ ...b, llave: { tipo: 'quitar' } }).apiKeyOpenAi).toBe('');
    expect(cuerpoPlanIa({ ...b, llave: { tipo: 'reemplazar', valor: ' sk-proj-0123456789abcdefghij ' } }).apiKeyOpenAi).toBe('sk-proj-0123456789abcdefghij');
    expect(validarPlanIa({ ...b, llave: { tipo: 'reemplazar', valor: 'corta' } })).toEqual(['llave-invalida']);
  });

  it('valida cupo, tope, modelos y umbral; sin adicional no hay tope', () => {
    const b = borradorDesdePlan(PLAN);
    expect(validarPlanIa(b)).toEqual([]);
    expect(validarPlanIa({ ...b, incluidas: '-1', tope: 'x', umbral: '1,5', modeloPorDefecto: 'con espacio' })).toEqual([
      'incluidas-invalidas',
      'tope-invalido',
      'modelo-invalido',
      'umbral-invalido',
    ]);
    expect(cuerpoPlanIa({ ...b, adicional: false }).topeAdicionalMes).toBeNull();
    expect(cuerpoPlanIa({ ...b, tope: '' }).topeAdicionalMes).toBeNull();
    expect(cuerpoPlanIa({ ...b, umbral: '0,9' }).umbralConfianza).toBe(0.9);
  });

  it('un plan sin configurar se propone activo; el estado de la lista distingue la falta de llave', () => {
    expect(borradorDesdePlan({ ...PLAN, configurado: false, activo: false }).activo).toBe(true);
    expect(estadoPlanIa({ planIaConfigurado: false, planIaActivo: false, llaveConfigurada: false })).toBe('sin-plan');
    expect(estadoPlanIa({ planIaConfigurado: true, planIaActivo: true, llaveConfigurada: false })).toBe('sin-llave');
    expect(estadoPlanIa({ planIaConfigurado: true, planIaActivo: true, llaveConfigurada: true })).toBe('activo');
  });
});

describe('PaginaPlanIa', () => {
  afterEach(() => reiniciarCliente());

  const GRUPOS: GrupoDto[] = [
    {
      tenant: { id: 't1', slug: 'grupo-andino', nombre: 'Grupo Andino', region: 'co', plan: 'Inicial', zonaHoraria: 'America/Bogota', idioma: 'es-CO', activo: true, creadoEn: '2026-11-09T15:00:00Z' },
      empresas: [
        {
          id: 'e1',
          nombre: 'Andina de Obras S.A.S.',
          identificacion: { pais: 'CO', tipo: 'NIT', numero: '800197268', digitoVerificacion: '4' },
          prefijoFolio: 'ANDINA',
          activa: true,
          planIaConfigurado: true,
          planIaActivo: true,
          llaveConfigurada: true,
          ultimosCuatro: 'WXYZ',
        },
      ],
    },
  ];

  function montar() {
    const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
    configurarCliente({
      fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
        const metodo = init?.method ?? 'GET';
        const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
        if (metodo !== 'GET') peticiones.push({ url, metodo, cuerpo });
        const json = (datos: unknown) => Promise.resolve(new Response(JSON.stringify(datos), { status: 200, headers: { 'Content-Type': 'application/json' } }));
        if (url.endsWith('/probar-llave')) return json({ resultado: 'valida', mensaje: 'ok', ultimosCuatro: 'WXYZ', probadaEn: '2026-11-09T15:00:00Z' });
        if (url.includes('/plataforma/grupos/t1')) return json(GRUPOS[0]);
        if (metodo === 'PUT') return json({ ...PLAN, ultimosCuatro: 'NUEV', actualizadoEn: '2026-11-09T16:00:00Z' });
        return json(PLAN);
      }),
    });
    render(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
          <MemoryRouter initialEntries={['/plataforma/grupos/t1/empresas/e1/plan-ia']}>
            <Routes>
              <Route path="/plataforma/grupos/:tenantId/empresas/:empresaId/plan-ia" element={<PaginaPlanIa />} />
            </Routes>
          </MemoryRouter>
        </QueryClientProvider>
      </ThemeProvider>,
    );
    return peticiones;
  }

  it('muestra la llave solo enmascarada, la prueba a pedido y la reemplaza al guardar', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar();

    expect(await screen.findByText('Configurada · ••••WXYZ')).toBeInTheDocument();
    expect(screen.getByText('Andina de Obras S.A.S. · Grupo Andino')).toBeInTheDocument();
    expect(screen.queryByLabelText('Nueva API key de OpenAI')).not.toBeInTheDocument();
    expect(peticiones).toEqual([]); // nada se prueba solo

    await usuario.click(screen.getByRole('button', { name: 'Probar llave' }));
    expect(await screen.findByText('OpenAI aceptó la llave ••••WXYZ.')).toBeInTheDocument();
    expect(peticiones[0]).toMatchObject({ metodo: 'POST', cuerpo: { apiKeyOpenAi: null } });

    await usuario.click(screen.getByRole('button', { name: 'Reemplazar' }));
    const campo = screen.getByLabelText('Nueva API key de OpenAI');
    expect(campo).toHaveAttribute('type', 'password');
    expect(campo).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Guardar plan' })).toBeDisabled();
    await usuario.type(campo, 'sk-proj-nueva-0123456789NUEV');
    await usuario.click(screen.getByRole('button', { name: 'Guardar plan' }));

    await waitFor(() => expect(peticiones.find((p) => p.metodo === 'PUT')).toBeDefined());
    expect(peticiones.find((p) => p.metodo === 'PUT')?.cuerpo).toMatchObject({ apiKeyOpenAi: 'sk-proj-nueva-0123456789NUEV', validacionesIncluidasMes: 200, topeAdicionalMes: 50 });
    expect(await screen.findByText('Configurada · ••••NUEV')).toBeInTheDocument();
    expect(screen.queryByLabelText('Nueva API key de OpenAI')).not.toBeInTheDocument();
  });

  it('quitar la llave envía una cadena vacía', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar();
    await usuario.click(await screen.findByRole('button', { name: 'Quitar' }));
    expect(screen.getByText('La llave se quitará al guardar.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Guardar plan' }));
    await waitFor(() => expect(peticiones.find((p) => p.metodo === 'PUT')?.cuerpo).toMatchObject({ apiKeyOpenAi: '' }));
  });
});
