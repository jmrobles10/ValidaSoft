import { Box, Button, Card, Typography } from '@mui/material';
import type { CuentaBancariaPublicaDto, VinculoPublicoDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FilaEmpresa } from '../shared/componentes';
import { designacionesDelVinculo, esActiva, nombreCuenta, vinculosParaDesignar } from './cuentas';
import { DialogoDesignar } from './DialogoDesignar';

/**
 * Una fila por empresa con la que el tercero puede designar (invitación aceptada, vínculo vigente): qué cuenta tiene
 * aprobada, cuál espera su aprobación o cuál rechazó, y «Elegir cuenta» / «Cambiar cuenta».
 */
export function CuentasPorEmpresa({ vinculos, cuentas }: { vinculos: readonly VinculoPublicoDto[]; cuentas: readonly CuentaBancariaPublicaDto[] }) {
  const { t } = useTranslation();
  const [designando, setDesignando] = useState<VinculoPublicoDto | null>(null);
  const elegibles = vinculosParaDesignar(vinculos);
  const hayActivas = cuentas.some(esActiva);

  const detalle = (vinculoId: string) => {
    const { Aprobada: aprobada, Propuesta: propuesta, Rechazada: rechazada } = designacionesDelVinculo(vinculoId, cuentas);
    const partes = [
      aprobada && t('cuentas.porEmpresa.designada', { cuenta: nombreCuenta(aprobada.cuenta) }),
      propuesta && t('cuentas.porEmpresa.propuesta', { cuenta: nombreCuenta(propuesta.cuenta) }),
      rechazada &&
        (rechazada.motivoRechazo
          ? t('cuentas.porEmpresa.rechazada', { cuenta: nombreCuenta(rechazada.cuenta), motivo: rechazada.motivoRechazo })
          : t('cuentas.porEmpresa.rechazadaSinMotivo', { cuenta: nombreCuenta(rechazada.cuenta) })),
    ].filter((p) => typeof p === 'string');
    // El chip dice lo más urgente: lo que espera aprobación, luego lo rechazado y por último lo aprobado.
    const estado = propuesta?.estado ?? rechazada?.estado ?? aprobada?.estado ?? null;
    return { texto: partes.length > 0 ? partes.join(' ') : t('cuentas.porEmpresa.sinCuenta'), estado, tieneCuenta: Boolean(aprobada ?? propuesta) };
  };

  return (
    <Box component="section" aria-labelledby="titulo-por-empresa" sx={{ mt: 3 }}>
      <Typography id="titulo-por-empresa" component="h2" sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px', mb: 1 }}>
        {t('cuentas.porEmpresa.titulo')}
      </Typography>
      {elegibles.length === 0 ? (
        <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px' }}>
          {t('cuentas.porEmpresa.sinVinculos')}
        </Typography>
      ) : (
        <Card sx={{ p: 0, overflow: 'hidden' }}>
          {/* La primera fila no lleva divisor: el borde de la tarjeta ya separa. */}
          <Box component="ul" aria-labelledby="titulo-por-empresa" sx={{ p: 0, m: 0, '& > li:first-of-type': { borderTop: 0 } }}>
            {elegibles.map((v) => {
              const { texto, estado, tieneCuenta } = detalle(v.id);
              return (
                <FilaEmpresa
                  key={v.id}
                  nombre={v.empresa.nombre}
                  estado={estado ? <ChipEstado estado={estado} etiqueta={t(`cuentas.designacion.chip.${estado}`)} /> : undefined}
                  detalle={hayActivas ? texto : `${texto} ${t('cuentas.porEmpresa.primeroAgrega')}`}
                  accion={
                    <Button
                      variant="text"
                      size="small"
                      disabled={!hayActivas}
                      onClick={() => setDesignando(v)}
                      aria-label={`${tieneCuenta ? t('cuentas.porEmpresa.cambiar') : t('cuentas.porEmpresa.elegir')}: ${v.empresa.nombre}`}
                      sx={{ minHeight: 44, ml: -1, '@media (min-width:600px)': { minHeight: 32, ml: 0 } }}
                    >
                      {tieneCuenta ? t('cuentas.porEmpresa.cambiar') : t('cuentas.porEmpresa.elegir')}
                    </Button>
                  }
                />
              );
            })}
          </Box>
        </Card>
      )}
      {designando && <DialogoDesignar vinculo={designando} cuentas={cuentas} onCerrar={() => setDesignando(null)} />}
    </Box>
  );
}
