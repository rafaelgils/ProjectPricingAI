// Contratos da API (architecture.md §4, business-rules.md §8). Códigos iguais aos da documentação.

export type Papel = 'admin' | 'cliente-interno' | 'cliente-externo';
export type TipoMaterial = 'material' | 'servico';
export type UnidadeMedida = 'm' | 'm2' | 'un' | 'L' | 'h';
export type StatusMaterial = 'ativo' | 'inativo';
export type StatusProjeto = 'rascunho' | 'cotado' | 'arquivado';
export type PapelMensagem = 'usuario' | 'assistente';

export interface Perfil {
  keycloakId: string;
  nome: string;
  email: string | null;
  papeis: Papel[];
}

export interface Pagina<T> {
  itens: T[];
  pagina: number;
  tamanho: number;
  total: number;
}

export interface Material {
  id: string;
  nome: string;
  sinonimos: string[];
  tipo: TipoMaterial;
  categoria: string;
  unidade: UnidadeMedida;
  precoUnitario: number;
  moeda: string;
  fornecedor: string;
  status: StatusMaterial;
  atualizadoEm: string;
}

export interface DadosMaterial {
  nome: string;
  sinonimos: string[];
  tipo: TipoMaterial;
  categoria: string;
  unidade: UnidadeMedida;
  precoUnitario: number;
  fornecedor: string;
}

export interface ItemProjeto {
  materialId: string;
  nome: string;
  quantidade: number;
  unidade: UnidadeMedida;
  precoUnitario: number;
  subtotal: number;
}

export interface Projeto {
  id: string;
  descricao: string;
  status: StatusProjeto;
  itens: ItemProjeto[];
  valorTotal: number | null;
  moeda: string;
  criadoEm: string;
  alteradoEm: string;
}

export type ProjetoResumo = Omit<Projeto, 'itens'>;

export interface Mensagem {
  papel: PapelMensagem;
  conteudo: string;
  enviadaEm: string;
}

export interface SugestaoMaterial {
  termo: string;
  materialId: string;
  nome: string;
  origem: 'similaridade' | 'agente';
  similaridade: number | null;
}

/** Problem Details (RFC 7807) com o code estável da API (standards.md §5). */
export interface Problema {
  type?: string;
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
  itensNaoEncontrados?: string[];
  pergunta?: string;
  sugestoes?: SugestaoMaterial[];
  projetoId?: string;
  termoDuplicado?: string;
}

/** Eventos SSE da cotação e do refinamento (standards.md §5). */
export type EventoConversa =
  | { tipo: 'delta'; dados: { texto: string } }
  | { tipo: 'cotacao'; dados: Projeto }
  | { tipo: 'erro'; dados: Problema }
  | { tipo: 'fim'; dados: { projetoId: string; status: StatusProjeto } };

/** Usuário gerenciado pelo Admin (RF10); a fonte é o Keycloak (ADR-004). */
export interface Usuario {
  id: string;
  usuario: string;
  nome: string;
  sobrenome: string;
  email: string | null;
  papel: Papel | null;
  ativo: boolean;
  criadoEm: string;
}

export interface DadosNovoUsuario {
  usuario: string;
  nome: string;
  sobrenome: string;
  email: string;
  papel: Papel;
  senhaTemporaria: string;
}

export interface DadosAlteracaoUsuario {
  nome: string;
  sobrenome: string;
  email: string;
  papel: Papel;
  ativo: boolean;
}
