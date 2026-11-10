import { ROLES, type PersonaRelacionada, type RolPersona } from '../tipos';

export const esRol = (valor: string): valor is RolPersona => ROLES.some((r) => r === valor);

/** «Ana María Gómez» o la razón social de una persona jurídica (NIT: solo `nombres`). */
export const nombrePersona = (p: Pick<PersonaRelacionada, 'nombres' | 'apellidos'>) => [p.nombres, p.apellidos].filter(Boolean).join(' ');
