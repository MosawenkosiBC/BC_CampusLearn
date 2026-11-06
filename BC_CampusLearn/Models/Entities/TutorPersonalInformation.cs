namespace BC_CampusLearn.Models.Entities;

public class TutorPersonalInformation
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string Surname { get; set; } = null!;
    public string FullNames { get; set; } = null!;
    public string PhysicalHomeAddress { get; set; } = null!;
    public string StreetAddress { get; set; } = null!;
    public string Suburb { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Province { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string IncomeTaxNumber { get; set; } = null!;
    public string HomeLanguage { get; set; } = null!;
    public string Gender { get; set; } = null!;
    public string Nationality { get; set; } = null!;
    public string IdentityOrPassportNumber { get; set; } = null!;
    public DateOnly? WorkPermitExpiryDate { get; set; }
    public string? AsylumNumber { get; set; }
    public DateOnly BirthDate { get; set; }
    public string EmergencyContactName { get; set; } = null!;
    public string EmergencyContactPrimaryPhone { get; set; } = null!;
    public string EmergencyContactRelationship { get; set; } = null!;
    public string? SignatureName { get; set; }
    public byte[]? SignatureImage { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Tutor Tutor { get; set; } = null!;
}
