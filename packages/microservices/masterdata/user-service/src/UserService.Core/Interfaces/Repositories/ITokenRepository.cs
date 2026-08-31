using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface ITokenRepository : IGenericRepository<PersonalAccessToken>
{
    Task<PersonalAccessToken?> GetTokenByJtiAsync(string jti);
    Task<PersonalAccessToken?> GetTokenByUserIdAsync(Guid userId);
    Task<bool> DeleteAllTokensForUserAsync(Guid userId);
}
