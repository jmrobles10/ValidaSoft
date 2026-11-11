import { describe, expect, it } from 'vitest';
import { idsResumibles } from './api';

const ANA = '0194f6c2-4b1a-7c3e-8d2f-1a2b3c4d5e6f';
const LUIS = '0194f6c2-4b1a-7c3e-8d2f-1a2b3c4d5e70';

describe('idsResumibles', () => {
  it('deja solo identificadores de usuario, sin repetir y ordenados', () => {
    expect(idsResumibles([LUIS, ANA, LUIS, ANA.toUpperCase()])).toEqual([ANA, ANA.toUpperCase(), LUIS].sort());
  });

  it('descarta al sistema: un id que no es de usuario haría fallar el lote entero (422 ids-invalidos)', () => {
    expect(idsResumibles(['revision-automatica', ANA, 'sistema', '', 'no-es-un-id'])).toEqual([ANA]);
  });

  it('no pasa de 100 ids por llamada', () => {
    const muchos = Array.from({ length: 150 }, (_, i) => `0194f6c2-4b1a-7c3e-8d2f-${String(i).padStart(12, '0')}`);
    expect(idsResumibles(muchos)).toHaveLength(100);
  });
});
