import { SCOPES, type EtapaExpedienteDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import { area, areaDeRequisito, expediente, requisito, veredicto } from './datosPrueba';
import { eventoExpedienteATimeline } from './historial';
import { accionesDeExpediente, dialogoAplica, enFaseInicial, puedeReabrirEtapa, type ContextoRevisor } from './reglas';
import { veredictosRevertidos, veredictosVigentesDeRequisito } from './requisitos';

const ADMIN = new Set<string>([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.expedientesAdministrar]);
const REVISOR = new Set<string>([SCOPES.expedientesLeer, SCOPES.expedientesRevisar]);

const etapa = (orden: number, estado: EtapaExpedienteDto['estado'], areas = [area()]): EtapaExpedienteDto => ({
  orden,
  estado,
  activadaEn: null,
  aprobadaEn: null,
  areas,
});

/** Dos etapas: la 1 aprobada (con el veredicto de Compras) y la 2 en curso. */
function enSegundaEtapa(estado: 'EnRevision' | 'Observado' | 'Aprobado' = 'EnRevision') {
  const base = expediente(estado);
  return {
    ...base,
    etapaActual: 2,
    etapas: [
      etapa(1, 'Aprobada', [area({ areaId: 'a-compras', nombre: 'Compras', estado: 'Aprobada', veredictos: [veredicto()] })]),
      etapa(2, estado === 'Aprobado' ? 'Aprobada' : 'Activa', [area({ areaId: 'a-cumplimiento', nombre: 'Cumplimiento' })]),
    ],
  };
}

const contexto =
  (scopes: ReadonlySet<string>, vinculoBloqueado = false) =>
  (): ContextoRevisor => ({ scopes, usuarioId: 'u-1', esMiembro: true, vinculoBloqueado });

describe('desaprobar: qué acciones se ofrecen (DS-1, DS-2)', () => {
  it('un expediente aprobado se desaprueba o vuelve a la fase inicial; solo el administrador', () => {
    expect(accionesDeExpediente(expediente('Aprobado'), ADMIN)).toEqual(['faseInicial', 'desaprobar']);
    expect(accionesDeExpediente(expediente('Aprobado'), REVISOR)).toEqual([]);
    // Con el vínculo bloqueado la API responde 409 vinculo-bloqueado: no se ofrece.
    expect(accionesDeExpediente(expediente('Aprobado'), ADMIN, true)).toEqual(['faseInicial']);
  });

  it('volver a la fase inicial solo se ofrece si hay algo que repetir', () => {
    // Recién abierto: primera etapa en curso y nadie decidió.
    expect(accionesDeExpediente(expediente('EnRevision'), ADMIN)).toEqual(['anular']);
    // Con una etapa ya aprobada sí.
    expect(accionesDeExpediente(enSegundaEtapa(), ADMIN)).toEqual(['faseInicial', 'anular']);
    // Devuelto con una observación vigente: también.
    const observado = expediente('Observado', [area({ estado: 'Observada', veredictos: [veredicto({ veredicto: 'Observado' })] })]);
    expect(accionesDeExpediente(observado, ADMIN)).toEqual(['reenviar', 'faseInicial', 'anular']);
    // Un expediente cerrado sin aprobar no se devuelve.
    expect(accionesDeExpediente(expediente('Rechazado'), ADMIN)).toEqual([]);
    expect(accionesDeExpediente(expediente('Anulado'), ADMIN)).toEqual([]);
  });

  it('el veredicto del sistema no cuenta como haber decidido', () => {
    const automatica = expediente('EnRevision', [area({ automatica: true, veredictos: [veredicto({ origen: 'Sistema' })] })]);
    expect(enFaseInicial(automatica)).toBe(true);
    expect(enFaseInicial(expediente('EnRevision', [area({ veredictos: [veredicto()] })]))).toBe(false);
    // Un veredicto revertido ya no cuenta: tras volver a la fase inicial vuelve a estar en el comienzo.
    expect(enFaseInicial(expediente('EnRevision', [area({ veredictos: [veredicto({ vigente: false, revertido: true })] })]))).toBe(true);
  });

  it('con requisitos, la decisión de un documento también cuenta', () => {
    const conDecision = { ...expediente('EnRevision'), requisitos: [requisito({ areas: [areaDeRequisito({ veredictos: [veredicto()] })] })] };
    expect(enFaseInicial(conDecision)).toBe(false);
    const sinDecision = { ...expediente('EnRevision'), requisitos: [requisito()] };
    expect(enFaseInicial(sinDecision)).toBe(true);
  });

  it('reabrir una etapa: solo una ya aprobada de un expediente en revisión o devuelto, con el vínculo sin bloquear', () => {
    const exp = enSegundaEtapa();
    expect(puedeReabrirEtapa(exp, exp.etapas[0]!, ADMIN)).toBe(true);
    expect(puedeReabrirEtapa(exp, exp.etapas[1]!, ADMIN)).toBe(false); // la activa se corrige anulando sus veredictos
    expect(puedeReabrirEtapa(exp, exp.etapas[0]!, REVISOR)).toBe(false);
    expect(puedeReabrirEtapa(exp, exp.etapas[0]!, ADMIN, true)).toBe(false);
    expect(puedeReabrirEtapa(enSegundaEtapa('Observado'), exp.etapas[0]!, ADMIN)).toBe(true);
    // Aprobado: se desaprueba (otra acción), no se reabre una etapa suelta.
    expect(puedeReabrirEtapa(enSegundaEtapa('Aprobado'), exp.etapas[0]!, ADMIN)).toBe(false);
  });

  it('el diálogo deja de aplicar si el expediente cambió', () => {
    const exp = enSegundaEtapa();
    const dialogo = { tipo: 'reabrirEtapa', etapa: exp.etapas[0]! } as const;
    expect(dialogoAplica(dialogo, exp, contexto(ADMIN))).toBe(true);
    // Otra persona ya la reabrió: la etapa 1 está activa y ya no aplica.
    const reabierta = { ...exp, etapas: [etapa(1, 'Activa'), etapa(2, 'Esperando')] };
    expect(dialogoAplica(dialogo, reabierta, contexto(ADMIN))).toBe(false);
    // El expediente ya no está aprobado: desaprobar ya no aplica.
    expect(dialogoAplica({ tipo: 'desaprobar' }, exp, contexto(ADMIN))).toBe(false);
    expect(dialogoAplica({ tipo: 'desaprobar' }, expediente('Aprobado'), contexto(ADMIN))).toBe(true);
  });
});

describe('desaprobar: veredictos revertidos y línea de tiempo', () => {
  it('lo revertido no cuenta como vigente pero se puede listar aparte', () => {
    const revertido = veredicto({ id: 'v-rev', vigente: false, revertido: true });
    const vigente = veredicto({ id: 'v-vig' });
    const area = areaDeRequisito({ veredictos: [revertido, vigente] });

    expect(veredictosVigentesDeRequisito(area).map((v) => v.id)).toEqual(['v-vig']);
    expect(veredictosRevertidos(area).map((v) => v.id)).toEqual(['v-rev']);
  });

  const t = i18n.t.bind(i18n);
  const contextoHistorial = { usuarioActual: null, areas: new Map<string, string>() };
  const evento = (tipo: string, datos: Record<string, unknown>) => ({
    version: 12,
    tipo,
    fecha: '2026-11-10T15:00:00Z',
    actorTipo: 'Usuario',
    actorId: 'u-admin',
    sistema: null,
    datos: { fecha: '2026-11-10T15:00:00Z', ...datos },
  });

  it('«etapa_reabierta» dice desde qué etapa se revisa de nuevo y por qué', () => {
    const entrada = eventoExpedienteATimeline(evento('etapa_reabierta', { orden: 2, faseInicial: false, justificacion: 'Faltó la garantía.' }), contextoHistorial, t);

    expect(entrada.titulo).toBe('Etapa 2 reabierta: se revisa de nuevo');
    expect(entrada.motivo).toBe('Faltó la garantía.');
  });

  it('volver a la fase inicial y desaprobar el expediente se leen distinto', () => {
    const inicial = eventoExpedienteATimeline(evento('etapa_reabierta', { orden: 1, faseInicial: true, justificacion: 'Empezar de cero.' }), contextoHistorial, t);
    expect(inicial.titulo).toBe('Vuelve a la fase inicial: se revisa todo de nuevo');

    const desaprobado = eventoExpedienteATimeline(evento('expediente_desaprobado', { orden: 3, faseInicial: false, justificacion: 'Inhabilidad.' }), contextoHistorial, t);
    expect(desaprobado.titulo).toBe('Etapa 3 reabierta: se revisa de nuevo');
    expect(desaprobado.antes).toBe('Aprobado');
    expect(desaprobado.despues).toBe('EnRevision');
    expect(desaprobado.motivo).toBe('Inhabilidad.');
  });
});
