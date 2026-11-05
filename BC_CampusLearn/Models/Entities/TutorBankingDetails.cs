namespace BC_CampusLearn.Models.Entities;

public class TutorBankingDetails
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string AccountHolder { get; set; } = null!;
    public string RecipientAccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public string BranchCode { get; set; } = null!;
    public string BankName { get; set; } = null!;
    public Tutor Tutor { get; set; } = null!;
}
