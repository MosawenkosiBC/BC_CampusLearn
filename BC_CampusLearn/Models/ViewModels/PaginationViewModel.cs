namespace BC_CampusLearn.Models.ViewModels;

public sealed class PaginationViewModel
{
    public required string TargetId { get; init; }

    public required string AriaLabel { get; init; }

    public int MobilePageSize { get; init; }

    public int DefaultPageSize { get; init; }
}
