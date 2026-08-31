using System.Linq.Expressions;
using HSEService.Core.DTOs.Common;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Tests;

/// <summary>
/// An in-memory <see cref="IHseCrudService{T}"/> holding a fixed list.
///
/// <para>Only <c>FindAsync</c> is exercised — it is all <c>HseDashboardService</c> calls. The write
/// members throw rather than returning a default, so a test that starts depending on them fails
/// loudly instead of silently asserting against a fake that quietly did nothing.</para>
/// </summary>
public sealed class FakeHseCrudService<T>(params T[] rows) : IHseCrudService<T> where T : BaseEntity
{
    private readonly List<T> _rows = rows.ToList();

    public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        Task.FromResult(_rows.Where(predicate.Compile()).ToList());

    public Task<IEnumerable<T>> GetAllAsync() => Task.FromResult<IEnumerable<T>>(_rows);

    public Task<T?> GetByIdAsync(string id) => throw new NotSupportedException("Not used by the dashboard.");
    public Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters) => throw new NotSupportedException();
    public Task<T> CreateAsync(T entity) => throw new NotSupportedException();
    public Task<T> UpdateAsync(T entity) => throw new NotSupportedException();
    public Task<bool> DeleteAsync(string id) => throw new NotSupportedException();
}
