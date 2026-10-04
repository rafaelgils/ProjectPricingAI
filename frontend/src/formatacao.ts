import type { StatusMaterial, StatusProjeto, UnidadeMedida } from './api/tipos.ts';

// standards.md §4: valores em reais sempre com Intl.NumberFormat pt-BR / BRL.
const formatoMoeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const formatoQuantidade = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});
const formatoData = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

/** Exibe o valor que a API devolveu; o navegador nunca recalcula (RN08). */
export const formatarMoeda = (valor: number): string => formatoMoeda.format(valor);

export const formatarQuantidade = (valor: number): string => formatoQuantidade.format(valor);

export const formatarData = (iso: string): string => formatoData.format(new Date(iso));

const NOMES_UNIDADE: Record<UnidadeMedida, string> = {
  m: 'm',
  m2: 'm²',
  un: 'un',
  L: 'L',
  h: 'h',
};

export const nomeUnidade = (unidade: UnidadeMedida): string => NOMES_UNIDADE[unidade];

const NOMES_STATUS_PROJETO: Record<StatusProjeto, string> = {
  rascunho: 'Rascunho',
  cotado: 'Cotado',
  arquivado: 'Arquivado',
};

export const nomeStatusProjeto = (status: StatusProjeto): string => NOMES_STATUS_PROJETO[status];

export const nomeStatusMaterial = (status: StatusMaterial): string =>
  status === 'ativo' ? 'Ativo' : 'Inativo';
