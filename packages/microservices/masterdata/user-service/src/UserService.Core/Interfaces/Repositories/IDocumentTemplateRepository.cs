using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IDocumentTemplateRepository : IGenericRepository<DocumentTemplate>
{
    Task<DocumentTemplate?> GetByDocTypeAsync(string docType);
}
