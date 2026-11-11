import type { FrameLocator, Page } from '@playwright/test';
import { llamar } from './api';
import { abrirEmbebidoCon, sesionDeUsuario } from './embebido';
import type { Empresa, Escenario, Persona } from './semilla';

interface ApiKey {
  id: string;
  nombre: string;
  scopes: string[];
  sistemaId: string | null;
}

/**
 * Una API key del mismo sistema integrado que la del escenario pero con el permiso de responder el habeas data
 * (`supresiones:resolver`): lo que el sistema de la empresa le daría a quien atiende esas solicitudes. La llave del escenario no lo
 * lleva (se creó antes de esa función) y una persona delegada nunca tiene más que su llave.
 */
export async function llaveConHabeas(e: Escenario): Promise<string> {
  const llaves = await llamar<ApiKey[]>('GET', '/api/v1/api-keys', { llave: e.admin });
  const base = llaves.find((k) => k.nombre === 'E2E embebido');
  if (!base) throw new Error('No encontré la API key del escenario.');
  const nueva = await llamar<{ valor: string }>('POST', '/api/v1/api-keys', {
    llave: e.admin,
    cuerpo: { nombre: 'E2E habeas data', scopes: [...base.scopes, 'supresiones:resolver'], empresas: null, expiraEn: null, sandbox: false, sistemaId: base.sistemaId },
  });
  return nueva.valor;
}

/** Abre la web embebida como `persona` en `empresa` con una API key concreta (por omisión, la que trae el permiso del habeas data). */
export async function abrirEmbebidoConLlave(page: Page, e: Escenario, llave: string, persona: Persona, empresa: Empresa, rutaInicial: string): Promise<FrameLocator> {
  return abrirEmbebidoCon(page, { empresaId: empresa.id, rutaInicial, sesion: () => sesionDeUsuario(llave, e.delegado(persona)) });
}
