using AutoMapper;
using Moq;
using UserService.Core.DTOs.Departments;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Services;
using Xunit;

namespace UserService.Tests.Services;

public class DepartmentServiceTests
{
    private readonly Mock<IDepartmentRepository> _repo = new();
    private readonly Mock<IMapper> _mapper = new();

    private DepartmentService CreateSut() => new(_repo.Object, _mapper.Object);

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        var depts = new List<Department> { new() { Id = Guid.NewGuid().ToString(), Name = "Engineering" } };
        var dtos = new List<DepartmentReadDto> { new() { Name = "Engineering" } };

        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(depts);
        _mapper.Setup(m => m.Map<IEnumerable<DepartmentReadDto>>(depts)).Returns(dtos);

        var result = await CreateSut().GetAllAsync();

        Assert.Single(result);
        Assert.Equal("Engineering", result.First().Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<string>(), false)).ReturnsAsync((Department?)null);

        var result = await CreateSut().GetByIdAsync("missing-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDto_WhenFound()
    {
        var dept = new Department { Id = Guid.NewGuid().ToString(), Name = "Finance" };
        var dto = new DepartmentReadDto { Name = "Finance" };

        _repo.Setup(r => r.GetByIdAsync(dept.Id.ToString(), false)).ReturnsAsync(dept);
        _mapper.Setup(m => m.Map<DepartmentReadDto>(dept)).Returns(dto);

        var result = await CreateSut().GetByIdAsync(dept.Id.ToString());

        Assert.NotNull(result);
        Assert.Equal("Finance", result.Name);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenDepartmentNameExists()
    {
        var existing = new Department { Id = Guid.NewGuid().ToString(), Name = "HR" };
        _repo.Setup(r => r.GetByNameAsync("HR")).ReturnsAsync(existing);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().CreateAsync(new CreateDepartmentDto { Name = "HR" }));
    }

    [Fact]
    public async Task CreateAsync_CreatesDepartment_WhenNameIsUnique()
    {
        var dto = new CreateDepartmentDto { Name = "Legal", Description = "Legal dept", IsActive = true };
        var created = new Department { Id = Guid.NewGuid().ToString(), Name = dto.Name };
        var readDto = new DepartmentReadDto { Name = dto.Name };

        _repo.Setup(r => r.GetByNameAsync(dto.Name)).ReturnsAsync((Department?)null);
        _repo.Setup(r => r.CreateAsync(It.IsAny<Department>())).ReturnsAsync((Department d) => d);
        _mapper.Setup(m => m.Map<DepartmentReadDto>(It.IsAny<Department>())).Returns(readDto);

        var result = await CreateSut().CreateAsync(dto);

        Assert.NotNull(result);
        _repo.Verify(r => r.CreateAsync(It.Is<Department>(d =>
            d.Name == dto.Name && d.IsActive == dto.IsActive
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenDepartmentNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<string>(), false)).ReturnsAsync((Department?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateSut().UpdateAsync("missing", new UpdateDepartmentDto { Name = "New" }));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_WhenFound()
    {
        var dept = new Department { Id = Guid.NewGuid().ToString(), Name = "Old Name", IsActive = true };
        var dto = new UpdateDepartmentDto { Name = "New Name", IsActive = false };
        var readDto = new DepartmentReadDto { Name = "New Name" };

        _repo.Setup(r => r.GetByIdAsync(dept.Id.ToString(), false)).ReturnsAsync(dept);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Department>())).ReturnsAsync((Department d) => d);
        _mapper.Setup(m => m.Map<DepartmentReadDto>(It.IsAny<Department>())).Returns(readDto);

        var result = await CreateSut().UpdateAsync(dept.Id.ToString(), dto);

        Assert.NotNull(result);
        _repo.Verify(r => r.UpdateAsync(It.Is<Department>(d =>
            d.Name == "New Name" && d.IsActive == false
        )), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepository()
    {
        _repo.Setup(r => r.DeleteAsync("dept-id")).ReturnsAsync(true);

        var result = await CreateSut().DeleteAsync("dept-id");

        Assert.True(result);
    }
}
