using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BC_CampusLearn.ViewComponents;

public sealed class PaginationViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string targetId,
        int mobilePageSize = 7,
        int defaultPageSize = 11,
        string ariaLabel = "Pagination")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        if (mobilePageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mobilePageSize));
        }

        if (defaultPageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPageSize));
        }

        return View(
            "~/Pages/Shared/Components/Pagination/Default.cshtml",
            new PaginationViewModel
            {
                TargetId = targetId,
                AriaLabel = string.IsNullOrWhiteSpace(ariaLabel)
                    ? "Pagination"
                    : ariaLabel,
                MobilePageSize = mobilePageSize,
                DefaultPageSize = defaultPageSize
            });
    }
}
