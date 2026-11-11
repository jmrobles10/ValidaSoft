import { Box, Chip, IconButton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tooltip, Typography } from '@mui/material';
import type { ConsumoDeIaEmpresaDto, ConsumoDeIaGrupoDto } from '@validasoft/api-client';
import { MarcoTabla } from '@validasoft/ui';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { aNumero, formatearEntero, formatearMoneda, formatearTokens, formatearUsd } from './modeloConsumo';

const NUMERICA = { whiteSpace: 'nowrap' } as const;

function Cifra({ children }: { children: string }) {
  return (
    <Typography variant="monoTabla" sx={NUMERICA}>
      {children}
    </Typography>
  );
}

/** Una empresa: lo incluido que usó, lo adicional con su precio (propio o general), tokens y costo; debajo, los modelos que usó. */
function FilaEmpresa({ empresa, moneda }: { empresa: ConsumoDeIaEmpresaDto; moneda: string }) {
  const { t } = useTranslation();
  return (
    <TableRow sx={{ bgcolor: 'superficie.nivel1' }}>
      <TableCell sx={{ pl: 7, verticalAlign: 'top' }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography sx={{ fontSize: 14 }}>{empresa.empresa}</Typography>
          {empresa.precioPropio && (
            <Tooltip title={t('consola.consumo.precioPropioAyuda', { precio: formatearMoneda(empresa.precioAdicional, moneda) })}>
              <Chip size="small" variant="outlined" label={t('consola.consumo.precioPropio')} />
            </Tooltip>
          )}
          {!empresa.adicionalHabilitado && <Chip size="small" variant="outlined" label={t('consola.consumo.sinAdicional')} />}
        </Stack>
        {empresa.modelos.length > 0 && (
          <Typography variant="caption" component="p" sx={{ color: 'text.secondary', mt: 0.25 }}>
            {empresa.modelos.map((m) => t('consola.consumo.modelo', { modelo: m.modelo, llamadas: formatearEntero(m.llamadas), costo: formatearUsd(m.costoUsd) })).join(' · ')}
          </Typography>
        )}
      </TableCell>
      <TableCell align="right" sx={{ verticalAlign: 'top' }}>
        <Cifra>{`${formatearEntero(empresa.incluidasConsumidas)} / ${formatearEntero(empresa.validacionesIncluidasMes)}`}</Cifra>
      </TableCell>
      <TableCell align="right" sx={{ verticalAlign: 'top' }}>
        <Cifra>{formatearEntero(empresa.adicionalesConsumidas)}</Cifra>
      </TableCell>
      <TableCell align="right" sx={{ verticalAlign: 'top' }}>
        <Cifra>{`${formatearTokens(empresa.tokensEntrada)} · ${formatearTokens(empresa.tokensSalida)}`}</Cifra>
      </TableCell>
      <TableCell align="right" sx={{ verticalAlign: 'top' }}>
        <Cifra>{formatearUsd(empresa.costoUsd)}</Cifra>
      </TableCell>
      <TableCell align="right" sx={{ verticalAlign: 'top' }}>
        <Cifra>{aNumero(empresa.valorAdicional) > 0 ? formatearMoneda(empresa.valorAdicional, moneda) : '—'}</Cifra>
      </TableCell>
    </TableRow>
  );
}

function FilaGrupo({ grupo, moneda }: { grupo: ConsumoDeIaGrupoDto; moneda: string }) {
  const { t } = useTranslation();
  const [abierta, setAbierta] = useState(false);
  return (
    <>
      <TableRow hover>
        <TableCell sx={{ height: 44 }}>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', minWidth: 0 }}>
            <IconButton
              size="small"
              aria-expanded={abierta}
              aria-label={abierta ? t('consola.consumo.ocultarEmpresas', { grupo: grupo.grupo }) : t('consola.consumo.verEmpresas', { grupo: grupo.grupo, count: grupo.empresas.length })}
              onClick={() => setAbierta((v) => !v)}
            >
              {abierta ? <ChevronDown size={16} aria-hidden /> : <ChevronRight size={16} aria-hidden />}
            </IconButton>
            <Typography noWrap sx={{ fontWeight: 600 }}>
              {grupo.grupo}
            </Typography>
            <Chip size="small" variant="outlined" label={grupo.slug} sx={{ fontFamily: (th) => th.typography.mono.fontFamily }} />
          </Stack>
        </TableCell>
        <TableCell align="right">
          <Cifra>{formatearEntero(grupo.incluidasConsumidas)}</Cifra>
        </TableCell>
        <TableCell align="right">
          <Cifra>{formatearEntero(grupo.adicionalesConsumidas)}</Cifra>
        </TableCell>
        <TableCell align="right">
          <Cifra>{`${formatearTokens(grupo.tokensEntrada)} · ${formatearTokens(grupo.tokensSalida)}`}</Cifra>
        </TableCell>
        <TableCell align="right">
          <Cifra>{formatearUsd(grupo.costoUsd)}</Cifra>
        </TableCell>
        <TableCell align="right">
          <Cifra>{aNumero(grupo.valorAdicional) > 0 ? formatearMoneda(grupo.valorAdicional, moneda) : '—'}</Cifra>
        </TableCell>
      </TableRow>
      {abierta && grupo.empresas.map((e) => <FilaEmpresa key={e.empresaId} empresa={e} moneda={moneda} />)}
    </>
  );
}

/** Consumo por grupo, con sus empresas al desplegar: validaciones, tokens, costo del proveedor y valor de lo adicional. */
export function TablaConsumo({ grupos, moneda }: { grupos: readonly ConsumoDeIaGrupoDto[]; moneda: string }) {
  const { t } = useTranslation();
  return (
    <MarcoTabla>
      <TableContainer>
        <Table size="small" aria-label={t('consola.consumo.titulo')}>
          <TableHead>
            <TableRow>
              <TableCell>{t('consola.consumo.columnas.cliente')}</TableCell>
              <TableCell align="right" sx={{ width: 130 }}>
                {t('consola.consumo.columnas.incluidas')}
              </TableCell>
              <TableCell align="right" sx={{ width: 120 }}>
                {t('consola.consumo.columnas.adicionales')}
              </TableCell>
              <TableCell align="right" sx={{ width: 170 }}>
                {t('consola.consumo.columnas.tokens')}
              </TableCell>
              <TableCell align="right" sx={{ width: 130 }}>
                {t('consola.consumo.columnas.costo')}
              </TableCell>
              <TableCell align="right" sx={{ width: 150 }}>
                {t('consola.consumo.columnas.valor')}
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {grupos.map((g) => (
              <FilaGrupo key={g.grupoId} grupo={g} moneda={moneda} />
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Box sx={{ px: 2, py: 1, borderTop: 1, borderColor: 'divider' }}>
        <Typography variant="caption" sx={{ color: 'text.secondary' }}>
          {t('consola.consumo.notaTokens')}
        </Typography>
      </Box>
    </MarcoTabla>
  );
}
