import { Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CampoSiNo, CampoTexto, Rejilla } from '../campos';
import { nombrePais, PAISES } from '../paises';
import { SelectorMultiple, SelectorUnico } from '../selectores';
import type { ConyugePep } from '../tipos';
import { CampoPeriodo, CampoTipoDocumento, SeccionDeclaracion, type PropsSeccion } from './CamposPep';
import { MAXIMO } from './declaracion';
import { FilasFamiliares } from './FilasFamiliares';
import { FilasSociedades } from './FilasSociedades';

/** Sección 2: el cargo, el periodo, las cuentas en el exterior, el cónyuge, los familiares y las sociedades. */
export function SeccionCondicion({ d, cambiar, mensaje, hoy }: PropsSeccion) {
  const { t } = useTranslation();
  const conyuge = d.conyuge ?? {};
  const cambiarConyuge = (cambio: ConyugePep) => cambiar({ conyuge: { ...conyuge, ...cambio } });
  return (
    <SeccionDeclaracion titulo={t('ficha.pep.secciones.condicion')}>
      <Rejilla>
        <CampoTexto etiqueta={t('ficha.pep.condicion.cargo')} valor={d.cargo} onCambiar={(v) => cambiar({ cargo: v })} mensaje={mensaje('cargo')} maxLength={MAXIMO.cargo} />
        <CampoTexto etiqueta={t('ficha.pep.condicion.institucion')} valor={d.institucion} onCambiar={(v) => cambiar({ institucion: v })} mensaje={mensaje('institucion')} maxLength={MAXIMO.institucion} />
        <SelectorUnico etiqueta={t('ficha.pep.condicion.pais')} opciones={PAISES} valor={d.pais} onCambiar={(v) => cambiar({ pais: v })} mensaje={mensaje('pais')} />
      </Rejilla>
      <CampoPeriodo
        valor={d}
        onCambiar={cambiar}
        mensaje={(campo) => mensaje(campo)}
        hoy={hoy}
      />
      <CampoSiNo
        etiqueta={t('ficha.pep.condicion.cuentas')}
        valor={d.cuentas_exterior}
        onCambiar={(v) => cambiar({ cuentas_exterior: v, paises_cuentas_exterior: v ? d.paises_cuentas_exterior : null })}
        mensaje={mensaje('cuentas_exterior')}
      />
      {d.cuentas_exterior === true && (
        <SelectorMultiple
          etiqueta={t('ficha.pep.condicion.paisesCuentas')}
          opciones={PAISES}
          valor={d.paises_cuentas_exterior}
          onCambiar={(v) => cambiar({ paises_cuentas_exterior: v })}
          mensaje={mensaje('paises_cuentas_exterior')}
          etiquetaOpcion={(o) => nombrePais(o.codigo)}
        />
      )}
      <Stack spacing={1.5}>
        <Typography component="h3" sx={{ fontWeight: 500 }}>
          {t('ficha.pep.conyuge.titulo')}
        </Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 14, mt: -1 }}>{t('ficha.pep.conyuge.ayuda')}</Typography>
        <Rejilla>
          <CampoTexto etiqueta={t('ficha.pep.conyuge.nombre')} valor={conyuge.nombre} onCambiar={(v) => cambiarConyuge({ nombre: v })} mensaje={mensaje('conyuge.nombre')} maxLength={MAXIMO.nombre} />
          <CampoTipoDocumento etiqueta={t('ficha.pep.conyuge.tipoDocumento')} valor={conyuge.tipo_identificacion} onCambiar={(v) => cambiarConyuge({ tipo_identificacion: v })} mensaje={mensaje('conyuge.tipo_identificacion')} />
          <CampoTexto etiqueta={t('ficha.pep.conyuge.numero')} valor={conyuge.numero} onCambiar={(v) => cambiarConyuge({ numero: v })} mensaje={mensaje('conyuge.numero')} maxLength={MAXIMO.numero} />
        </Rejilla>
      </Stack>
      <FilasFamiliares filas={d.familiares ?? []} onCambiar={(familiares) => cambiar({ familiares })} mensaje={mensaje} />
      <FilasSociedades filas={d.sociedades ?? []} onCambiar={(sociedades) => cambiar({ sociedades })} mensaje={mensaje} />
    </SeccionDeclaracion>
  );
}
