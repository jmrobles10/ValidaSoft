import { Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, useMediaQuery, useTheme } from '@mui/material';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { CampoCifra, CampoFechaFicha, CampoSiNo, CampoTexto, Rejilla } from '../campos';
import { PAISES } from '../paises';
import { SelectorUnico } from '../selectores';
import { ROLES, TIPOS_IDENTIFICACION_PERSONA, type PersonaRelacionada } from '../tipos';
import { esRol } from './personas';

interface Props {
  inicial: PersonaRelacionada;
  nueva: boolean;
  /** Mensaje del servidor para un campo de esta persona (`pep` ⇒ `personas[i].pep`). */
  mensaje: (campo: string) => string | null;
  onGuardar: (persona: PersonaRelacionada) => void;
  onCerrar: () => void;
}

/**
 * Agregar o editar una persona relacionada. Solo cambia la lista del formulario: se guarda con la sección. En el celular
 * ocupa toda la pantalla. El DV de un NIT lo calcula Validasoft.
 */
export function DialogoPersona({ inicial, nueva, mensaje, onGuardar, onCerrar }: Props) {
  const { t } = useTranslation();
  const tema = useTheme();
  const pantallaCompleta = useMediaQuery(tema.breakpoints.down('sm'));
  const [p, setP] = useState<PersonaRelacionada>(inicial);
  const cambiar = (cambio: PersonaRelacionada) => setP((actual) => ({ ...actual, ...cambio }));
  const esNit = p.tipo_identificacion === 'NIT';
  const titulo = nueva ? t('ficha.legal.nueva') : t('ficha.legal.editarPersona');

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    onGuardar(esNit ? { ...p, apellidos: null } : p);
  };

  return (
    <Dialog open onClose={onCerrar} fullWidth maxWidth="sm" fullScreen={pantallaCompleta} aria-labelledby="titulo-persona">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-persona">{titulo}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField select label={t('ficha.legal.rol')} value={p.rol ?? ''} onChange={(e) => cambiar({ rol: esRol(e.target.value) ? e.target.value : null })} error={Boolean(mensaje('rol'))} helperText={mensaje('rol')} fullWidth>
              {ROLES.map((rol) => (
                <MenuItem key={rol} value={rol}>
                  {t(`ficha.legal.roles.${rol}`)}
                </MenuItem>
              ))}
            </TextField>
            <CampoTexto etiqueta={t('ficha.legal.tipoRepresentacion')} valor={p.tipo_representacion} onCambiar={(v) => cambiar({ tipo_representacion: v })} mensaje={mensaje('tipo_representacion')} ayuda={t('ficha.legal.tipoRepresentacionAyuda')} maxLength={100} />
            <Rejilla>
              <TextField
                select
                label={t('ficha.legal.tipoIdentificacion')}
                value={p.tipo_identificacion ?? ''}
                onChange={(e) => cambiar({ tipo_identificacion: e.target.value || null })}
                error={Boolean(mensaje('tipo_identificacion'))}
                helperText={mensaje('tipo_identificacion')}
                fullWidth
              >
                {TIPOS_IDENTIFICACION_PERSONA.map((tipo) => (
                  <MenuItem key={tipo} value={tipo}>
                    {tipo}
                  </MenuItem>
                ))}
              </TextField>
              <CampoTexto etiqueta={t('ficha.legal.numero')} valor={p.numero} onCambiar={(v) => cambiar({ numero: v })} mensaje={mensaje('numero') ?? mensaje('dv')} maxLength={20} />
            </Rejilla>
            <Rejilla>
              <CampoTexto etiqueta={esNit ? t('ficha.legal.razonSocial') : t('ficha.legal.nombres')} valor={p.nombres} onCambiar={(v) => cambiar({ nombres: v })} mensaje={mensaje('nombres')} maxLength={200} />
              {!esNit && <CampoTexto etiqueta={t('ficha.legal.apellidos')} valor={p.apellidos} onCambiar={(v) => cambiar({ apellidos: v })} mensaje={mensaje('apellidos')} maxLength={150} />}
              <CampoTexto etiqueta={t('ficha.legal.cargo')} valor={p.cargo} onCambiar={(v) => cambiar({ cargo: v })} mensaje={mensaje('cargo')} maxLength={150} />
              <CampoCifra etiqueta={t('ficha.legal.porcentaje')} valor={p.porcentaje} onCambiar={(v) => cambiar({ porcentaje: v })} mensaje={mensaje('porcentaje')} />
            </Rejilla>
            <CampoSiNo etiqueta={t('ficha.legal.pep')} valor={p.pep} onCambiar={(v) => cambiar({ pep: v })} mensaje={mensaje('pep')} />
            <CampoSiNo etiqueta={t('ficha.legal.vinculoPep')} valor={p.vinculo_pep} onCambiar={(v) => cambiar({ vinculo_pep: v })} mensaje={mensaje('vinculo_pep')} />
            <Rejilla>
              <CampoFechaFicha etiqueta={t('ficha.legal.fechaExpedicion')} valor={p.fecha_expedicion} onCambiar={(v) => cambiar({ fecha_expedicion: v })} mensaje={mensaje('fecha_expedicion')} />
              <CampoFechaFicha etiqueta={t('ficha.legal.fechaNacimiento')} valor={p.fecha_nacimiento} onCambiar={(v) => cambiar({ fecha_nacimiento: v })} mensaje={mensaje('fecha_nacimiento')} />
              <SelectorUnico etiqueta={t('ficha.legal.nacionalidad')} opciones={PAISES} valor={p.nacionalidad} onCambiar={(v) => cambiar({ nacionalidad: v })} mensaje={mensaje('nacionalidad')} />
              <SelectorUnico etiqueta={t('ficha.legal.residencia')} opciones={PAISES} valor={p.residencia} onCambiar={(v) => cambiar({ residencia: v })} mensaje={mensaje('residencia')} />
              <CampoTexto etiqueta={t('ficha.legal.correo')} valor={p.correo} onCambiar={(v) => cambiar({ correo: v })} mensaje={mensaje('correo')} tipo="email" inputMode="email" maxLength={254} />
              <CampoTexto etiqueta={t('ficha.legal.telefono')} valor={p.telefono} onCambiar={(v) => cambiar({ telefono: v })} mensaje={mensaje('telefono')} tipo="tel" inputMode="tel" maxLength={25} />
            </Rejilla>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained">
            {t('ficha.legal.listo')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
