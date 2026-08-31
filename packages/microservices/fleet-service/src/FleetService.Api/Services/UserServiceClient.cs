using System.Text.Json;
using FleetService.Core.Interfaces;

namespace FleetService.Api.Services;

public class UserServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<UserServiceClient> logger) : IUserServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<UserContactDto?> GetUserContactAsync(string tenantSchema, string userId)
    {
        var baseUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("UserService:BaseUrl not configured — skipping user lookup for {UserId}", userId);
            return null;
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("InternalServices:ServiceKey not configured — skipping user lookup for {UserId}", userId);
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("UserService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var response = await client.GetAsync($"{baseUrl}/internal/users/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService returned {StatusCode} looking up user {UserId}", response.StatusCode, userId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<UserServiceResponse>(json, JsonOpts);
            if (wrapper?.Data == null) return null;

            return new UserContactDto(wrapper.Data.Id, wrapper.Data.Name, wrapper.Data.Email, wrapper.Data.MobileNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to look up user {UserId} from UserService", userId);
            return null;
        }
    }

    public async Task<IEnumerable<UserContactDto>> GetUsersByPermissionAsync(string tenantSchema, string permission)
    {
        var baseUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("UserService:BaseUrl or InternalServices:ServiceKey not configured — skipping permission lookup for {Permission}", permission);
            return [];
        }

        try
        {
            var client = httpClientFactory.CreateClient("UserService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var response = await client.GetAsync($"{baseUrl}/internal/users/by-permission/{Uri.EscapeDataString(permission)}");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService returned {StatusCode} looking up users with permission {Permission}", response.StatusCode, permission);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<UserServiceListResponse>(json, JsonOpts);
            if (wrapper?.Data == null) return [];

            return wrapper.Data.Select(d => new UserContactDto(d.Id, d.Name, d.Email, d.MobileNumber));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to look up users with permission {Permission} from UserService", permission);
            return [];
        }
    }

    public async Task<IEnumerable<UserContactDto>> GetUsersByRoleNameAsync(string tenantSchema, string roleName)
    {
        var baseUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("UserService:BaseUrl or InternalServices:ServiceKey not configured — skipping role lookup for {RoleName}", roleName);
            return [];
        }

        try
        {
            var client = httpClientFactory.CreateClient("UserService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var response = await client.GetAsync($"{baseUrl}/internal/users/by-role/{Uri.EscapeDataString(roleName)}");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService returned {StatusCode} looking up users with role {RoleName}", response.StatusCode, roleName);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<UserServiceListResponse>(json, JsonOpts);
            if (wrapper?.Data == null) return [];

            return wrapper.Data.Select(d => new UserContactDto(d.Id, d.Name, d.Email, d.MobileNumber));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to look up users with role {RoleName} from UserService", roleName);
            return [];
        }
    }

    private sealed record UserServiceResponse(bool Success, UserContactData? Data);
    private sealed record UserServiceListResponse(bool Success, List<UserContactData>? Data);

    // Wire shape is firstName/lastName (matches user-service's internal/users response) —
    // not a combined "name" field.
    private sealed record UserContactData(string Id, string? FirstName, string? LastName, string? Email, string? MobileNumber)
    {
        public string Name => $"{FirstName} {LastName}".Trim();
    }
}
