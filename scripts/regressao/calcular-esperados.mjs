#!/usr/bin/env node
// Calcula os valores esperados dos casos cotados da suíte de regressão (plano, F11), sem usar o backend:
// conversão da RN05 e arredondamento da RN08 refeitos aqui, com frações exatas (BigInt).
//
// Uso:
//   node scripts/regressao/calcular-esperados.mjs            confere os valores gravados nos casos
//   node scripts/regressao/calcular-esperados.mjs --gravar   preenche os valores que faltam
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const arquivoCatalogo = join(raiz, 'infra', 'mongodb', 'referencia', 'catalogo-referencia.json');
const arquivoCasos = join(raiz, 'backend', 'tests', 'ProjectPricing.Aplicacao.Tests', 'Regressao', 'casos-referencia.json');
const gravar = process.argv.includes('--gravar');

// ---- Frações exatas ----
const mdc = (a, b) => (b === 0n ? a : mdc(b, a % b));
const fracao = (n, d = 1n) => {
  const g = mdc(n < 0n ? -n : n, d) || 1n;
  return { n: n / g, d: d / g };
};
const deDecimal = (valor) => {
  const [inteiro, decimais = ''] = String(valor).split('.');
  return fracao(BigInt(inteiro + decimais), 10n ** BigInt(decimais.length));
};
const vezes = (a, b) => fracao(a.n * b.n, a.d * b.d);
const mais = (a, b) => fracao(a.n * b.d + b.n * a.d, a.d * b.d);

// RN08: arredondamento comum para 3 casas (4ª casa >= 5 sobe), devolvido em milésimos.
const tresCasas = ({ n, d }) => (n * 1000n * 2n + d) / (2n * d);
// RN08: de 3 para 2 casas, sobe só se a 3ª casa for 6 a 9. Devolve fração com 2 casas.
const duasCasas = (valor) => {
  const milesimos = tresCasas(valor);
  let centavos = milesimos / 10n;
  if (milesimos % 10n >= 6n) centavos += 1n;
  return fracao(centavos, 100n);
};
const texto = ({ n, d }) => Number(n) / Number(d);

// ---- RN05 ----
const LINEARES = { mm: '0.001', cm: '0.01', m: '1' };
const FATORES = {
  m: LINEARES,
  m2: { mm2: '0.000001', cm2: '0.0001', m2: '1' },
  L: { ml: '0.001', l: '1' },
  h: { min: fracao(1n, 60n), h: '1' },
  un: { un: '1' },
};
class MedidaInvalida extends Error {}

function converter(item, unidadeMaterial) {
  const fator = (tabela, unidade) => {
    const f = tabela[unidade.trim().toLowerCase().replace('²', '2')];
    if (f === undefined) throw new MedidaInvalida(`${unidade} → ${unidadeMaterial}`);
    return typeof f === 'string' ? deDecimal(f) : f;
  };
  const temDimensoes = item.largura != null || item.altura != null;
  if (temDimensoes) {
    if (unidadeMaterial !== 'm2') throw new MedidaInvalida('dimensões fora de m²');
    const f = fator(LINEARES, item.unidade);
    const pecas = item.quantidade == null ? fracao(1n) : deDecimal(item.quantidade);
    return vezes(vezes(vezes(deDecimal(item.largura), f), vezes(deDecimal(item.altura), f)), pecas);
  }
  if (!(item.quantidade > 0)) throw new MedidaInvalida('quantidade');
  return vezes(deDecimal(item.quantidade), fator(FATORES[unidadeMaterial], item.unidade));
}

// ---- Casos ----
const catalogo = JSON.parse(readFileSync(arquivoCatalogo, 'utf8')).materiais;
const dados = JSON.parse(readFileSync(arquivoCasos, 'utf8'));
const porTermo = new Map();
for (const m of catalogo) {
  for (const t of [m.nome, ...m.sinonimos]) porTermo.set(normalizar(t), m.chave);
}
function normalizar(t) {
  return t.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().trim().replace(/\s+/g, ' ');
}

let divergencias = 0;
let preenchidos = 0;
for (const caso of dados.casos) {
  const precos = Object.fromEntries(catalogo.map((m) => [m.chave, m.precoUnitario]));
  const congelados = new Map(); // RN02: material já cotado no projeto → preço congelado
  const sugeridos = new Map(); // termo → material oferecido na confirmação
  for (const [indice, rodada] of caso.rodadas.entries()) {
    Object.assign(precos, rodada.precosAtualizados ?? {});
    for (const s of rodada.esperado.sugestoes ?? []) sugeridos.set(s.termo, s.material);
    if (rodada.esperado.resultado !== 'cotado') continue;

    const itens = rodada.llm.itens.map((item) => {
      const chave = item.materialId ?? porTermo.get(normalizar(item.termo)) ?? sugeridos.get(item.termo);
      if (!chave) throw new Error(`${caso.id}: termo sem material exato: ${item.termo}`);
      const material = catalogo.find((m) => m.chave === chave);
      const preco = congelados.get(chave) ?? precos[chave];
      const quantidade = duasCasas(converter(item, material.unidade));
      const subtotal = duasCasas(vezes(quantidade, deDecimal(preco)));
      return { material: chave, quantidade: texto(quantidade), precoUnitario: preco, subtotal: texto(subtotal), fracaoSubtotal: subtotal };
    });
    const total = texto(itens.reduce((soma, i) => mais(soma, i.fracaoSubtotal), fracao(0n)));
    itens.forEach((i) => {
      delete i.fracaoSubtotal;
      congelados.set(i.material, i.precoUnitario);
    });
    const esperado = { resultado: 'cotado', itens, total };

    const rotulo = `${caso.id} rodada ${indice + 1}`;
    if (rodada.esperado.itens === undefined) {
      preenchidos++;
      if (gravar) rodada.esperado = esperado;
      else console.log(`${rotulo}: sem valores (rode com --gravar)`);
    } else if (JSON.stringify(rodada.esperado) !== JSON.stringify(esperado)) {
      divergencias++;
      console.log(`${rotulo}: DIVERGE\n  gravado:   ${JSON.stringify(rodada.esperado)}\n  calculado: ${JSON.stringify(esperado)}`);
    }
  }
}

if (gravar && preenchidos > 0) {
  writeFileSync(arquivoCasos, JSON.stringify(dados, null, 2) + '\n');
  console.log(`${preenchidos} rodadas preenchidas.`);
}
if (divergencias > 0 || (!gravar && preenchidos > 0)) process.exit(1);
console.log(`Valores esperados conferidos: ${dados.casos.length} casos.`);
