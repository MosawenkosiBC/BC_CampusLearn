using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using System.Globalization;
using System.Net.Mail;

namespace BC_CampusLearn.Services.Events;

public static class CampusEventFormValidator
{
    public const int ShortTextCharacterLimit = 160;

    public static IEnumerable<(string Key, string Message)> Validate(
        CampusEventInput input)
    {
        for (int index = 0; index < input.CustomFields.Count; index++)
        {
            CampusEventDetailInput field = input.CustomFields[index];
            string key = $"Input.CustomFields[{index}].Value";
            string value = field.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(field.Title) &&
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            bool valid = field.DataType switch
            {
                CampusEventDetailDataType.Number => decimal.TryParse(
                    value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
                CampusEventDetailDataType.Date => DateOnly.TryParse(value, out _),
                CampusEventDetailDataType.Time => TimeOnly.TryParse(value, out _),
                CampusEventDetailDataType.Url => Uri.TryCreate(
                    value, UriKind.Absolute, out Uri? uri) &&
                    uri.Scheme is "http" or "https",
                CampusEventDetailDataType.Email => IsEmail(value),
                CampusEventDetailDataType.YesNo => bool.TryParse(value, out _),
                _ => true
            };
            if (!valid)
            {
                yield return (key, $"Enter a valid {TypeLabel(field.DataType)} value.");
            }
        }
    }

    public static DateTimeOffset AtSouthAfricaOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            TimeSpan.FromHours(2));

    public static DateTimeOffset RemovalAt(DateTime eventDateTime) =>
        AtSouthAfricaOffset(eventDateTime).AddMinutes(10);

    public static DateTimeOffset ResolvePublishAt(
        DateTime? scheduledDateTime,
        DateTimeOffset publishImmediatelyAt) =>
        scheduledDateTime.HasValue
            ? AtSouthAfricaOffset(scheduledDateTime.Value)
            : publishImmediatelyAt;

    public static CampusEventDetailDataType NormalizeDataType(
        CampusEventDetailDataType dataType,
        string? value) =>
        dataType == CampusEventDetailDataType.ShortText &&
        (value?.Trim().Length ?? 0) > ShortTextCharacterLimit
            ? CampusEventDetailDataType.LongText
            : dataType;

    public static string TypeLabel(CampusEventDetailDataType type) => type switch
    {
        CampusEventDetailDataType.ShortText => "short text",
        CampusEventDetailDataType.LongText => "long text",
        CampusEventDetailDataType.YesNo => "yes/no",
        _ => type.ToString().ToLowerInvariant()
    };

    private static bool IsEmail(string value)
    {
        try
        {
            return new MailAddress(value).Address == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
