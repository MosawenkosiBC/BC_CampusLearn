namespace BC_CampusLearn.Authentication;

public sealed class IdentityProtectionOptions
{
    public const string SectionName = "IdentityProtection";

    public string LookupKey { get; set; } = string.Empty;

    public bool TryGetLookupKey(out byte[] key)
    {
        try
        {
            key = Convert.FromBase64String(LookupKey);
            return key.Length >= 32;
        }
        catch (FormatException)
        {
            key = [];
            return false;
        }
    }
}
