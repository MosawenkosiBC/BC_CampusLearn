using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Authentication;

public interface IEntraIdentityProtector
{
    ProtectedEntraIdentity Protect(string tenantId, string objectId);

    ProtectedEntraIdentity ProtectForUser(string tenantId, string objectId, string? email) =>
        Protect(tenantId, objectId);

    EntraIdentity UnprotectForUser(BcUser user) =>
        Unprotect(user.EncryptedEntraTenantId!, user.EncryptedEntraObjectId!);

    EntraIdentity Unprotect(string encryptedTenantId, string encryptedObjectId);

    string CreateLookupHash(string tenantId, string objectId);
}

public sealed record EntraIdentity(string TenantId, string ObjectId);

public sealed record ProtectedEntraIdentity(
    string TenantId,
    string ObjectId,
    string LookupHash);
