namespace BC_CampusLearn.Models.Entities;

public class CampusEventDetail
{
    public int CampusEventDetailId { get; set; }
    public int CampusEventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public CampusEventDetailDataType DataType { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Position { get; set; }

    public CampusEvent CampusEvent { get; set; } = null!;
}
