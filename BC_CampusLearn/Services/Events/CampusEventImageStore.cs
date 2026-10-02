namespace BC_CampusLearn.Services.Events;

public class CampusEventImageStore(IWebHostEnvironment environment)
{
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    public string? GetValidationError(IFormFile? image, bool required)
    {
        if (image is null || image.Length == 0)
        {
            return required ? "A banner image is required." : null;
        }
        if (image.Length > MaximumFileSize)
        {
            return "The banner image must be 5 MB or smaller.";
        }
        if (!AllowedTypes.ContainsKey(image.ContentType))
        {
            return "Use a JPG, PNG, or WebP banner image.";
        }
        return null;
    }

    public async Task<string> SaveAsync(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        string extension = AllowedTypes[image.ContentType];
        string directory = Path.Combine(
            environment.WebRootPath, "uploads", "events");
        Directory.CreateDirectory(directory);

        string fileName = $"{Guid.NewGuid():N}{extension}";
        string fullPath = Path.Combine(directory, fileName);
        await using FileStream stream = new(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await image.CopyToAsync(stream, cancellationToken);
        return $"/uploads/events/{fileName}";
    }

    public void Delete(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        string relativePath = imagePath.TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(
            environment.WebRootPath, relativePath));
        string uploadRoot = Path.GetFullPath(Path.Combine(
            environment.WebRootPath, "uploads", "events")) +
            Path.DirectorySeparatorChar;

        if (fullPath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
