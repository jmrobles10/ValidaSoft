import { Box, Button, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { GrupoDto } from '@validasoft/api-client';
import { formatearFecha, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { ChevronDown, ChevronUp, CircleDashed, KeyRound, Plug, Plus, Sparkles } from 'lucide-react';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../shared/componentes';
import { DialogoEmpresa } from '../configuracion/empresas/DialogoEmpresa';
import { identificacionVisible } from '../configuracion/empresas/modelo';
import { EstadoActivo } from '../configuracion/EstadoActivo';
import { useCrearEmpresaDeGrupo } from './apiIdentidad';
import { DialogoSistemasDeGrupo } from './DialogoSistemasDeGrupo';
import { estadoPlanIa, llaveEnmascarada, MAX_EMPRESAS_SIN_COLAPSAR } from './modelo';

function TablaEmpresas({ grupo, id }: { grupo: GrupoDto; id: string }) {
  const { t } = useTranslation();
  return (
    <MarcoTabla>
      <TableContainer id={id}>
        <Table size="small" aria-label={t('plataforma.grupos.empresasDe', { grupo: grupo.tenant.nombre })}>
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

/** Agregar una empresa a un grupo que ya existe (PI-4): el mismo formulario del grupo, con la clave de idempotencia de este diálogo. */
function DialogoNuevaEmpresa({ grupo, onCerrar }: { grupo: GrupoDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const [clave] = useState(() => crypto.randomUUID());
  const mutacion = useCrearEmpresaDeGrupo(grupo.tenant.id, clave);
  return <DialogoEmpresa claveIdempotencia={clave} mutacion={mutacion} subtitulo={t('consola.empresa.paraElGrupo', { grupo: grupo.tenant.nombre })} onCerrar={onCerrar} onCreada={onCerrar} />;
}

/**
 * Un grupo con su identificador, el alta y la tabla de sus empresas con el estado del plan de IA. Con más de
 * {@link MAX_EMPRESAS_SIN_COLAPSAR} empresas la tabla va plegada y se despliega a pedido (`expandido` lo guarda la lista,
 * porque la tarjeta se desmonta al salir de la pantalla).
 */
export function TarjetaGrupo({ grupo, expandido, onAlternar }: { grupo: GrupoDto; expandido: boolean; onAlternar: () => void }) {
  const { t } = useTranslation();
  const idTabla = useId();
  const plegable = grupo.empresas.length > MAX_EMPRESAS_SIN_COLAPSAR;
  const [agregando, setAgregando] = useState(false);
  const [verSistemas, setVerSistemas] = useState(false);
  return (
    <Tarjeta
      etiqueta={grupo.tenant.nombre}
      titulo={
        <Stack direction="row" spacing={1} component="span" sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <span>{grupo.tenant.nombre}</span>
          <Chip size="small" variant="outlined" label={grupo.tenant.slug} sx={{ fontFamily: (th) => th.typography.mono.fontFamily }} />
          {!grupo.tenant.activo && <Chip size="small" label={t('plataforma.grupos.inactivo')} />}
        </Stack>
      }
      acciones={
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          <Typography variant="caption" sx={{ color: 'text.secondary', whiteSpace: 'nowrap' }}>
            {t('plataforma.grupos.empresasCuenta', { count: grupo.empresas.length })} · {t('plataforma.grupos.creado', { fecha: formatearFecha(grupo.tenant.creadoEn) })}
          </Typography>
          <Button size="small" variant="outlined" startIcon={<Plug size={14} aria-hidden />} onClick={() => setVerSistemas(true)} aria-label={t('consola.sistemas.verDe', { grupo: grupo.tenant.nombre })}>
            {t('consola.sistemas.ver')}
          </Button>
          <Button size="small" variant="outlined" startIcon={<Plus size={14} aria-hidden />} onClick={() => setAgregando(true)} aria-label={t('consola.empresa.agregarA', { grupo: grupo.tenant.nombre })}>
            {t('consola.empresa.agregar')}
          </Button>
        </Stack>
      }
    >
      {grupo.empresas.length === 0 ? (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('plataforma.grupos.sinEmpresas')}
        </Typography>
      ) : (
        <>
          {(!plegable || expandido) && <TablaEmpresas grupo={grupo} id={idTabla} />}
          {plegable && (
            <Button
              size="small"
              variant="text"
              onClick={onAlternar}
              aria-expanded={expandido}
              aria-controls={expandido ? idTabla : undefined}
              startIcon={expandido ? <ChevronUp size={16} aria-hidden /> : <ChevronDown size={16} aria-hidden />}
              sx={{ mt: expandido ? 1 : 0 }}
            >
              {expandido ? t('plataforma.grupos.ocultarEmpresas') : t('plataforma.grupos.mostrarEmpresas', { count: grupo.empresas.length })}
            </Button>
          )}
        </>
      )}
      {agregando && <DialogoNuevaEmpresa grupo={grupo} onCerrar={() => setAgregando(false)} />}
      {verSistemas && <DialogoSistemasDeGrupo tenantId={grupo.tenant.id} grupo={grupo.tenant.nombre} onCerrar={() => setVerSistemas(false)} />}
    </Tarjeta>
  );
}
