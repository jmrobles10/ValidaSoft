import { Stack, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CampoCifra, CampoFechaFicha, CampoSiNo, CampoTexto, Rejilla } from '../campos';
import { PAISES } from '../paises';
import { CampoCatalogo, SelectorUnico } from '../selectores';
import type { DatosGenerales, PropsFormulario } from '../tipos';
import { PreguntaPep } from '../pep/PreguntaPep';
import { DireccionPorPartes } from './DireccionPorPartes';

const MAXIMO_SECUNDARIAS = 3;
const TIPOS_PERSONA = ['Natural', 'Juridica', 'Consorcio'] as const;
const esTipoPersona = (valor: string): valor is (typeof TIPOS_PERSONA)[number] => TIPOS_PERSONA.some((t) => t === valor);

/**
 * Información general (`FICHA_GENERAL`). Tipo de persona, identificación y DV los fija la API con el registro del
 * tercero: aquí solo se muestran. Persona jurídica (o consorcio) diligencia la razón social; natural, nombres y apellidos.
 */
export function FormularioGeneral({ datos, onCambiar, mensaje, hayEnRuta, deshabilitado, tipoPersona }: PropsFormulario<DatosGenerales> & { tipoPersona: string }) {
  const { t } = useTranslation();
  const natural = tipoPersona === 'Natural';
  const identificacion = datos.numero
    ? `${datos.tipo_identificacion ?? ''} ${datos.numero}${datos.dv ? `-${datos.dv}` : ''}`.trim()
    : t('ficha.general.identificacionPendiente');
  const secundarias = datos.ciiu_secundarias ?? [];
  const cambiarSecundarias = (lista: string[]) => onCambiar({ ciiu_secundarias: lista.length > 0 ? lista : null });

  return (
    <Stack spacing={2}>
      <Rejilla>
        <TextField label={t('ficha.general.identificacion')} value={identificacion} helperText={t('ficha.general.identificacionAyuda')} fullWidth slotProps={{ input: { readOnly: true } }} />
        <TextField
          label={t('ficha.general.tipoPersona')}
          value={esTipoPersona(tipoPersona) ? t(`ficha.general.tiposPersona.${tipoPersona}`) : tipoPersona}
          fullWidth
          slotProps={{ input: { readOnly: true } }}
        />
      </Rejilla>
      {natural ? (
        <>
          <Rejilla>
            <CampoTexto etiqueta={t('ficha.general.nombres')} valor={datos.nombres} onCambiar={(v) => onCambiar({ nombres: v })} mensaje={mensaje('nombres')} autoComplete="given-name" maxLength={150} deshabilitado={deshabilitado} />
          <CampoTexto etiqueta={t('ficha.general.apellidos')} valor={datos.apellidos} onCambiar={(v) => onCambiar({ apellidos: v })} mensaje={mensaje('apellidos')} autoComplete="family-name" maxLength={150} deshabilitado={deshabilitado} />
          </Rejilla>
          <Rejilla>
            <CampoFechaFicha etiqueta={t('ficha.general.fechaExpedicion')} valor={datos.fecha_expedicion} onCambiar={(v) => onCambiar({ fecha_expedicion: v })} mensaje={mensaje('fecha_expedicion')} ayuda={t('ficha.general.fechaExpedicionAyuda')} deshabilitado={deshabilitado} />
            <CampoTexto etiqueta={t('ficha.general.lugarExpedicion')} valor={datos.lugar_expedicion} onCambiar={(v) => onCambiar({ lugar_expedicion: v })} mensaje={mensaje('lugar_expedicion')} maxLength={100} deshabilitado={deshabilitado} />
          </Rejilla>
        </>
      ) : (
        <CampoTexto etiqueta={t('ficha.general.razonSocial')} valor={datos.razon_social} onCambiar={(v) => onCambiar({ razon_social: v })} mensaje={mensaje('razon_social')} autoComplete="organization" deshabilitado={deshabilitado} />
      )}
      <Rejilla>
        <CampoTexto etiqueta={t('ficha.general.correo')} valor={datos.correo} onCambiar={(v) => onCambiar({ correo: v })} mensaje={mensaje('correo')} tipo="email" inputMode="email" autoComplete="email" maxLength={254} deshabilitado={deshabilitado} />
        <CampoTexto etiqueta={t('ficha.general.telefono')} valor={datos.telefono} onCambiar={(v) => onCambiar({ telefono: v })} mensaje={mensaje('telefono')} tipo="tel" inputMode="tel" autoComplete="tel" maxLength={25} deshabilitado={deshabilitado} />
        <SelectorUnico
          etiqueta={t('ficha.general.pais')}
          opciones={PAISES}
          valor={datos.pais}
          // La ciudad (DANE) solo aplica en Colombia: al cambiar de país se borra.
          onCambiar={(pais) => onCambiar({ pais, ciudad: pais === 'CO' ? datos.ciudad : null })}
          mensaje={mensaje('pais')}
          deshabilitado={deshabilitado}
        />
        {datos.pais === 'CO' && (
          <CampoCatalogo catalogo="ciudades" etiqueta={t('ficha.general.ciudad')} valor={datos.ciudad} onCambiar={(ciudad) => onCambiar({ ciudad })} mensaje={mensaje('ciudad')} deshabilitado={deshabilitado} />
        )}
      </Rejilla>
      <CampoTexto etiqueta={t('ficha.general.direccion')} valor={datos.direccion} onCambiar={(v) => onCambiar({ direccion: v })} mensaje={mensaje('direccion')} ayuda={t('ficha.general.direccionAyuda')} autoComplete="street-address" deshabilitado={deshabilitado} />
      <DireccionPorPartes valor={datos.direccion_estructurada} onCambiar={(d) => onCambiar({ direccion_estructurada: d })} mensaje={mensaje} deshabilitado={deshabilitado} />
      <CampoCatalogo
        catalogo="actividades-economicas"
        etiqueta={t('ficha.general.ciiuPrincipal')}
        valor={datos.ciiu_principal}
        onCambiar={(ciiu) => onCambiar({ ciiu_principal: ciiu })}
        mensaje={mensaje('ciiu_principal')}
        deshabilitado={deshabilitado}
      />
      {/* Las secundarias ya elegidas y, mientras quepan, un campo vacío para agregar otra: elegir agrega, borrar quita. */}
      {secundarias.map((codigo, i) => (
        <CampoCatalogo
          key={codigo}
          catalogo="actividades-economicas"
          etiqueta={t('ficha.general.ciiuSecundaria', { n: String(i + 1) })}
          valor={codigo}
          onCambiar={(nuevo) => cambiarSecundarias(nuevo ? secundarias.map((c, j) => (j === i ? nuevo : c)) : secundarias.filter((_, j) => j !== i))}
          mensaje={mensaje(`ciiu_secundarias[${String(i)}]`) ?? (i === 0 ? mensaje('ciiu_secundarias') : null)}
          deshabilitado={deshabilitado}
        />
      ))}
      {secundarias.length < MAXIMO_SECUNDARIAS && (
        <CampoCatalogo
          key={`nueva-${String(secundarias.length)}`}
          catalogo="actividades-economicas"
          etiqueta={t('ficha.general.ciiuSecundaria', { n: String(secundarias.length + 1) })}
          valor={null}
          onCambiar={(nuevo) => {
            if (nuevo && !secundarias.includes(nuevo)) cambiarSecundarias([...secundarias, nuevo]);
          }}
          deshabilitado={deshabilitado}
        />
      )}
      <Rejilla>
        <CampoTexto etiqueta={t('ficha.general.paginaWeb')} valor={datos.pagina_web} onCambiar={(v) => onCambiar({ pagina_web: v })} mensaje={mensaje('pagina_web')} tipo="url" inputMode="url" autoComplete="url" deshabilitado={deshabilitado} />
        <CampoCifra etiqueta={t('ficha.general.anioInicio')} valor={datos.anio_inicio} onCambiar={(v) => onCambiar({ anio_inicio: v })} mensaje={mensaje('anio_inicio')} entero deshabilitado={deshabilitado} />
      </Rejilla>
      <CampoSiNo etiqueta={t('ficha.general.certificadoIso')} valor={datos.certificado_iso} onCambiar={(v) => onCambiar({ certificado_iso: v })} mensaje={mensaje('certificado_iso')} deshabilitado={deshabilitado} />
      {natural && (
        <>
          <PreguntaPep
            etiqueta={t('ficha.general.pep')}
            pep={datos.pep}
            declaracion={datos.declaracion_pep}
            origen={{ tipo: 'natural', nombres: datos.nombres, apellidos: datos.apellidos, tipoIdentificacion: datos.tipo_identificacion, numero: datos.numero, direccion: datos.direccion }}
            onCambiar={onCambiar}
            mensaje={mensaje}
            hayEnRuta={hayEnRuta}
            deshabilitado={deshabilitado}
          />
          <CampoSiNo etiqueta={t('ficha.general.vinculoPep')} valor={datos.vinculo_pep} onCambiar={(v) => onCambiar({ vinculo_pep: v })} mensaje={mensaje('vinculo_pep')} deshabilitado={deshabilitado} />
        </>
      )}
    </Stack>
  );
}
