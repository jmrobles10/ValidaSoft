import { Alert, Box, Button, FormControlLabel, Stack, Switch, TextField, Typography } from '@mui/material';
import type { PlanIaEmpresaDto } from '@validasoft/api-client';
import { AlineadoConCampo, CabeceraEsqueleto, DosColumnas, EsqueletoFormulario, EsqueletoTarjeta, FaltaParaEnviar } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, ErrorRecuperable, IconoCarga, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useGrupo, useGuardarPlanIa, usePlanIa, useProbarLlave } from './api';
import { LlaveOpenAi } from './LlaveOpenAi';
import { borradorDesdePlan, cuerpoPlanIa, validarPlanIa, type BorradorPlanIa, type ErrorPlanIa } from './modelo';
import { ResumenPlanIa } from './ResumenPlanIa';

type Campo = 'incluidas' | 'tope' | 'precio' | 'modelos' | 'umbral';

function FormularioPlan({ tenantId, empresaId, plan, empresa, grupo }: { tenantId: string; empresaId: string; plan: PlanIaEmpresaDto; empresa: string; grupo: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [b, setB] = useState<BorradorPlanIa>(() => borradorDesdePlan(plan));
  const [tocados, setTocados] = useState<ReadonlySet<Campo>>(new Set());
  const guardar = useGuardarPlanIa(tenantId, empresaId);
  const probar = useProbarLlave(tenantId, empresaId);

  const errores = validarPlanIa(b);
  const primero = errores[0];
  const mensaje = (e: ErrorPlanIa) => t(`plataforma.planIa.errores.${e}`);
  const errorDe = (campo: Campo, codigo: ErrorPlanIa) => (errores.includes(codigo) && tocados.has(campo) ? mensaje(codigo) : null);
  const tocar = (campo: Campo) => setTocados((p) => new Set([...p, campo]));
  const cambiar = (parcial: Partial<BorradorPlanIa>) => setB((p) => ({ ...p, ...parcial }));

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (primero) return;
    guardar.mutate(cuerpoPlanIa(b), {
      onSuccess: (guardado) => {
        mostrar(t('plataforma.planIa.guardado', { empresa }));
        setB(borradorDesdePlan(guardado));
        probar.reset();
      },
    });
  };

  const numero = (campo: Campo, etiqueta: string, valor: string, alCambiar: (v: string) => void, codigo: ErrorPlanIa, ayuda?: string) => {
    const e = errorDe(campo, codigo);
    return (
      <TextField
        label={etiqueta}
        value={valor}
        onChange={(ev) => alCambiar(ev.target.value)}
        onBlur={() => tocar(campo)}
        error={e !== null}
        helperText={e ?? ayuda}
        slotProps={{ htmlInput: { inputMode: 'decimal', autoComplete: 'off' } }}
        sx={{ flex: 1, minWidth: 0 }}
      />
    );
  };

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <EncabezadoPagina
        antes={<EnlaceVolver a="/plataforma/grupos">{t('plataforma.planIa.volver')}</EnlaceVolver>}
        titulo={t('plataforma.planIa.titulo')}
        subtitulo={t('plataforma.planIa.subtitulo', { empresa, grupo })}
        acciones={
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            {primero && <FaltaParaEnviar>{mensaje(primero)}</FaltaParaEnviar>}
            <Button type="submit" variant="contained" disabled={guardar.isPending || primero !== undefined} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
              {t('plataforma.planIa.guardar')}
            </Button>
          </Stack>
        }
      />
      {!plan.configurado && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {t('plataforma.planIa.sinConfigurar')}
        </Alert>
      )}
      {guardar.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mensajeDeError(guardar.error, t)}
        </Alert>
      )}
      <DosColumnas
        izquierda={
          <>
            <Tarjeta titulo={t('plataforma.planIa.cupo')}>
              <Stack spacing={2}>
                <Box sx={{ maxWidth: { sm: 'calc(50% - 6px)' } }}>
                  {numero('incluidas', t('plataforma.planIa.incluidas'), b.incluidas, (v) => cambiar({ incluidas: v }), 'incluidas-invalidas', t('plataforma.planIa.incluidasAyuda'))}
                </Box>
                <Box>
                  <FormControlLabel control={<Switch checked={b.adicional} onChange={(e) => cambiar({ adicional: e.target.checked })} />} label={t('plataforma.planIa.adicional')} />
                  <Typography variant="caption" component="p" sx={{ color: 'text.secondary' }}>
                    {t('plataforma.planIa.adicionalAyuda')}
                  </Typography>
                </Box>
                {b.adicional && (
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                    {numero('tope', t('plataforma.planIa.tope'), b.tope, (v) => cambiar({ tope: v }), 'tope-invalido', t('plataforma.planIa.topeAyuda'))}
                    {numero('precio', t('plataforma.planIa.precio'), b.precio, (v) => cambiar({ precio: v }), 'precio-invalido', t('plataforma.planIa.precioAyuda'))}
                  </Stack>
                )}
              </Stack>
            </Tarjeta>
            <Tarjeta titulo={t('plataforma.planIa.modelos')}>
              <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5}>
                {numero('modelos', t('plataforma.planIa.modeloPorDefecto'), b.modeloPorDefecto, (v) => cambiar({ modeloPorDefecto: v }), 'modelo-invalido')}
                {numero('modelos', t('plataforma.planIa.modeloEscalamiento'), b.modeloEscalamiento, (v) => cambiar({ modeloEscalamiento: v }), 'modelo-invalido')}
                {numero('umbral', t('plataforma.planIa.umbral'), b.umbral, (v) => cambiar({ umbral: v }), 'umbral-invalido', t('plataforma.planIa.umbralAyuda'))}
              </Stack>
            </Tarjeta>
            <Tarjeta>
              <AlineadoConCampo conEtiqueta={false}>
                <FormControlLabel control={<Switch checked={b.activo} onChange={(e) => cambiar({ activo: e.target.checked })} />} label={t('plataforma.planIa.activo')} />
              </AlineadoConCampo>
              <Typography variant="caption" component="p" sx={{ color: 'text.secondary' }}>
                {t('plataforma.planIa.activoAyuda')}
              </Typography>
            </Tarjeta>
          </>
        }
        derecha={
          <>
            <Tarjeta titulo={t('plataforma.planIa.llave')}>
              <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
                {t('plataforma.planIa.llaveAyuda')}
              </Typography>
              <LlaveOpenAi
                configurada={plan.llaveConfigurada}
                ultimosCuatro={plan.ultimosCuatro ?? null}
                cambio={b.llave}
                onCambio={(llave) => cambiar({ llave })}
                onProbar={(llave) => probar.mutate(llave)}
                probando={probar.isPending}
                prueba={probar.data ?? null}
                errorPrueba={probar.isError ? mensajeDeError(probar.error, t) : null}
                errorLlave={b.llave.tipo === 'reemplazar' && b.llave.valor && errores.includes('llave-invalida') ? mensaje('llave-invalida') : null}
              />
            </Tarjeta>
            <ResumenPlanIa plan={plan} borrador={b} />
          </>
        }
      />
    </Box>
  );
}

