import { createBrowserRouter, Navigate, type RouteObject } from 'react-router';
import { PaginaEntrar, PaginaNoEncontrada, RequiereSesion } from '../sesion/PaginasSesion';
import { Shell } from './Shell';

/**
 * Rutas del tercero (docs/06 §3.2) con historial del navegador: el correo trae `{UrlPublica}/e/{token}`. Las páginas
 * se cargan por demanda para que la entrada por el enlace sea liviana en el celular.
 */
const rutas: RouteObject[] = [
  {
    element: <Shell />,
    children: [
      { index: true, element: <Navigate to="/empresas" replace /> },
      { path: 'entrar', element: <PaginaEntrar /> },
      // Entrada de un sistema integrador (IO-5): bóveda, expediente o ficha dentro de su iframe, con la sesión que él entrega.
      { path: 'embed/:componente', lazy: () => import('../embebido/PaginaEmbebida').then((m) => ({ Component: m.PaginaEmbebida })) },
      { path: 'e/:token', lazy: () => import('../enlace/PaginaEnlace').then((m) => ({ Component: m.PaginaEnlace })) },
      // Anónima: a donde lleva el QR del certificado (`{UrlPublica}/verificar/{folio}?huella=…`).
      { path: 'verificar/:folio', lazy: () => import('../verificacion/PaginaVerificacion').then((m) => ({ Component: m.PaginaVerificacion })) },
      {
        element: <RequiereSesion />,
        children: [
          { path: 'empresas', lazy: () => import('../vinculos/PaginaEmpresas').then((m) => ({ Component: m.PaginaEmpresas })) },
          { path: 'empresas/:vinculoId', lazy: () => import('../invitacion/PaginaInvitacion').then((m) => ({ Component: m.PaginaInvitacion })) },
          { path: 'boveda', lazy: () => import('../boveda/PaginaBoveda').then((m) => ({ Component: m.PaginaBoveda })) },
          { path: 'boveda/subir', lazy: () => import('../boveda/PaginaSubir').then((m) => ({ Component: m.PaginaSubir })) },
          { path: 'ficha', lazy: () => import('../ficha/PaginaFicha').then((m) => ({ Component: m.PaginaFicha })) },
          { path: 'ficha/:seccion', lazy: () => import('../ficha/PaginaSeccion').then((m) => ({ Component: m.PaginaSeccion })) },
          { path: 'cuentas', lazy: () => import('../cuentas/PaginaCuentas').then((m) => ({ Component: m.PaginaCuentas })) },
          // Habeas data y exportación firmada de la bóveda (OD-1, OD-9).
          { path: 'privacidad', lazy: () => import('../privacidad/PaginaPrivacidad').then((m) => ({ Component: m.PaginaPrivacidad })) },
          { path: 'expediente/:id', lazy: () => import('../expediente/PaginaExpediente').then((m) => ({ Component: m.PaginaExpediente })) },
        ],
      },
      ...(import.meta.env.DEV
        ? [{ path: 'dev/correos', lazy: () => import('../dev/PaginaCorreos').then((m) => ({ Component: m.PaginaCorreos })) }]
        : []),
      { path: '*', element: <PaginaNoEncontrada /> },
    ],
  },
];

export const crearRouter = () => createBrowserRouter(rutas);
