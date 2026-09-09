using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BC_CampusLearn.ViewComponents;

public sealed class PaginationViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string? targetId = null,
        int mobilePageSize = 7,
        int defaultPageSize = 11,
        string ariaLabel = "Pagination",
        int? currentPage = null,
        int? totalPages = null,
        string pageRouteValueName = "page",
        string? fragment = null)
    {
        bool usesServerPagination = currentPage.HasValue || totalPages.HasValue;

        if (usesServerPagination)
        {
            if (!currentPage.HasValue || !totalPages.HasValue)
            {
                throw new ArgumentException(
                    "Current page and total pages must both be supplied for server pagination.");
            }

            if (currentPage < 1 || totalPages < 1 || currentPage > totalPages)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentPage),
                    "Server pagination values must describe a valid page.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(pageRouteValueName);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        }

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
                DefaultPageSize = defaultPageSize,
                CurrentPage = currentPage,
                TotalPages = totalPages,
                PageRouteValueName = pageRouteValueName,
                Fragment = string.IsNullOrWhiteSpace(fragment)
                    ? null
                    : fragment.TrimStart('#')
            });
    }
}