/** Plan de IA de una empresa (ADR-0012, IA-2): cupo, modelos, umbral, activo y su API key de OpenAI de solo escritura. */
export function PaginaPlanIa() {
  const { t } = useTranslation();
  const { tenantId = '', empresaId = '' } = useParams();
  const plan = usePlanIa(tenantId, empresaId);
  const grupo = useGrupo(tenantId);
  const empresa = grupo.data?.empresas.find((e) => e.id === empresaId);

  if (plan.isError) return <ErrorRecuperable mensaje={mensajeDeError(plan.error, t)} onReintentar={() => void plan.refetch()} />;
  if (!plan.data || grupo.isPending) {
    return (
      <>
        <CabeceraEsqueleto />
        <DosColumnas
          izquierda={
            <>
              <EsqueletoFormulario campos={2} conTarjeta />
              <EsqueletoFormulario campos={3} columnas={2} conTarjeta />
            </>
          }
          derecha={
            <>
              <EsqueletoFormulario campos={2} conTarjeta />
              <EsqueletoTarjeta lineas={4} />
            </>
          }
        />
      </>
    );
  }

  // Al guardar, el formulario se rehace con lo que devolvió el servidor (sin la llave escrita).
  return <FormularioPlan key={`${tenantId}:${empresaId}`} tenantId={tenantId} empresaId={empresaId} plan={plan.data} empresa={empresa?.nombre ?? '—'} grupo={grupo.data?.tenant.nombre ?? '—'} />;
}
