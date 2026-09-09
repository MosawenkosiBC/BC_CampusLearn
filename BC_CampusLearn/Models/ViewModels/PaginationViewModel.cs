namespace BC_CampusLearn.Models.ViewModels;

public sealed class PaginationViewModel
{
    public string? TargetId { get; init; }

    public required string AriaLabel { get; init; }

    public int MobilePageSize { get; init; }

    public int DefaultPageSize { get; init; }

    public int? CurrentPage { get; init; }

    public int? TotalPages { get; init; }

    public string PageRouteValueName { get; init; } = "page";

    public string? Fragment { get; init; }

    public bool UsesServerPagination =>
        CurrentPage.HasValue && TotalPages.HasValue;
}
