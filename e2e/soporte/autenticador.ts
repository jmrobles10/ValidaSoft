import type { CDPSession, Page } from '@playwright/test';

export interface AutenticadorVirtual {
  cdp: CDPSession;
  id: string;
}

/**
 * Autenticador virtual con huella simulada (CDP `WebAuthn`): credenciales detectables y verificación de usuario.
 *
 * Con una credencial guardada, el autenticador virtual resuelve solo la petición «condicional» que la pantalla de ingreso deja abierta (PI-14: las
 * passkeys dentro del campo del correo) y entra sin que nadie la elija, justo cuando la prueba sigue por otro camino (el código del correo o el botón
 * «Entrar con una passkey»): según cuánto tarde el navegador, la pantalla desaparece debajo de la prueba. Estas pruebas no son las de esa interfaz (de
 * ella se ocupan las de Vitest con un navegador simulado), así que se la apaga en las páginas que usan el autenticador: la pantalla ofrece solo el
 * código y el botón y el orden de los pasos no depende de la carrera.
 *
 * `sesionCdp`: sesión CDP ya abierta de la página (para cambiar de autenticador sin volver a apagar la interfaz condicional).
 */
export async function autenticadorVirtual(page: Page, sesionCdp?: CDPSession): Promise<AutenticadorVirtual> {
  const cdp = sesionCdp ?? (await page.context().newCDPSession(page));
  if (!sesionCdp) {
    await cdp.send('WebAuthn.enable');
    await page.addInitScript(() => {
      const credencial = (globalThis as { PublicKeyCredential?: object }).PublicKeyCredential;
      if (credencial) Object.defineProperty(credencial, 'isConditionalMediationAvailable', { value: () => Promise.resolve(false), configurable: true });
    });
  }
  const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', {
    options: { protocol: 'ctap2', transport: 'internal', hasResidentKey: true, hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true },
  });
  return { cdp, id: authenticatorId };
}
