namespace BC_CampusLearn.Authentication;

public interface IEntraIdentityProtector
{
    ProtectedEntraIdentity Protect(string tenantId, string objectId);

    string CreateLookupHash(string tenantId, string objectId);
}

public sealed record ProtectedEntraIdentity(
    string TenantId,
    string ObjectId,
    string LookupHash);
