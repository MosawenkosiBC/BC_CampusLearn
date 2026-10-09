using System.Text;
using System.Text.Json;

namespace BC_CampusLearn.Services.Announcements;

public static class AnnouncementContent
{
    public const int MaximumLength = 100000;
    public static bool TryGetText(string? content, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500000) return false;
        if (!content.TrimStart().StartsWith('{'))
        {
            text = content.Trim();
            return text.Length <= MaximumLength;
        }
        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 16 });
            if (!document.RootElement.TryGetProperty("ops", out var operations) ||
                operations.ValueKind != JsonValueKind.Array || operations.GetArrayLength() > 1000) return false;
            var result = new StringBuilder();
            foreach (var operation in operations.EnumerateArray())
            {
                if (operation.ValueKind != JsonValueKind.Object ||
                    !operation.TryGetProperty("insert", out var insert) ||
                    insert.ValueKind != JsonValueKind.String ||
                    operation.TryGetProperty("delete", out _) || operation.TryGetProperty("retain", out _)) return false;
                result.Append(insert.GetString());
                if (result.Length > MaximumLength) return false;
                if (!operation.TryGetProperty("attributes", out var attributes)) continue;
                if (attributes.ValueKind != JsonValueKind.Object) return false;
                foreach (var attribute in attributes.EnumerateObject())
                {
                    bool valid = attribute.Name switch
                    {
                        "bold" or "italic" or "underline" or "blockquote" => attribute.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                        "header" => attribute.Value.ValueKind == JsonValueKind.Number && attribute.Value.TryGetInt32(out int level) && level is 2 or 3,
                        "list" => attribute.Value.ValueKind == JsonValueKind.String && attribute.Value.GetString() is "ordered" or "bullet",
                        "code-block" => attribute.Value.ValueKind is JsonValueKind.True or JsonValueKind.False ||
                            (attribute.Value.ValueKind == JsonValueKind.String && attribute.Value.GetString() == "plain"),
                        "link" => attribute.Value.ValueKind == JsonValueKind.String &&
                            Uri.TryCreate(attribute.Value.GetString(), UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                        _ => false
                    };
                    if (!valid) return false;
                }
            }
            text = result.ToString().Trim();
            return text.Length > 0;
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
