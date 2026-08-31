using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class DocumentTemplateRepository(LanteUserServiceDbContext context)
    : GenericRepository<DocumentTemplate>(context), IDocumentTemplateRepository
{
    public async Task<DocumentTemplate?> GetByDocTypeAsync(string docType)
    {
        return await Context.DocumentTemplates.FirstOrDefaultAsync(d => d.DocType == docType);
    }
}
