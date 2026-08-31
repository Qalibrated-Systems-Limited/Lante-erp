using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class TokenRepository(LanteUserServiceDbContext context)
    : GenericRepository<PersonalAccessToken>(context), ITokenRepository
{
    public async Task<PersonalAccessToken?> GetTokenByJtiAsync(string jti)
    {
        return await Context.PersonalAccessTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Jti == jti);
    }

    public async Task<PersonalAccessToken?> GetTokenByUserIdAsync(Guid userId)
    {
        var userIdStr = userId.ToString();
        return await Context.PersonalAccessTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userIdStr && !t.IsRevoked)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> DeleteAllTokensForUserAsync(Guid userId)
    {
        var userIdStr = userId.ToString();
        var tokens = await Context.PersonalAccessTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userIdStr)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.UpdatedAt = DateTime.UtcNow;
        }

        await Context.SaveChangesAsync();
        return true;
    }
}
