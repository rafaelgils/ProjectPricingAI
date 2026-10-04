using MongoDB.Driver;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Infraestrutura.Repositorios;

public sealed class MaterialRepository(IMongoCollection<Material> colecao)
    : RepositorioMongo<Material>(colecao, m => m.Id), IMaterialRepository;
