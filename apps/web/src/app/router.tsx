import { createHashRouter, Navigate } from 'react-router';
import { EmpresaDeLaRuta, RedireccionInicial } from '../features/empresas/RutasEmpresa';
import { AppShell } from './AppShell';
import { InicioEmpresa } from './InicioEmpresa';
import { LayoutConfiguracion } from './LayoutConfiguracion';

/**
 * Hash router: dentro del iframe la navegación no toca la URL del sistema que aloja la pantalla y las rutas no
 * dependen de la configuración del servidor estático. Toda ruta de operación está dentro de una empresa
 * (`/e/:empresaId/…`, docs/06 §3.1); el tenant va implícito en la sesión. Cada página se carga bajo demanda.
 */
export function crearRouter() {
  return createHashRouter([
    { path: '/', element: <RedireccionInicial /> },
    {
      path: '/e/:empresaId',
      element: <EmpresaDeLaRuta />,
      children: [
        {
          element: <AppShell />,
          children: [
            { index: true, element: <InicioEmpresa /> },
            {
              path: 'inicio',
              lazy: () => import('../features/inicio/PaginaInicio').then((m) => ({ Component: m.PaginaInicio })),
            },
            {
              path: 'bandeja',
              lazy: () => import('../features/expedientes/pages/Bandeja').then((m) => ({ Component: m.Bandeja })),
            },
            {
              path: 'revalidaciones',
              lazy: () => import('../features/revalidaciones/PaginaRevalidaciones').then((m) => ({ Component: m.PaginaRevalidaciones })),
            },
            {
              path: 'vencimientos',
              lazy: () => import('../features/vencimientos/PaginaVencimientos').then((m) => ({ Component: m.PaginaVencimientos })),
            },
            {
              path: 'cumplimiento',
              lazy: () => import('../features/cumplimiento/PaginaCumplimiento').then((m) => ({ Component: m.PaginaCumplimiento })),
            },
            {
              path: 'certificados',
              lazy: () => import('../features/certificados/lista/PaginaCertificados').then((m) => ({ Component: m.PaginaCertificados })),
            },
            {
              path: 'expedientes/:expedienteId',
              lazy: () => import('../features/expedientes/pages/DetalleExpediente').then((m) => ({ Component: m.DetalleExpediente })),
            },
            {
              path: 'vinculos',
              lazy: () => import('../features/vinculos/pages/ListaVinculos').then((m) => ({ Component: m.ListaVinculos })),
            },
            {
              path: 'vinculos/:vinculoId',
              lazy: () => import('../features/vinculos/pages/DetalleVinculo').then((m) => ({ Component: m.DetalleVinculo })),
            },
            {
              path: 'terceros/:terceroId',
              lazy: () => import('../features/terceros/PaginaFichaTercero').then((m) => ({ Component: m.PaginaFichaTercero })),
            },
            {
              path: 'auditoria',
              lazy: () => import('../features/auditoria/PaginaAuditoria').then((m) => ({ Component: m.PaginaAuditoria })),
            },
            {
              element: <LayoutConfiguracion />,
              children: [
                {
                  path: 'areas',
                  lazy: () => import('../features/configuracion/areas/PaginaAreas').then((m) => ({ Component: m.PaginaAreas })),
                },
                {
                  path: 'usuarios',
                  lazy: () => import('../features/configuracion/usuarios/PaginaUsuarios').then((m) => ({ Component: m.PaginaUsuarios })),
                },
                {
                  path: 'plantillas',
                  lazy: () => import('../features/configuracion/plantillas/PaginaPlantillas').then((m) => ({ Component: m.PaginaPlantillas })),
                },
                {
                  path: 'uso-ia',
                  lazy: () => import('../features/configuracion/usoIa/PaginaUsoIa').then((m) => ({ Component: m.PaginaUsoIa })),
                },
                {
                  path: 'listas-restrictivas',
                  lazy: () =>
                    import('../features/configuracion/listasRestrictivas/PaginaListasRestrictivas').then((m) => ({ Component: m.PaginaListasRestrictivas })),
                },
                {
                  path: 'motivos',
                  lazy: () => import('../features/configuracion/motivos/PaginaMotivos').then((m) => ({ Component: m.PaginaMotivos })),
                },
                {
                  path: 'flujos',
                  lazy: () => import('../features/flujos/pages/PaginaFlujos').then((m) => ({ Component: m.PaginaFlujos })),
                },
                {
                  path: 'flujos/:flujoId',
                  lazy: () => import('../features/flujos/pages/EditorFlujo').then((m) => ({ Component: m.EditorFlujo })),
                },
                {
                  path: 'webhooks',
                  lazy: () => import('../features/configuracion/webhooks/PaginaWebhooks').then((m) => ({ Component: m.PaginaWebhooks })),
                },
                {
                  path: 'tipos-documento',
                  lazy: () => import('../features/configuracion/tiposDocumento/PaginaTiposDocumento').then((m) => ({ Component: m.PaginaTiposDocumento })),
                },
                {
                  path: 'sistemas',
                  lazy: () => import('../features/configuracion/sistemas/PaginaSistemas').then((m) => ({ Component: m.PaginaSistemas })),
                },
                {
                  path: 'empresas',
                  lazy: () => import('../features/configuracion/empresas/PaginaEmpresas').then((m) => ({ Component: m.PaginaEmpresas })),
                },
                {
                  path: 'api-keys',
                  lazy: () => import('../features/configuracion/apiKeys/PaginaApiKeys').then((m) => ({ Component: m.PaginaApiKeys })),
                },
                {
                  path: 'accesos-soporte',
                  lazy: () => import('../features/configuracion/accesosSoporte/PaginaAccesosSoporte').then((m) => ({ Component: m.PaginaAccesosSoporte })),
                },
                {
                  path: 'datos-marca',
                  lazy: () => import('../features/configuracion/datosMarca/PaginaDatosMarca').then((m) => ({ Component: m.PaginaDatosMarca })),
                },
                {
                  path: 'tipos-vinculo',
                  lazy: () => import('../features/configuracion/tiposVinculo/PaginaTiposVinculo').then((m) => ({ Component: m.PaginaTiposVinculo })),
                },
              ],
            },
          ],
        },
      ],
    },
    { path: '*', element: <Navigate to="/" replace /> },
  ]);
}
