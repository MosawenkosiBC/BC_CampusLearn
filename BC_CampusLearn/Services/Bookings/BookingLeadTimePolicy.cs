namespace BC_CampusLearn.Services.Bookings;

public static class BookingLeadTimePolicy
{
    public const int MaximumMinutes = 365 * 24 * 60;

    public static bool TryParse(
        string? value,
        out int minutes)
    {
        minutes = 0;
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized is "no lead time" or "none" or
            "0 mins (no lead time)")
        {
            return true;
        }

        System.Text.RegularExpressions.Match match =
            System.Text.RegularExpressions.Regex.Match(
                normalized,
                @"^(?<amount>\d+)\s*(?<unit>[a-z]*)$");

        if (!match.Success ||
            !int.TryParse(match.Groups["amount"].Value, out int amount))
        {
            return false;
        }

        long multiplier = match.Groups["unit"].Value switch
        {
            "" or "m" or "min" or "mins" or "minute" or "minutes" => 1,
            "h" or "hr" or "hrs" or "hour" or "hours" => 60,
            "d" or "day" or "days" => 24 * 60,
            _ => 0
        };
        long totalMinutes = amount * multiplier;

        if (multiplier == 0 || totalMinutes > MaximumMinutes)
        {
            return false;
        }

        minutes = (int)totalMinutes;
        return true;
    }

    public static string Format(int minutes)
    {
        if (minutes == 0)
        {
            return "0 mins (No lead time)";
        }

        if (minutes % (24 * 60) == 0)
        {
            int days = minutes / (24 * 60);
            return $"{days} {(days == 1 ? "day" : "days")}";
        }

        if (minutes % 60 == 0)
        {
            int hours = minutes / 60;
            return $"{hours} {(hours == 1 ? "hour" : "hours")}";
        }

        return $"{minutes} {(minutes == 1 ? "minute" : "minutes")}";
    }

    public static string FormatAdjective(int minutes)
    {
        if (minutes % (24 * 60) == 0)
        {
            return $"{minutes / (24 * 60)}-day";
        }

        if (minutes % 60 == 0)
        {
            return $"{minutes / 60}-hour";
        }

        return $"{minutes}-minute";
    }

    public static bool IsBookable(
        DateTimeOffset sessionStart,
        int minimumLeadTimeMinutes,
        DateTimeOffset now) =>
        sessionStart >= now.AddMinutes(minimumLeadTimeMinutes);
}
