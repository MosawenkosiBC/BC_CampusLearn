namespace BC_CampusLearn.Models.ViewModels;

public enum CampusAlertVariant
{
    Success,
    Danger,
    Warning,
    Info
}

public record CampusAlertViewModel(
    string Message,
    CampusAlertVariant Variant = CampusAlertVariant.Success)
{
    public string BootstrapClass => Variant switch
    {
        CampusAlertVariant.Danger => "danger",
        CampusAlertVariant.Warning => "warning",
        CampusAlertVariant.Info => "info",
        _ => "success"
    };

    public string IconClass => Variant switch
    {
        CampusAlertVariant.Danger => "bi-exclamation-circle-fill",
        CampusAlertVariant.Warning => "bi-exclamation-triangle-fill",
        CampusAlertVariant.Info => "bi-info-circle-fill",
        _ => "bi-check-circle-fill"
    };

    public string Role => Variant == CampusAlertVariant.Danger
        ? "alert"
        : "status";
}
