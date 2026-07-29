using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BC_CampusLearn.Validation;

[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter)]
public sealed class MustBeTrueAttribute
    : ValidationAttribute, IClientModelValidator
{
    public override bool IsValid(object? value)
    {
        return value is true;
    }

    public void AddValidation(
        ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        MergeAttribute(
            context.Attributes,
            "data-val",
            "true");
        MergeAttribute(
            context.Attributes,
            "data-val-mustbetrue",
            ErrorMessage ??
                "This field must be selected.");
    }

    private static void MergeAttribute(
        IDictionary<string, string> attributes,
        string key,
        string value)
    {
        if (!attributes.ContainsKey(key))
        {
            attributes.Add(key, value);
        }
    }
}
