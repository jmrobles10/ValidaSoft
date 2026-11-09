import { Box, Button } from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { CampoIa, ValorIa } from './revisionIa';

/** Un valor: texto tal cual; una lista, como lista con viñetas; un objeto, como sus pares «Etiqueta: valor». */
function Valor({ valor }: { valor: ValorIa }) {
  if (valor.tipo === 'texto') return <>{valor.texto}</>;
  if (valor.tipo === 'lista') {
    return (
      <Box component="ul" sx={{ m: 0, pl: 2 }}>
        {valor.elementos.map((elemento, i) => (
          <li key={i}>
            <Valor valor={elemento} />
          </li>
        ))}
      </Box>
    );
  }
  return (
    <Box component="span" sx={{ display: 'block' }}>
      {valor.pares.map((par) => (
        <Box component="span" key={par.clave} sx={{ display: 'block' }}>
          <Box component="span" sx={{ color: 'text.secondary' }}>
            {par.etiqueta}:
          </Box>{' '}
          <Valor valor={par.valor} />
        </Box>
      ))}
    </Box>
  );
}

/** Datos que extrajo la IA: lista de definiciones en dos columnas, con «Ver n datos más» si hay límite. */
export function CamposIa({ campos, titulo, limite }: { campos: readonly CampoIa[]; titulo: string; limite?: number }) {
  const { t } = useTranslation();
  const [todos, setTodos] = useState(false);
  const visibles = limite && !todos ? campos.slice(0, limite) : campos;
  const ocultos = campos.length - visibles.length;

  return (
    <Box>
      <Box
        component="dl"
        aria-label={titulo}
        sx={{ m: 0, display: 'grid', gridTemplateColumns: 'minmax(0, max-content) minmax(0, 1fr)', columnGap: 1.5, rowGap: 0.5, fontSize: 13, lineHeight: '18px' }}
      >
        {visibles.map((campo) => (
          <Box key={campo.clave} sx={{ display: 'contents' }}>
            <Box component="dt" sx={{ color: 'text.secondary' }}>
              {campo.etiqueta}
            </Box>
            <Box component="dd" sx={{ m: 0, overflowWrap: 'anywhere' }}>
              <Valor valor={campo.valor} />
            </Box>
          </Box>
        ))}
      </Box>
      {limite !== undefined && campos.length > limite && (
        <Button size="small" variant="text" onClick={() => setTodos((v) => !v)} sx={{ mt: 0.5, px: 0.5, minWidth: 0 }} aria-expanded={todos}>
          {todos ? t('ia.revision.verMenos') : t('ia.revision.verTodos', { count: ocultos })}
        </Button>
      )}
    </Box>
  );
}
