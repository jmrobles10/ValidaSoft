import { llamar } from '../../soporte/api';
import { pdf } from '../../soporte/archivos';
import { esperarCorreo, literal } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { llenarGeneralJuridica } from '../../soporte/ficha';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RAZON_SOCIAL = 'Aceros de la Campaña S.A.S.';

recorrido(
  {
    dominio: 'operacion',
    actor: 'Administrador de empresa',
    titulo: 'Abre la renovación de varios clientes a la vez',
    resumen:
      'La administradora crea una campaña de renovación para los vínculos que vencen pronto: filtra, revisa a quién afecta sin enviar nada, confirma y la campaña abre la renovación de cada tercero en segundo plano.',
    precondiciones: [
      'Un grupo con Constructora Sur, su flujo de Cliente que pide el RUT y la ficha general, con vigencia de 90 días, y un cliente ya aprobado cuyo vínculo vence en unos meses.',
    ],
    relacionados: ['operacion/recordatorio-automatico-por-flujo'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_CAMPANIA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }], {
      dias: 90,
      diasAviso: [30, 5],
      diasGracia: 3,
    });
    const invitacion = await invitar(e, e.sur, nuevoTercero(RAZON_SOCIAL), 'CLIENTE');
    const cliente = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(cliente, invitacion);
    await aceptarInvitacion(cliente, invitacion);
    await cliente.getByRole('link', { name: /^Completar/ }).first().click();
    await llenarGeneralJuridica(cliente, RAZON_SOCIAL);
    await cliente.goto('/empresas');
    await abrirEmpresa(cliente, e.sur.nombre, 'CLIENTE');
    await subirDocumento(cliente, 'Registro Único Tributario', pdf('RUT campaña v1'));
    await esperarEstadoDelTercero(cliente, 'EnRevision');
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión', 'Cliente');
    await aprobarArea(sofia, 'Revisión');
    await esperarEstadoDelTercero(cliente, 'Aprobado');

    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'vencimientos');
    await ana.getByRole('link', { name: 'Campañas de renovación' }).or(ana.getByRole('button', { name: 'Campañas de renovación' })).first().click();
    await expect(ana.getByText('Aún no hay campañas de renovación.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Abre las campañas de renovación',
      explicacion:
        'Desde Vencimientos, «Campañas de renovación» lista las campañas que la empresa ha creado para renovar la validación de sus terceros en lote. Una campaña abre la renovación de muchos terceros a la vez, en lugar de hacerlo uno por uno.',
      resaltar: ana.getByRole('button', { name: 'Nueva campaña' }),
      configuracion: { ajuste: 'Campañas de renovación', ruta: 'Vencimientos › Campañas de renovación', efecto: 'Solo afecta a los vínculos que cumplen el filtro que elijas al crear la campaña.' },
    });

    await ana.getByRole('button', { name: 'Nueva campaña' }).click();
    await expect(ana.getByRole('heading', { level: 1, name: 'Nueva campaña de renovación' })).toBeVisible();
    await ana.getByRole('textbox', { name: 'Vencen en (días)' }).fill('120');
    await ana.getByRole('button', { name: 'Ver a quiénes afecta' }).click();
    await expect(ana.getByText('Se les abre la renovación')).toBeVisible();
    await expect(ana.getByRole('table').getByText(invitacion.nombre)).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Filtra los vínculos y revisa a quién afecta',
      explicacion:
        'Eliges a quiénes renovar con «Vencen en (días)», el tipo de vínculo y el estado. Al pulsar «Ver a quiénes afecta» se calcula la vista previa sin enviar nada: los totales dicen cuántos se abren, cuántos se recuerdan, cuántos se omiten y a cuántos llega correo. ' +
        'La tabla muestra qué pasará con cada tercero. Incluye también lo que ya venció.',
      resaltar: [ana.getByRole('textbox', { name: 'Vencen en (días)' }), ana.getByRole('table')],
      variantes: [
        { cuando: 'Ningún vínculo cumple el filtro', entonces: 'Aparece «Ningún vínculo con este filtro necesita que se abra o se recuerde su renovación» y no se puede abrir la campaña.' },
        { cuando: 'El vínculo tiene la renovación ya abierta', entonces: 'En vez de abrirse, se recuerda la renovación que ya tiene abierta.' },
      ],
    });

    await ana.getByLabel('Nombre de la campaña').fill('Renovación de clientes E2E');
    await paso(paginaAna, {
      titulo: 'Nombra la campaña y pide confirmación',
      explicacion:
        'Escribe un nombre para reconocer la campaña luego; si lo dejas vacío se usa «Renovación del» y la fecha. «Abrir renovaciones» no envía nada todavía: primero muestra un aviso con lo que va a pasar.',
      resaltar: [ana.getByLabel('Nombre de la campaña'), ana.getByRole('button', { name: 'Abrir renovaciones' })],
    });

    await ana.getByRole('button', { name: 'Abrir renovaciones' }).click();
    const aviso = ana.getByRole('dialog', { name: '¿Abrir las renovaciones?' });
    await expect(aviso.getByText('Se abrirá la renovación a 1 tercero.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Confirma lo que va a pasar',
      explicacion:
        'El aviso dice cuántos terceros recibirán la renovación, a cuántos llega un correo y que todos reciben un aviso en su campana. Se procesa en segundo plano, por tandas: puedes salir de la pantalla y seguir el avance en la campaña. ' +
        'Puedes pausarla o cancelarla cuando quieras; lo que ya se abrió sigue su curso.',
      resaltar: aviso,
      avisos: true,
      completa: false,
    });
    await aviso.getByRole('button', { name: 'Confirmar y abrir' }).click();

    await expect(ana.getByRole('heading', { level: 1, name: 'Renovación de clientes E2E' })).toBeVisible();
    await expect(ana.getByText('Completada', { exact: true })).toBeVisible({ timeout: 40_000 });
    await expect(ana.getByRole('table', { name: 'Vínculos de la campaña' }).getByText(/^Renovación abierta · /)).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Sigue el avance de la campaña',
      explicacion:
        'El detalle muestra la campaña con su estado (Procesando, Pausada, Completada o Con errores), el avance y una fila por cada vínculo con su acción: «Renovación abierta» o «Renovación recordada». ' +
        'Desde aquí puedes pausar, reanudar o cancelar la campaña mientras se procesa, y descargar el informe.',
      resaltar: [ana.getByRole('heading', { level: 1, name: 'Renovación de clientes E2E' }), ana.getByRole('table', { name: 'Vínculos de la campaña' })],
    });

    await esperarCorreo(invitacion.correo, new RegExp(`^${literal(e.sur.nombre)} abrió la renovación de tu validación$`));
    const vinculo = await llamar<{ estado: string }>('GET', `/api/v1/vinculos/${invitacion.vinculoId}`, { llave: e.admin, empresa: e.sur.id });
    expect(vinculo.estado).toBe('Activo');
  },
);
