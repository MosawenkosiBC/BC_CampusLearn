using System.Text;
using System.Text.Json;

namespace BC_CampusLearn.Models.ViewModels;

public class StudentLearningResourceListItem
{
    private static readonly TimeSpan SouthAfricaOffset = TimeSpan.FromHours(2);

    public int LearningResourceId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string? TutorProfileImagePath { get; set; }
    public DateTimeOffset? DatePublished { get; set; }

    public string Summary => ExtractPlainText(Content, 115);

    public string TopicDisplay
    {
        get
        {
            string[] words = Topic.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            return words.Length <= 4
                ? string.Join(" ", words)
                : $"{string.Join(" ", words.Take(4))}...";
        }
    }

    public string PublishedDateDisplay => DatePublished?
        .ToOffset(SouthAfricaOffset)
        .ToString("dd/MM/yyyy") ?? "Recently";

    private static string ExtractPlainText(string content, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Open this resource to start learning.";
        }

        string text = content;
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("ops", out JsonElement operations) &&
                operations.ValueKind == JsonValueKind.Array)
            {
                StringBuilder builder = new();
                foreach (JsonElement operation in operations.EnumerateArray())
                {
                    if (operation.TryGetProperty("insert", out JsonElement insert) &&
                        insert.ValueKind == JsonValueKind.String)
                    {
                        builder.Append(insert.GetString());
                    }
                }
                text = builder.ToString();
            }
        }
        catch (JsonException)
        {
            // Older resources may contain plain text rather than a Quill Delta.
        }

        text = string.Join(" ", text.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Open this resource to start learning.";
        }
        return text.Length <= maximumLength
            ? text
            : text[..maximumLength].TrimEnd() + "…";
    }
}
