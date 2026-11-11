import { vi } from 'vitest';

/**
 * Un navegador con passkeys para las pruebas: `PublicKeyCredential` y `navigator.credentials` falsos. `get` devuelve una firma de verdad (con binarios) para que
 * el código que la serializa corra completo; los tests ven lo que se le pidió (`mediation`, `signal`) en `llamadas`.
 */
export interface NavegadorSimulado {
  llamadas: { mediation: string | undefined; conSenal: boolean }[];
  /** Las señales de aborto con que se pidió cada firma, para comprobar que se cancelan al salir de la pantalla. */
  senales: AbortSignal[];
  get: ReturnType<typeof vi.fn>;
  restaurar: () => void;
}

class CredencialFalsa {
  id = 'cred-1';
  rawId = new Uint8Array([1, 2, 3]).buffer;
  type = 'public-key';
  response = {
    authenticatorData: new Uint8Array([4, 5]).buffer,
    clientDataJSON: new Uint8Array([6, 7]).buffer,
    signature: new Uint8Array([8, 9]).buffer,
    userHandle: null,
  };
}

export const OPCIONES_DE_FIRMA = { challenge: 'AQID', rpId: 'localhost', userVerification: 'required', allowCredentials: [] };

export function simularNavegadorConPasskeys(opciones: { condicional?: boolean; esperaLaFirma?: boolean } = {}): NavegadorSimulado {
  const llamadas: NavegadorSimulado['llamadas'] = [];
  const senales: AbortSignal[] = [];
  const anterior = Object.getOwnPropertyDescriptor(window, 'PublicKeyCredential');
  const credenciales = Object.getOwnPropertyDescriptor(navigator, 'credentials');

  const clase = CredencialFalsa as unknown as { isConditionalMediationAvailable?: () => Promise<boolean> };
  if (opciones.condicional !== undefined) clase.isConditionalMediationAvailable = () => Promise.resolve(opciones.condicional === true);
  else delete clase.isConditionalMediationAvailable;

  // Las peticiones condicionales abiertas: una petición modal nueva las aborta, como hace el navegador.
  const abiertas: (() => void)[] = [];
  const get = vi.fn((peticion: { mediation?: string; signal?: AbortSignal }) => {
    llamadas.push({ mediation: peticion.mediation, conSenal: peticion.signal !== undefined });
    if (peticion.signal) senales.push(peticion.signal);
    const condicional = peticion.mediation === 'conditional';
    if (!condicional) for (const abortar of abiertas.splice(0)) abortar();
    // Como el navegador: la petición condicional queda abierta hasta que la persona elige o se aborta.
    if (opciones.esperaLaFirma && condicional) {
      return new Promise((_resolver, rechazar) => {
        const abortar = () => rechazar(new DOMException('abortada', 'AbortError'));
        abiertas.push(abortar);
        peticion.signal?.addEventListener('abort', abortar);
      });
    }
    return Promise.resolve(new CredencialFalsa());
  });
  Object.defineProperty(window, 'PublicKeyCredential', { value: CredencialFalsa, configurable: true, writable: true });
  Object.defineProperty(navigator, 'credentials', { value: { create: vi.fn(), get }, configurable: true });

  return {
    llamadas,
    senales,
    get,
    restaurar: () => {
      if (anterior) Object.defineProperty(window, 'PublicKeyCredential', anterior);
      else delete (window as { PublicKeyCredential?: unknown }).PublicKeyCredential;
      if (credenciales) Object.defineProperty(navigator, 'credentials', credenciales);
      else delete (navigator as { credentials?: unknown }).credentials;
    },
  };
}
