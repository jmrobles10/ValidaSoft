import type { FrameLocator, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { asercion, CLAVE_PLATAFORMA, clavesDeSistema, llamar, type PersonaDelSistema } from './api';
import { abrirEmbebidoCon } from './embebido';
import { ORIGEN_SISTEMA } from './semilla';

/**
 * Consola de Plataforma (PT-4) como la abriría el backoffice del proveedor: registra un sistema de Plataforma propio de la
 * prueba (clave pública y el origen de la página anfitriona), y cada vez que la consola pide sesión firma la aserción del
 * operador y la canjea con la credencial de Plataforma en `POST /api/v1/plataforma/sesiones`.
 */
export async function abrirConsolaDePlataforma(page: Page, operador?: PersonaDelSistema): Promise<FrameLocator> {
  const codigo = `E2E_${randomUUID().replace(/-/g, '').slice(0, 8).toUpperCase()}`;
  const claves = clavesDeSistema();
  await llamar('PUT', `/api/v1/plataforma/sistemas/${codigo}`, {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { nombre: 'Backoffice E2E', clavePublicaPem: claves.publica, origenesPermitidos: [ORIGEN_SISTEMA] },
  });
  const persona = operador ?? { sub: 'paula', nombre: 'Paula Plataforma', correo: 'paula@validasoft.e2e' };
  const sesion = async () =>
    (await llamar<{ token: string }>('POST', '/api/v1/plataforma/sesiones', { llave: CLAVE_PLATAFORMA, delegado: asercion(codigo, claves.privada, persona) })).token;
  return abrirEmbebidoCon(page, { empresaId: null, rutaInicial: 'plataforma/grupos', sesion });
}
