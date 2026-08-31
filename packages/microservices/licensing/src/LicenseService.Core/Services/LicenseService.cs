using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using LicenseService.Core.DTOs.Licenses;
using LicenseService.Core.Entities;
using LicenseService.Core.Interfaces.Repositories;
using LicenseService.Core.Interfaces.Services;

namespace LicenseService.Core.Services;

public class LicenseService(
    ILicenseRepository repository,
    IConfiguration configuration,
    ILogger<LicenseService> logger)
    : ILicenseService
{
    // ── Private key (loaded once from config) ─────────────────────────────────
    private ECDsa? _privateKey;

    private ECDsa GetPrivateKey()
    {
        if (_privateKey is not null) return _privateKey;

        var jwkJson = configuration["License:PrivateKeyJwk"]
            ?? throw new InvalidOperationException(
                "License:PrivateKeyJwk is not configured. " +
                "Add it to your environment or appsettings.Production.json.");

        var jwk = new JsonWebKey(jwkJson);

        _privateKey = ECDsa.Create();
        _privateKey.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64UrlEncoder.DecodeBytes(jwk.X),
                Y = Base64UrlEncoder.DecodeBytes(jwk.Y),
            },
            D = Base64UrlEncoder.DecodeBytes(jwk.D!),
        });

        return _privateKey;
    }

    // ── Issue ─────────────────────────────────────────────────────────────────
    public async Task<IssueLicenseResponseDto> IssueAsync(IssueLicenseDto dto, string issuedBy)
    {
        var key   = new ECDsaSecurityKey(GetPrivateKey());
        var creds = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var now    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, dto.CustomerId),
            new(JwtRegisteredClaimNames.Iss, "qalibrated.co.ke"),
            new(JwtRegisteredClaimNames.Iat, now.ToString(), ClaimValueTypes.Integer64),
            new("app",      dto.AppId),
            new("features", JsonSerializer.Serialize(dto.Features), JsonClaimValueTypes.JsonArray),
        };

        if (!string.IsNullOrWhiteSpace(dto.MachineId))
            claims.Add(new("mid", dto.MachineId));

        var jwt = new JwtSecurityToken(
            issuer:             "qalibrated.co.ke",
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            dto.ExpiresAt.ToUniversalTime(),
            signingCredentials: creds
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        var license = new License
        {
            Token        = token,
            CustomerId   = dto.CustomerId,
            CustomerName = dto.CustomerName,
            AppId        = dto.AppId,
            Features     = string.Join(',', dto.Features),
            MachineId    = dto.MachineId,
            IssuedAt     = DateTime.UtcNow,
            ExpiresAt    = dto.ExpiresAt.ToUniversalTime(),
            Notes        = dto.Notes,
            CreatedBy    = issuedBy,
        };

        await repository.CreateAsync(license);

        logger.LogInformation(
            "License issued: customer={CustomerId} app={AppId} features=[{Features}] expires={ExpiresAt} by={IssuedBy}",
            dto.CustomerId, dto.AppId, string.Join(',', dto.Features), dto.ExpiresAt, issuedBy);

        return new IssueLicenseResponseDto
        {
            Id         = license.Id,
            Token      = token,
            CustomerId = license.CustomerId,
            AppId      = license.AppId,
            Features   = license.GetFeatures(),
            ExpiresAt  = license.ExpiresAt,
        };
    }

    // ── Validate (called by client apps on activation + daily check-in) ───────
    public async Task<ValidateLicenseResponseDto> ValidateAsync(ValidateLicenseDto dto)
    {
        var record = await repository.GetByTokenAsync(dto.Token);

        if (record is null)
        {
            logger.LogWarning("Validation failed: unknown token. machineId={MachineId} appId={AppId}",
                dto.MachineId, dto.AppId);
            return Fail("unknown_key");
        }

        if (record.Revoked)
        {
            logger.LogWarning("Validation failed: revoked. licenseId={Id} customer={CustomerId}",
                record.Id, record.CustomerId);
            return Fail("revoked");
        }

        if (record.AppId != dto.AppId)
        {
            logger.LogWarning("Validation failed: wrong_app. expected={Expected} got={Got}",
                record.AppId, dto.AppId);
            return Fail("wrong_app");
        }

        if (record.ExpiresAt < DateTime.UtcNow)
        {
            logger.LogWarning("Validation failed: expired. licenseId={Id} expiredAt={ExpiresAt}",
                record.Id, record.ExpiresAt);
            return Fail("expired");
        }

        if (record.MachineId is not null && record.MachineId != dto.MachineId)
        {
            logger.LogWarning("Validation failed: machine_mismatch. bound={Bound} got={Got}",
                record.MachineId, dto.MachineId);
            return Fail("machine_mismatch");
        }

        // Update check-in audit trail
        record.LastSeen      = DateTime.UtcNow;
        record.LastMachineId = dto.MachineId;
        await repository.UpdateAsync(record);

        logger.LogInformation("License validated: customer={CustomerId} app={AppId} machine={MachineId}",
            record.CustomerId, record.AppId, dto.MachineId);

        return new ValidateLicenseResponseDto
        {
            Valid        = true,
            CustomerId   = record.CustomerId,
            AppId        = record.AppId,
            Features     = record.GetFeatures(),
            ExpiresAt    = record.ExpiresAt,
            ServerChecked = true,
        };
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────
    public async Task<LicenseReadDto?> GetByIdAsync(string id)
    {
        var record = await repository.GetByIdAsync(id);
        return record is null ? null : ToReadDto(record);
    }

    // ── List with filters ─────────────────────────────────────────────────────
    public async Task<IEnumerable<LicenseReadDto>> GetAllAsync(string? customerId, string? appId, bool? active)
    {
        var records = await repository.GetAllWithFiltersAsync(customerId, appId, active);
        return records.Select(ToReadDto);
    }

    // ── Revoke ────────────────────────────────────────────────────────────────
    public async Task RevokeAsync(string id, string reason, string revokedBy)
    {
        var record = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"License {id} not found.");

        // Atomic — touches only Revoked/RevokeReason, so a concurrent RenewAsync's ExpiresAt
        // change can't be lost to a full-record overwrite (see ILicenseRepository.TryRevokeAsync).
        if (!await repository.TryRevokeAsync(id, reason, revokedBy))
            throw new KeyNotFoundException($"License {id} not found.");

        logger.LogInformation("License revoked: id={Id} customer={CustomerId} reason={Reason} by={RevokedBy}",
            id, record.CustomerId, reason, revokedBy);
    }

    // ── Renew ─────────────────────────────────────────────────────────────────
    public async Task<LicenseReadDto> RenewAsync(string id, DateTime newExpiresAt, string renewedBy)
    {
        var record = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"License {id} not found.");

        if (record.Revoked)
            throw new InvalidOperationException("Cannot renew a revoked license.");

        var newExpiresAtUtc = newExpiresAt.ToUniversalTime();
        if (newExpiresAtUtc <= record.ExpiresAt)
            throw new InvalidOperationException(
                $"New expiry ({newExpiresAtUtc:yyyy-MM-dd}) must be after the current expiry ({record.ExpiresAt:yyyy-MM-dd}).");

        var previousExpiresAt = record.ExpiresAt;

        // Atomic — touches only ExpiresAt, so a concurrent RevokeAsync can't be lost to a
        // full-record overwrite; also re-checks Revoked/ExpiresAt server-side so a state change
        // between the reads above and now (a concurrent revoke, or another renewal already past
        // this one) fails cleanly instead of silently reapplying a stale decision.
        if (!await repository.TryRenewAsync(id, newExpiresAtUtc, renewedBy))
            throw new InvalidOperationException(
                "License could not be renewed — it may have just been revoked or already renewed past this date.");

        logger.LogInformation(
            "License renewed: id={Id} customer={CustomerId} previousExpiresAt={PreviousExpiresAt} newExpiresAt={NewExpiresAt} by={RenewedBy}",
            id, record.CustomerId, previousExpiresAt, newExpiresAtUtc, renewedBy);

        // record itself was never persisted (TryRenewAsync wrote straight to the DB) — reflect the
        // new value here purely so the returned DTO matches what's now actually stored.
        record.ExpiresAt = newExpiresAtUtc;
        record.UpdatedBy = renewedBy;
        return ToReadDto(record);
    }

    // ── Expiring soon ─────────────────────────────────────────────────────────
    public async Task<IEnumerable<LicenseReadDto>> GetExpiringAsync(int withinDays = 30)
    {
        var records = await repository.GetExpiringAsync(withinDays);
        return records.Select(ToReadDto);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static ValidateLicenseResponseDto Fail(string reason) =>
        new() { Valid = false, Reason = reason, ServerChecked = true };

    private static LicenseReadDto ToReadDto(License r) => new()
    {
        Id             = r.Id,
        CustomerId     = r.CustomerId,
        CustomerName   = r.CustomerName,
        AppId          = r.AppId,
        Features       = r.GetFeatures(),
        MachineId      = r.MachineId,
        IssuedAt       = r.IssuedAt,
        ExpiresAt      = r.ExpiresAt,
        Revoked        = r.Revoked,
        RevokeReason   = r.RevokeReason,
        LastSeen       = r.LastSeen,
        LastMachineId  = r.LastMachineId,
        Notes          = r.Notes,
        IsActive       = r.IsActive,
        IsExpired      = r.IsExpired,
        DaysUntilExpiry = r.IsExpired ? 0 : (int)Math.Ceiling((r.ExpiresAt - DateTime.UtcNow).TotalDays),
        Token          = r.Token,
    };
}
