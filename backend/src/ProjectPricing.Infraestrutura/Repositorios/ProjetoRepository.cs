using MongoDB.Driver;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Infraestrutura.Repositorios;

public sealed class ProjetoRepository(IMongoCollection<Projeto> colecao)
    : RepositorioMongo<Projeto>(colecao, p => p.Id), IProjetoRepository;
