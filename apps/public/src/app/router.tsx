import { createBrowserRouter, Navigate, type RouteObject } from 'react-router';
import { PaginaNoEncontrada, RequiereSesion } from '../sesion/PaginasSesion';
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
      { path: 'e/:token', lazy: () => import('../enlace/PaginaEnlace').then((m) => ({ Component: m.PaginaEnlace })) },
      {
        element: <RequiereSesion />,
        children: [
          { path: 'empresas', lazy: () => import('../vinculos/PaginaEmpresas').then((m) => ({ Component: m.PaginaEmpresas })) },
          { path: 'empresas/:vinculoId', lazy: () => import('../invitacion/PaginaInvitacion').then((m) => ({ Component: m.PaginaInvitacion })) },
          { path: 'boveda', lazy: () => import('../boveda/PaginaBoveda').then((m) => ({ Component: m.PaginaBoveda })) },
          { path: 'boveda/subir', lazy: () => import('../boveda/PaginaSubir').then((m) => ({ Component: m.PaginaSubir })) },
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
