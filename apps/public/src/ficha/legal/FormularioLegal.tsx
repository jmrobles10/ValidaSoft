import { Alert, Button, Chip, Paper, Stack, Typography } from '@mui/material';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CampoTexto, Rejilla, Subtitulo } from '../campos';
import type { DatosLegales, PersonaRelacionada, PropsFormulario } from '../tipos';
import { DialogoPersona } from './DialogoPersona';
import { esRol, nombrePersona } from './personas';

/**
 * Información legal (`FICHA_LEGAL`, fase 1 de FM-7): las personas relacionadas como tarjetas (agregar, editar en un
 * diálogo, quitar) y la firma de revisoría fiscal si el revisor actúa por una firma.
 */
export function FormularioLegal({ datos, onCambiar, mensaje, deshabilitado }: PropsFormulario<DatosLegales>) {
  const { t } = useTranslation();
  const personas = datos.personas ?? [];
  // Índice de la persona en edición; `personas.length` = una nueva.
  const [editando, setEditando] = useState<number | null>(null);
  const firma = datos.revisor_fiscal_firma ?? {};

  const guardarPersona = (i: number, persona: PersonaRelacionada) => {
    onCambiar({ personas: i < personas.length ? personas.map((p, j) => (j === i ? persona : p)) : [...personas, persona] });
    setEditando(null);
  };
  const quitar = (i: number) => {
    const resto = personas.filter((_, j) => j !== i);
    onCambiar({ personas: resto.length > 0 ? resto : null });
  };
  const cambiarFirma = (cambio: { nit?: string | null; razon_social?: string | null }) => {
    const nueva = { ...firma, ...cambio };
    onCambiar({ revisor_fiscal_firma: nueva.nit || nueva.razon_social ? nueva : null });
  };

  return (
    <Stack spacing={2}>
      <Subtitulo ayuda={t('ficha.legal.personasAyuda')}>{t('ficha.legal.personas')}</Subtitulo>
      {mensaje('personas') && <Alert severity="error">{mensaje('personas')}</Alert>}
      {personas.length === 0 ? (
        <Typography sx={{ color: 'text.secondary' }}>{t('ficha.legal.sinPersonas')}</Typography>
      ) : (
        <Stack component="ul" spacing={1.5} aria-label={t('ficha.legal.personas')} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {personas.map((persona, i) => (
            <TarjetaPersona key={`${persona.rol ?? ''}-${persona.numero ?? ''}-${String(i)}`} persona={persona} indice={i} mensaje={mensaje} onEditar={() => setEditando(i)} onQuitar={() => quitar(i)} deshabilitado={deshabilitado} />
          ))}
        </Stack>
      )}
      <Button variant="outlined" startIcon={<Plus size={18} aria-hidden />} onClick={() => setEditando(personas.length)} disabled={deshabilitado} sx={{ alignSelf: { xs: 'stretch', sm: 'flex-start' }, minHeight: 44 }}>
        {t('ficha.legal.agregar')}
      </Button>
      <Subtitulo>{t('ficha.legal.firma')}</Subtitulo>
      <Rejilla>
        <CampoTexto etiqueta={t('ficha.legal.firmaNit')} valor={firma.nit} onCambiar={(nit) => cambiarFirma({ nit })} mensaje={mensaje('revisor_fiscal_firma.nit')} maxLength={20} deshabilitado={deshabilitado} />
        <CampoTexto etiqueta={t('ficha.legal.firmaRazonSocial')} valor={firma.razon_social} onCambiar={(razon) => cambiarFirma({ razon_social: razon })} mensaje={mensaje('revisor_fiscal_firma.razon_social')} deshabilitado={deshabilitado} />
      </Rejilla>
      {editando !== null && (
        <DialogoPersona
          inicial={personas[editando] ?? {}}
          nueva={editando >= personas.length}
          mensaje={(campo) => mensaje(`personas[${String(editando)}].${campo}`)}
          onGuardar={(persona) => guardarPersona(editando, persona)}
          onCerrar={() => setEditando(null)}
        />
      )}
    </Stack>
  );
}

interface PropsTarjeta {
  persona: PersonaRelacionada;
  indice: number;
  mensaje: (ruta: string) => string | null;
  onEditar: () => void;
  onQuitar: () => void;
  deshabilitado: boolean;
}

/** Una persona: nombre, rol, identificación y cuántos datos le faltan según el servidor. */
function TarjetaPersona({ persona, indice, mensaje, onEditar, onQuitar, deshabilitado }: PropsTarjeta) {
  const { t } = useTranslation();
  const nombre = nombrePersona(persona) || t('ficha.legal.sinNombre');
  const ruta = `personas[${String(indice)}]`;
  const campos = ['rol', 'tipo_identificacion', 'numero', 'dv', 'nombres', 'apellidos', 'cargo', 'porcentaje', 'pep', 'vinculo_pep', 'fecha_expedicion', 'fecha_nacimiento', 'nacionalidad', 'residencia', 'correo', 'telefono', 'tipo_representacion'];
  const marcados = campos.filter((c) => mensaje(`${ruta}.${c}`)).length;
  const errorFila = mensaje(ruta);

  return (
    <Paper component="li" variant="outlined" aria-label={nombre} sx={{ p: 1.5 }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' } }}>
        <Stack sx={{ minWidth: 0, flex: 1 }} spacing={0.5}>
          <Typography sx={{ fontWeight: 500 }}>{nombre}</Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>
            {[persona.rol && esRol(persona.rol) ? t(`ficha.legal.roles.${persona.rol}`) : null, persona.numero ? `${persona.tipo_identificacion ?? ''} ${persona.numero}`.trim() : null, persona.porcentaje !== null && persona.porcentaje !== undefined ? `${String(persona.porcentaje)} %` : null]
              .filter(Boolean)
              .join(' · ')}
          </Typography>
          {marcados > 0 && <Chip size="small" color="warning" variant="outlined" label={t('ficha.legal.faltan', { count: marcados })} sx={{ alignSelf: 'flex-start' }} />}
          {errorFila && <Typography sx={{ color: 'error.main', fontSize: 14 }}>{errorFila}</Typography>}
        </Stack>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<Pencil size={16} aria-hidden />} onClick={onEditar} disabled={deshabilitado} aria-label={t('ficha.legal.editarAccesible', { nombre })} sx={{ minHeight: 44, flex: { xs: 1, sm: 'none' } }}>
            {t('ficha.editar')}
          </Button>
          <Button variant="text" color="inherit" startIcon={<Trash2 size={16} aria-hidden />} onClick={onQuitar} disabled={deshabilitado} aria-label={t('ficha.legal.quitarAccesible', { nombre })} sx={{ minHeight: 44, flex: { xs: 1, sm: 'none' } }}>
            {t('ficha.quitar')}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
