import { Box, Button, Card, Stack, Typography } from '@mui/material';
import type { CuentaBancariaPublicaDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { Archive, Landmark } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FilaEmpresa } from '../shared/componentes';
import { designacionesVisibles, esActiva, esTipoCuenta, nombreCuenta } from './cuentas';
import { DialogoRetirarCuenta } from './DialogoRetirarCuenta';

/**
 * Una cuenta del tercero (misma anatomía que la tarjeta de un documento de la bóveda): cabecera con banco, tipo, número
 * enmascarado y titular, y debajo una fila por empresa con el estado de la designación.
 */
export function TarjetaCuenta({ cuenta }: { cuenta: CuentaBancariaPublicaDto }) {
  const { t } = useTranslation();
  const [retirando, setRetirando] = useState(false);
  const activa = esActiva(cuenta);
  const designaciones = designacionesVisibles(cuenta);
  const tipo = esTipoCuenta(cuenta.tipoCuenta) ? t(`cuentas.tipos.${cuenta.tipoCuenta}`) : cuenta.tipoCuenta;

  return (
    <Card component="article" aria-labelledby={`cuenta-${cuenta.id}`} sx={{ p: 0, overflow: 'hidden' }}>
      <Stack direction="row" alignItems="center" useFlexGap flexWrap="wrap" sx={{ columnGap: 1.25, rowGap: 0.5, py: 1, pl: { xs: 1.5, sm: 2 }, pr: 1 }}>
        <Box aria-hidden sx={(th) => ({ display: 'grid', placeItems: 'center', flex: 'none', width: 28, height: 28, borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2', color: 'text.secondary' })}>
          <Landmark size={18} />
        </Box>
        <Box sx={{ minWidth: 0, flex: '1 1 180px' }}>
          <Typography id={`cuenta-${cuenta.id}`} component="h2" sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px' }}>
            {cuenta.banco}
          </Typography>
          <Typography variant="mono" component="p" color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px' }}>
            {t('cuentas.resumen', { tipo, numero: cuenta.numeroEnmascarado, moneda: cuenta.moneda })}
          </Typography>
          <Typography color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px' }}>
            {t('cuentas.titularCon', { titular: cuenta.titular, identificacion: cuenta.identificacionTitular })}
          </Typography>
        </Box>
        <Stack direction="row" alignItems="center" spacing={0.5} sx={{ ml: 'auto', flex: 'none' }}>
          {activa ? <ChipEstado estado="Activo" etiqueta={t('cuentas.estados.Activa')} size="medium" /> : <ChipEstado estado="Retirada" etiqueta={t('cuentas.estados.Retirada')} size="medium" />}
          {activa && (
            <Button variant="text" onClick={() => setRetirando(true)} startIcon={<Archive size={16} aria-hidden />} aria-label={`${t('cuentas.retirar')}: ${nombreCuenta(cuenta)}`} sx={{ color: 'text.secondary', minHeight: 44, '@media (min-width:600px)': { minHeight: 36 } }}>
              {t('cuentas.retirar')}
            </Button>
          )}
        </Stack>
      </Stack>

      <Box component="ul" aria-label={t('cuentas.empresas')} sx={{ p: 0, m: 0 }}>
        {designaciones.length === 0 ? (
          <Typography component="li" color="text.secondary" sx={{ fontSize: 13, py: 1, px: { xs: 1.5, sm: 2 }, borderTop: 1, borderColor: 'divider', listStyle: 'none' }}>
            {t('cuentas.sinEmpresas')}
          </Typography>
        ) : (
          designaciones.map((d) => (
            <FilaEmpresa
              key={`${d.vinculoId}-${d.estado}`}
              nombre={d.empresa}
              estado={<ChipEstado estado={d.estado} etiqueta={t(`cuentas.designacion.chip.${d.estado}`)} />}
              detalle={
                d.estado === 'Rechazada' && !d.motivoRechazo
                  ? t('cuentas.designacion.RechazadaSinMotivo', { empresa: d.empresa })
                  : t(`cuentas.designacion.${d.estado}`, { empresa: d.empresa, motivo: d.motivoRechazo ?? '' })
              }
            />
          ))
        )}
      </Box>
      {retirando && <DialogoRetirarCuenta cuenta={cuenta} onCerrar={() => setRetirando(false)} />}
    </Card>
  );
}
