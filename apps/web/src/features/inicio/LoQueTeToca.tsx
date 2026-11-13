import { Box, Link, Skeleton, Stack, Typography } from '@mui/material';
import type { MisPendientesDto } from '@validasoft/api-client';
import { EstadoVacio } from '@validasoft/ui';
import { CalendarClock, CalendarX, CircleCheck, FolderClock, Landmark, RefreshCw, Workflow, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useMisPendientes } from './api';
import { accesosPendientes, estaAlDia, textoArea, type ClaveAcceso } from './modelo';

const ICONOS: Readonly<Record<ClaveAcceso, LucideIcon>> = {
  revalidaciones: RefreshCw,
  cuentas: Landmark,
  aperturas: FolderClock,
  porVencer: CalendarClock,
  vencidos: CalendarX,
};

/** Fila de una lista de pendientes: ícono pequeño y el texto (enlace o no). */
function Fila({ icono: Icono, children }: { icono: LucideIcon; children: ReactNode }) {
  return (
    <Box component="li" sx={{ display: 'flex', alignItems: 'flex-start', gap: 1, minWidth: 0 }}>
      <Box component="span" aria-hidden sx={{ display: 'inline-flex', pt: '2px', color: 'text.secondary', flex: 'none' }}>
        <Icono size={16} strokeWidth={1.75} />
      </Box>
      <Box sx={{ minWidth: 0 }}>{children}</Box>
    </Box>
  );
}

function Lista({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="overline" component="h3" sx={{ color: 'text.secondary', display: 'block', mb: 0.75 }}>
        {titulo}
      </Typography>
      <Stack component="ul" spacing={1} sx={{ m: 0, p: 0, listStyle: 'none' }}>
        {children}
      </Stack>
    </Box>
  );
}

function Pendientes({ datos, empresaId }: { datos: MisPendientesDto; empresaId: string }) {
  const { t } = useTranslation();
  const accesos = accesosPendientes(datos);
  const dosColumnas = datos.porArea.length > 0 && accesos.length > 0;
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: dosColumnas ? 'repeat(2, minmax(0, 1fr))' : 'minmax(0, 1fr)' }, gap: 2 }}>
      {datos.porArea.length > 0 && (
        <Lista titulo={t('inicio.pendientes.porArea')}>
          {datos.porArea.map((a) => (
            <Fila key={a.areaId} icono={Workflow}>
              {a.cantidad > 0 ? (
                <Link component={RouterLink} to={`/e/${empresaId}/bandeja?area=${encodeURIComponent(a.areaId)}`}>
                  {textoArea(a, t)}
                </Link>
              ) : (
                <Typography sx={{ color: 'text.secondary' }}>{textoArea(a, t)}</Typography>
              )}
            </Fila>
          ))}
        </Lista>
      )}
      {accesos.length > 0 && (
        <Lista titulo={t('inicio.pendientes.otros')}>
          {accesos.map((a) => {
            const texto = t(`inicio.pendientes.${a.clave}`, { count: a.cantidad });
            return (
              <Fila key={a.clave} icono={ICONOS[a.clave]}>
                {a.ruta ? (
                  <Link component={RouterLink} to={`/e/${empresaId}/${a.ruta}`}>
                    {texto}
                  </Link>
                ) : (
                  <Typography>{texto}</Typography>
                )}
                {a.bloqueantes !== undefined && a.bloqueantes > 0 && (
                  <Typography variant="body2" sx={{ color: 'error.main' }}>
                    {t('inicio.pendientes.bloqueantes', { count: a.bloqueantes })}
                  </Typography>
                )}
                {a.clave === 'cuentas' && (
                  <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                    {t('inicio.pendientes.cuentasAyuda')}
                  </Typography>
                )}
              </Fila>
            );
          })}
        </Lista>
      )}
    </Box>
  );
}

/** «Lo que te toca» (INI-1): expedientes por revisar en tus áreas y los demás pendientes, cada uno con su pantalla. */
export function LoQueTeToca({ empresaId }: { empresaId: string }) {
  const { t } = useTranslation();
  const consulta = useMisPendientes(empresaId);
  const datos = consulta.data;
  const titulo = t('inicio.pendientes.titulo');

  let contenido: ReactNode;
  if (consulta.isError && !datos) {
    contenido = <ErrorRecuperable mensaje={`${t('inicio.pendientes.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />;
  } else if (!datos) {
    contenido = (
      <Stack spacing={1} aria-busy>
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} variant="rounded" height={18} width={`${String(70 - i * 15)}%`} />
        ))}
      </Stack>
    );
  } else if (estaAlDia(datos)) {
    contenido = <EstadoVacio compacto icono={CircleCheck} titulo={t('inicio.pendientes.vacio')} texto={t('inicio.pendientes.vacioAyuda')} />;
  } else {
    contenido = <Pendientes datos={datos} empresaId={empresaId} />;
  }

  return (
    <Tarjeta titulo={titulo} etiqueta={titulo}>
      {contenido}
    </Tarjeta>
  );
}
