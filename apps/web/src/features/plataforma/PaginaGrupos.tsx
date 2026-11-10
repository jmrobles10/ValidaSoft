import { Box, Button, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { GrupoDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFecha, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Building2, CircleDashed, KeyRound, Plus, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { identificacionVisible } from '../configuracion/empresas/modelo';
import { EstadoActivo } from '../configuracion/EstadoActivo';
import { useGrupos } from './api';
import { estadoPlanIa, llaveEnmascarada } from './modelo';

function TablaEmpresas({ grupo }: { grupo: GrupoDto }) {
  const { t } = useTranslation();
  if (grupo.empresas.length === 0)
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('plataforma.grupos.sinEmpresas')}
      </Typography>
    );

  return (
    <MarcoTabla>
      <TableContainer>
        <Table size="small" aria-label={t('plataforma.grupos.columnas.empresa')}>
          <TableHead>
            <TableRow>
              <TableCell>{t('plataforma.grupos.columnas.empresa')}</TableCell>
              <TableCell sx={{ width: 180 }}>{t('plataforma.grupos.columnas.identificacion')}</TableCell>
              <TableCell sx={{ width: 100 }}>{t('plataforma.grupos.columnas.prefijo')}</TableCell>
              <TableCell sx={{ width: 210 }}>{t('plataforma.grupos.columnas.planIa')}</TableCell>
              <TableCell sx={{ width: 120 }}>
                <Box component="span" sx={visualmenteOculto}>
                  {t('plataforma.grupos.columnas.acciones')}
                </Box>
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {grupo.empresas.map((e) => {
              const estado = estadoPlanIa(e);
              return (
                <TableRow key={e.id}>
                  <TableCell>
                    <Typography component="span" sx={{ fontWeight: 500, fontSize: 14 }}>
                      {e.nombre}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{identificacionVisible(e.identificacion)}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{e.prefijoFolio}</Typography>
                  </TableCell>
                  <TableCell>
                    <EstadoActivo
                      activo={estado === 'activo'}
                      icono={estado === 'activo' ? KeyRound : estado === 'sin-plan' ? CircleDashed : undefined}
                      texto={t(`plataforma.grupos.estadoPlan.${estado}`, { llave: llaveEnmascarada(e.ultimosCuatro) })}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Button
                      component={RouterLink}
                      to={`/plataforma/grupos/${grupo.tenant.id}/empresas/${e.id}/plan-ia`}
                      size="small"
                      variant="text"
                      startIcon={<Sparkles size={14} aria-hidden />}
                      aria-label={t('plataforma.grupos.configurarPlanDe', { empresa: e.nombre })}
                    >
                      {t('plataforma.grupos.configurarPlan')}
                    </Button>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </MarcoTabla>
  );
}

/** Los grupos (tenants) con sus empresas y el estado del plan de IA de cada una. */
export function PaginaGrupos() {
  const { t } = useTranslation();
  const consulta = useGrupos();

  return (
    <>
      <EncabezadoPagina
        titulo={t('plataforma.grupos.titulo')}
        subtitulo={t('plataforma.grupos.subtitulo')}
        acciones={
          <Button component={RouterLink} to="/plataforma/grupos/nuevo" variant="contained" startIcon={<Plus size={16} aria-hidden />}>
            {t('plataforma.grupos.nuevo')}
          </Button>
        }
      />
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && (
        <Typography variant="body2" role="status" sx={{ color: 'text.secondary' }}>
          {t('comun.cargando')}
        </Typography>
      )}
      {consulta.data?.length === 0 && <EstadoVacio icono={Building2} titulo={t('plataforma.grupos.vacio')} texto={t('plataforma.grupos.vacioAyuda')} />}
      <Stack spacing={2}>
        {consulta.data?.map((grupo) => (
          <Tarjeta
            key={grupo.tenant.id}
            etiqueta={grupo.tenant.nombre}
            titulo={
              <Stack direction="row" spacing={1} component="span" sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <span>{grupo.tenant.nombre}</span>
                <Chip size="small" variant="outlined" label={grupo.tenant.slug} sx={{ fontFamily: (th) => th.typography.mono.fontFamily }} />
                {!grupo.tenant.activo && <Chip size="small" label={t('plataforma.grupos.inactivo')} />}
              </Stack>
            }
            acciones={
              <Typography variant="caption" sx={{ color: 'text.secondary', whiteSpace: 'nowrap' }}>
                {t('plataforma.grupos.creado', { fecha: formatearFecha(grupo.tenant.creadoEn) })}
              </Typography>
            }
          >
            <TablaEmpresas grupo={grupo} />
          </Tarjeta>
        ))}
      </Stack>
    </>
  );
}
