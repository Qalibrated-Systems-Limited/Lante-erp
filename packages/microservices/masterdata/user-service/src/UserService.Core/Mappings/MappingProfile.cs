using AutoMapper;
using UserService.Core.DTOs.Departments;
using UserService.Core.DTOs.Permissions;
using UserService.Core.DTOs.Roles;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;

namespace UserService.Core.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mappings
        CreateMap<User, UserReadDto>()
            .ForMember(dest => dest.Roles,
                opt => opt.MapFrom(src => src.UserRoles
                    .Where(ur => !ur.IsDeleted && ur.Role != null && !ur.Role.IsDeleted)
                    .Select(ur => ur.Role.Name)
                    .ToList()))
            .ForMember(dest => dest.Permissions,
                opt => opt.MapFrom(src => src.UserRoles
                    .Where(ur => !ur.IsDeleted && ur.Role != null && !ur.Role.IsDeleted)
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Where(rp => rp != null && !rp.IsDeleted && rp.Permission != null && !rp.Permission.IsDeleted)
                    .Select(rp => rp.Permission.Name)
                    // Direct per-user grants (UserPermissions) — mirrors TenantAuthenticator's
                    // rolePerms.Concat(userPerms) so control-plane logins (password + Google) get
                    // the same permission resolution as tenant-schema logins.
                    .Concat(src.UserPermissions
                        .Where(up => !up.IsDeleted && up.Permission != null && !up.Permission.IsDeleted)
                        .Select(up => up.Permission.Name))
                    .Distinct()
                    .ToList()))
            .ForMember(dest => dest.DepartmentName,
                opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : null))
            // Schema-per-tenant (Phase 3): branch/company-admin come from the User itself now
            // (collapsed from UserTenant). BranchName is resolved separately in PopulateTenantContextAsync.
            .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
            .ForMember(dest => dest.IsCompanyAdmin, opt => opt.MapFrom(src => src.IsCompanyAdmin));

        // Role mappings
        CreateMap<Role, RoleReadDto>()
            .ForMember(dest => dest.UserCount, opt => opt.MapFrom(src => src.UserRoles.Count(ur => !ur.IsDeleted)))
            .ForMember(dest => dest.Permissions,
                opt => opt.MapFrom(src => src.RolePermissions
                    .Where(rp => !rp.IsDeleted && rp.Permission != null)
                    .Select(rp => rp.Permission.Name)
                    .ToList()));

        // Permission mappings
        CreateMap<Permission, PermissionReadDto>();

        // Department mappings
        CreateMap<Department, DepartmentReadDto>()
            .ForMember(dest => dest.UserCount, opt => opt.MapFrom(src => src.Users.Count(u => !u.IsDeleted)));
    }
}
