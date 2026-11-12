import { Box, Chip, Stack, TextField, Tooltip, Typography } from '@mui/material';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CAMPOS, insertarVariable, marcador, maximoDe, type BorradorPlantilla, type CampoPlantilla } from './modelo';

const VARIABLES_CONOCIDAS = ['empresa', 'folio', 'tipoVinculo', 'documento', 'fecha'] as const;
type VariableConocida = (typeof VARIABLES_CONOCIDAS)[number];
const esConocida = (variable: string): variable is VariableConocida => (VARIABLES_CONOCIDAS as readonly string[]).includes(variable);

type Entrada = HTMLInputElement | HTMLTextAreaElement;

/**
 * Asunto (una línea), cabecera y pie (multilínea) con contador, y las variables del correo como chips que insertan
 * `{{variable}}` en el último campo que tuvo el foco, donde estaba el cursor.
 */
export function CamposPlantilla({
  asuntoPorDefecto,
  variables,
  borrador,
  errores,
  soloLectura,
  onCambiar,
}: {
  asuntoPorDefecto: string;
  variables: readonly string[];
  borrador: BorradorPlantilla;
  errores: Partial<Record<CampoPlantilla, string>>;
  soloLectura: boolean;
  onCambiar: (campo: CampoPlantilla, valor: string) => void;
}) {
  const { t } = useTranslation();
  const entradas = useRef<Partial<Record<CampoPlantilla, Entrada | null>>>({});
  const [activo, setActivo] = useState<CampoPlantilla>('asunto');

  const insertar = (variable: string) => {
    const entrada = entradas.current[activo];
    const { texto, cursor } = insertarVariable(borrador[activo], variable, entrada?.selectionStart ?? null, entrada?.selectionEnd ?? null);
    onCambiar(activo, texto.slice(0, maximoDe(activo)));
    requestAnimationFrame(() => {
      entrada?.focus();
      entrada?.setSelectionRange(cursor, cursor);
    });
  };

  const ayudas: Record<CampoPlantilla, string> = {
    asunto: t('plantillas.asuntoAyuda', { asunto: asuntoPorDefecto }),
    cabecera: t('plantillas.cabeceraAyuda'),
    pie: t('plantillas.pieAyuda'),
  };

  return (
    <Stack spacing={2}>
      {!soloLectura && variables.length > 0 && (
        <Box>
          <Typography variant="body2" component="h3" sx={{ fontWeight: 500 }}>
            {t('plantillas.variables')}
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1 }}>
            {t('plantillas.variablesAyuda')}
          </Typography>
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', rowGap: 1 }}>
            {variables.map((variable) => (
              <Tooltip key={variable} describeChild title={esConocida(variable) ? t(`plantillas.nombresVariables.${variable}`) : variable}>
                <Chip size="small" variant="outlined" label={marcador(variable)} onClick={() => insertar(variable)} sx={{ fontFamily: 'monospace' }} />
              </Tooltip>
            ))}
          </Stack>
        </Box>
      )}
      {CAMPOS.map((campo) => {
        const maximo = maximoDe(campo);
        const error = errores[campo];
        return (
          <TextField
            key={campo}
            label={t(`plantillas.${campo}`)}
            value={borrador[campo]}
            onChange={(e) => onCambiar(campo, e.target.value.slice(0, maximo))}
            onFocus={() => setActivo(campo)}
            inputRef={(entrada: Entrada | null) => {
              entradas.current[campo] = entrada;
            }}
            disabled={soloLectura}
            multiline={campo !== 'asunto'}
            minRows={campo === 'asunto' ? undefined : 3}
            placeholder={campo === 'asunto' ? asuntoPorDefecto : undefined}
            error={Boolean(error)}
            helperText={
              <Box component="span" sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}>
                <span>{error ?? ayudas[campo]}</span>
                <span aria-hidden>{t('plantillas.contador', { actual: String(borrador[campo].length), maximo: String(maximo) })}</span>
              </Box>
            }
          />
        );
      })}
    </Stack>
  );
}
