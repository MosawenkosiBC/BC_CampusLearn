namespace BC_CampusLearn.Authentication;

public interface IEntraIdentityProtector
{
    ProtectedEntraIdentity Protect(string tenantId, string objectId);

    EntraIdentity Unprotect(string encryptedTenantId, string encryptedObjectId);

    string CreateLookupHash(string tenantId, string objectId);
}

public sealed record EntraIdentity(string TenantId, string ObjectId);

public sealed record ProtectedEntraIdentity(
    string TenantId,
    string ObjectId,
    string LookupHash);
