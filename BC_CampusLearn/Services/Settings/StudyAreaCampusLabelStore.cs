using System.Text.Json;
using BC_CampusLearn.Models.ViewModels;

namespace BC_CampusLearn.Services.Settings;

/// <summary>
/// Stores UI-only study-area labels outside the application database.
/// </summary>
public sealed class StudyAreaCampusLabelStore
{
    private readonly string? filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<int, string>? labels;

    public StudyAreaCampusLabelStore()
    {
    }

    public StudyAreaCampusLabelStore(IWebHostEnvironment environment)
    {
        filePath = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "study-area-campus-labels.json");
    }

    public async Task<string> GetLabelAsync(
        int studyAreaId,
        string studyAreaName,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return labels!.TryGetValue(studyAreaId, out string? label)
            ? label
            : StudyAreaDisplay.GetSubtext(studyAreaName);
    }

    public async Task SetLabelAsync(
        int studyAreaId,
        string studyAreaName,
        string campusLabel,
        CancellationToken cancellationToken = default)
    {
        if (!StudyAreaDisplay.IsCampusLabel(campusLabel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(campusLabel),
                "The campus label is not supported.");
        }
        campusLabel = campusLabel.Trim();

        await gate.WaitAsync(cancellationToken);
        try
        {
            await LoadWhileLockedAsync(cancellationToken);
            string defaultLabel = StudyAreaDisplay.GetSubtext(studyAreaName);
            if (string.Equals(
                campusLabel,
                defaultLabel,
                StringComparison.OrdinalIgnoreCase))
            {
                labels!.Remove(studyAreaId);
            }
            else
            {
                labels![studyAreaId] = campusLabel;
            }
            await SaveWhileLockedAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveLabelAsync(
        int studyAreaId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await LoadWhileLockedAsync(cancellationToken);
            if (labels!.Remove(studyAreaId))
            {
                await SaveWhileLockedAsync(cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (labels is not null) return;

        await gate.WaitAsync(cancellationToken);
        try
        {
            await LoadWhileLockedAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task LoadWhileLockedAsync(CancellationToken cancellationToken)
    {
        if (labels is not null) return;
        if (filePath is null || !File.Exists(filePath))
        {
            labels = [];
            return;
        }

        await using FileStream stream = File.OpenRead(filePath);
        labels = await JsonSerializer.DeserializeAsync<Dictionary<int, string>>(
            stream,
            cancellationToken: cancellationToken) ?? [];
    }

    private async Task SaveWhileLockedAsync(CancellationToken cancellationToken)
    {
        if (filePath is null) return;

        string directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = filePath + ".tmp";
        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                labels,
                new JsonSerializerOptions { WriteIndented = true },
                cancellationToken);
        }
        File.Move(temporaryPath, filePath, true);
    }
}
