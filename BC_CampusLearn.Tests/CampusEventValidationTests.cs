using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Events;
using Xunit;

namespace BC_CampusLearn.Tests;

public class CampusEventValidationTests
{
    [Fact]
    public void CalculatesRemovalTimeTenMinutesAfterEvent()
    {
        DateTime eventDateTime = new(2026, 9, 30, 11, 0, 0);

        DateTimeOffset removalAt = CampusEventFormValidator.RemovalAt(eventDateTime);

        Assert.Equal(new DateTimeOffset(2026, 9, 30, 11, 10, 0,
            TimeSpan.FromHours(2)), removalAt);
    }

    [Fact]
    public void UsesCurrentTimeWhenScheduleIsEmpty()
    {
        DateTimeOffset now = new(2026, 9, 30, 8, 30, 0,
            TimeSpan.Zero);

        DateTimeOffset publishAt = CampusEventFormValidator.ResolvePublishAt(
            null, now);

        Assert.Equal(now, publishAt);
    }

    [Fact]
    public void ConvertsOversizedShortTextToLongText()
    {
        string value = new('a',
            CampusEventFormValidator.ShortTextCharacterLimit + 1);

        CampusEventDetailDataType dataType =
            CampusEventFormValidator.NormalizeDataType(
                CampusEventDetailDataType.ShortText, value);

        Assert.Equal(CampusEventDetailDataType.LongText, dataType);
    }

    [Theory]
    [InlineData(CampusEventDetailDataType.Number, "many")]
    [InlineData(CampusEventDetailDataType.Date, "next week")]
    [InlineData(CampusEventDetailDataType.Url, "not-a-link")]
    [InlineData(CampusEventDetailDataType.Email, "not-an-email")]
    public void ValidatesAdministratorDefinedFieldValues(
        CampusEventDetailDataType dataType,
        string value)
    {
        CampusEventInput input = ValidInput();
        input.CustomFields.Add(new CampusEventDetailInput
        {
            Title = "Custom detail",
            DataType = dataType,
            Value = value
        });

        var errors = CampusEventFormValidator.Validate(input).ToList();

        Assert.Contains(errors, error =>
            error.Key == "Input.CustomFields[0].Value");
    }

    private static CampusEventInput ValidInput() => new()
    {
        Title = "Industry Connect",
        Description = "Connect with industry partners.",
        Location = "Pretoria Campus",
        StartsAt = new DateTime(2026, 10, 10, 9, 0, 0),
        PublishAt = new DateTime(2026, 10, 1, 9, 0, 0)
    };
}
