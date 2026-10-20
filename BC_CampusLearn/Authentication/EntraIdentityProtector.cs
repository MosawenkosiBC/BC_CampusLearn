using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Authentication;

public sealed class EntraIdentityProtector : IEntraIdentityProtector
{
    private const string Purpose =
        "BC_CampusLearn.EntraIdentity.v1";

    public const string SharedAccountEmail = "mzalaconnect@belgiumcampus.ac.za";

    public static bool UsesReadableIdentity(string? email) =>
        string.Equals(email?.Trim(), SharedAccountEmail, StringComparison.OrdinalIgnoreCase);

    private readonly IDataProtector _protector;
    private readonly byte[] _lookupKey;

    public EntraIdentityProtector(
        IDataProtectionProvider provider,
        IOptions<IdentityProtectionOptions> options)
    {
        _protector = provider.CreateProtector(Purpose);
        if (!options.Value.TryGetLookupKey(out _lookupKey))
        {
            throw new InvalidOperationException(
                "IdentityProtection:LookupKey must be a Base64-encoded key of at least 32 bytes.");
        }
    }

    public ProtectedEntraIdentity Protect(string tenantId, string objectId)
    {
        string normalizedTenantId = NormalizeGuid(tenantId, "tenant ID");
        string normalizedObjectId = NormalizeGuid(objectId, "object ID");

        return new ProtectedEntraIdentity(
            _protector.Protect(normalizedTenantId),
            _protector.Protect(normalizedObjectId),
            CreateLookupHash(normalizedTenantId, normalizedObjectId));
    }

    public EntraIdentity Unprotect(string encryptedTenantId, string encryptedObjectId) =>
        new(
            NormalizeGuid(_protector.Unprotect(encryptedTenantId), "tenant ID"),
            NormalizeGuid(_protector.Unprotect(encryptedObjectId), "object ID"));

    // Only this service account stores readable user identity identifiers.
    // Meeting snapshots and every other user's identifiers remain encrypted.
    public ProtectedEntraIdentity ProtectForUser(string tenantId, string objectId, string? email) =>
        UsesReadableIdentity(email)
            ? new(NormalizeGuid(tenantId, "tenant ID"), NormalizeGuid(objectId, "object ID"),
                CreateLookupHash(tenantId, objectId))
            : Protect(tenantId, objectId);

    public EntraIdentity UnprotectForUser(BcUser user) =>
        UsesReadableIdentity(user.Email) &&
        Guid.TryParse(user.EncryptedEntraTenantId, out Guid tenant) &&
        Guid.TryParse(user.EncryptedEntraObjectId, out Guid id)
            ? new(tenant.ToString("D"), id.ToString("D"))
            : Unprotect(user.EncryptedEntraTenantId!, user.EncryptedEntraObjectId!);

    public string CreateLookupHash(string tenantId, string objectId)
    {
        string normalizedTenantId = NormalizeGuid(tenantId, "tenant ID");
        string normalizedObjectId = NormalizeGuid(objectId, "object ID");
        byte[] identityBytes = Encoding.UTF8.GetBytes(
            $"{normalizedTenantId}:{normalizedObjectId}");

        return Convert.ToHexString(HMACSHA256.HashData(
            _lookupKey,
            identityBytes));
    }

    private static string NormalizeGuid(string value, string description)
    {
        if (!Guid.TryParse(value, out Guid parsed))
        {
            throw new InvalidOperationException(
                $"The authenticated principal does not contain a valid Entra {description}.");
        }

        return parsed.ToString("D");
    }
}
