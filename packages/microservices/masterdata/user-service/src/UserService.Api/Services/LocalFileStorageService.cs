namespace UserService.Api.Services;

public class LocalFileStorageService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
{
    private static readonly HashSet<string> AllowedExtensions = [".png", ".jpg", ".jpeg", ".svg"];

    private readonly string _basePath = config["Storage:BasePath"] ?? "/app/uploads";
    private readonly string _uploadsPath = "/uploads";
    private readonly string? _publicBaseUrl = config["Storage:BaseUrl"] is { } u && u.StartsWith("http") ? u.TrimEnd('/') : null;

    private string BaseUrl
    {
        get
        {
            if (_publicBaseUrl != null)
                return _publicBaseUrl + _uploadsPath;
            var req = httpContextAccessor.HttpContext?.Request;
            if (req != null)
                return $"{req.Scheme}://{req.Host}{_uploadsPath}";
            return _uploadsPath;
        }
    }

    public IFormFile? ResolveFile(IFormFile? named, string? fieldName = null)
    {
        if (named != null) return named;
        var files = httpContextAccessor.HttpContext?.Request.Form.Files;
        if (files == null) return null;
        return fieldName != null ? files.GetFile(fieldName) : files.FirstOrDefault();
    }

    public async Task<string> SaveAsync(IFormFile file, string folder)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not allowed. Accepted: png, jpg, jpeg, svg.");

        var dir = Path.Combine(_basePath, folder);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"{BaseUrl.TrimEnd('/')}/{folder.Replace('\\', '/')}/{fileName}";
    }

    public void Delete(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        var uploadsMarker = $"{_uploadsPath}/";
        var idx = url.IndexOf(uploadsMarker, StringComparison.Ordinal);
        if (idx < 0) return;
        var relative = url[(idx + uploadsMarker.Length)..];
        var fullPath = Path.Combine(_basePath, relative);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
