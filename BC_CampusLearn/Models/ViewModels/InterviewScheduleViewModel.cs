using BC_CampusLearn.Services.Teams;

namespace BC_CampusLearn.Models.ViewModels;

public sealed record InterviewScheduleViewModel(
    int TutorId, InterviewMeetingInput Input, string? AdminEmail,
    IReadOnlyList<InterviewPanelInput> Directory, bool Reserved, string? Error, string? Search, bool Editing = false);
