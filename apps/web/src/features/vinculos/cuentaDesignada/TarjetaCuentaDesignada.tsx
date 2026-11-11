import { Divider, Stack, Typography } from '@mui/material';
import { SCOPES, type CuentaDesignadaDelVinculoDto, type CuentaDesignadaDto, type VinculoDto } from '@validasoft/api-client';
import { EsqueletoTarjeta, EstadoVacio, formatearFechaHora } from '@validasoft/ui';
import { Landmark } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSesion } from '../../../sesion/sesionStore';
import { DialogoConfirmacion, ErrorRecuperable, Tarjeta } from '../../../shared/componentes';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useAprobarCuenta, useCuentaDesignada, useRechazarCuenta } from './api';
import { CuentaRechazada, DatosCuenta, SeccionCuenta } from './DatosCuenta';
import { DialogoRechazarCuenta } from './DialogoRechazarCuenta';
import { mensajeErrorCuenta, nombresDe, yaAprobo } from './modelo';
import { PropuestaCuenta } from './PropuestaCuenta';

/** Cuenta bancaria del vínculo: la designada, la que el tercero propuso (cuatro ojos) y la última rechazada. */
export function TarjetaCuentaDesignada({ vinculo }: { vinculo: Pick<VinculoDto, 'id'> }) {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.vinculosLeer);
  const consulta = useCuentaDesignada(empresa.id, vinculo.id, puedeLeer);

  if (!puedeLeer) return null;

  return (
    <Tarjeta titulo={t('cuentas.titulo')}>
      {consulta.isPending && <EsqueletoTarjeta alto={96} />}
      {consulta.isError && <ErrorRecuperable mensaje={`${t('cuentas.errorCarga')} ${mensajeErrorCuenta(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      {consulta.data && <ContenidoCuenta datos={consulta.data} empresaId={empresa.id} vinculoId={vinculo.id} tienePermiso={permisos.puede(SCOPES.cuentasAprobar)} />}
    </Tarjeta>
  );
}

interface ContenidoCuentaProps {
  datos: CuentaDesignadaDelVinculoDto;
  empresaId: string;
  vinculoId: string;
  tienePermiso: boolean;
}

/**
 * Las mutaciones y los diálogos viven aquí y no en la propuesta: al aprobar la segunda persona la propuesta desaparece
 * (queda designada) y el aviso de éxito debe llegar igual.
 */
function ContenidoCuenta({ datos, empresaId, vinculoId, tienePermiso }: ContenidoCuentaProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const usuarioId = useSesion((s) => s.sesion?.usuarioId ?? null);
  const aprobar = useAprobarCuenta(empresaId, vinculoId);
  const rechazar = useRechazarCuenta(empresaId, vinculoId);
  const [dialogo, setDialogo] = useState<{ tipo: 'aprobar' | 'rechazar'; cuenta: CuentaDesignadaDto } | null>(null);
  const { actual, propuesta, rechazada, aprobadoresRequeridos } = datos;
  const total = String(aprobadoresRequeridos);

  const confirmarAprobacion = (cuenta: CuentaDesignadaDto) =>
    aprobar.mutate(undefined, {
      onSuccess: (nuevo) => mostrar(nuevo.actual?.cuentaId === cuenta.cuentaId ? t('cuentas.designada') : t('cuentas.aprobada')),
      onError: (error) => mostrar(mensajeErrorCuenta(error, t), 'error'),
      onSettled: () => setDialogo(null),
    });

  const confirmarRechazo = (motivo: string) =>
    rechazar.mutate(motivo, {
      onSuccess: () => {
        mostrar(t('cuentas.rechazadaOk'));
        setDialogo(null);
      },
    });

  return (
    <>
      <Stack spacing={2} divider={<Divider flexItem />}>
        {actual && (
          <SeccionCuenta titulo={t('cuentas.secciones.designada')} cuenta={actual} fecha={actual.aprobadaEn}>
            <DatosCuenta cuenta={actual} />
            {actual.aprobadaEn && (
              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                {actual.aprobaciones.length > 0
                  ? t('cuentas.aprobadaPor', { nombres: nombresDe(actual.aprobaciones, t), fecha: formatearFechaHora(actual.aprobadaEn) })
                  : t('cuentas.aprobadaEl', { fecha: formatearFechaHora(actual.aprobadaEn) })}
              </Typography>
            )}
          </SeccionCuenta>
        )}
        {propuesta && (
          <PropuestaCuenta
            cuenta={propuesta}
            aprobadoresRequeridos={aprobadoresRequeridos}
            hayDesignada={actual !== null}
            puedeAprobar={tienePermiso && datos.puedesAprobar}
            yaAprobo={tienePermiso && !datos.puedesAprobar && yaAprobo(propuesta, usuarioId)}
            ocupado={aprobar.isPending || rechazar.isPending}
            onAprobar={() => setDialogo({ tipo: 'aprobar', cuenta: propuesta })}
            onRechazar={() => {
              rechazar.reset();
              setDialogo({ tipo: 'rechazar', cuenta: propuesta });
            }}
          />
        )}
        {!actual && !propuesta && <EstadoVacio compacto icono={Landmark} titulo={t('cuentas.vacio')} texto={t('cuentas.vacioAyuda')} />}
        {rechazada && <CuentaRechazada cuenta={rechazada} />}
      </Stack>
      {dialogo?.tipo === 'aprobar' && (
        <DialogoConfirmacion
          titulo={t('cuentas.aprobarTitulo')}
          mensaje={dialogo.cuenta.aprobaciones.length + 1 >= aprobadoresRequeridos ? t('cuentas.aprobarUltima', { total }) : t('cuentas.aprobarPrimera')}
          confirmar={t('cuentas.aprobar')}
          tono="primary"
          ocupado={aprobar.isPending}
          onConfirmar={() => confirmarAprobacion(dialogo.cuenta)}
          onCerrar={() => setDialogo(null)}
        />
      )}
      {dialogo?.tipo === 'rechazar' && (
        <DialogoRechazarCuenta
          ocupado={rechazar.isPending}
          error={rechazar.isError ? mensajeErrorCuenta(rechazar.error, t) : null}
          onRechazar={confirmarRechazo}
          onCerrar={() => setDialogo(null)}
        />
      )}
    </>
  );
}
