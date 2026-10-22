using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public sealed class TutorInterviewMeetingConfiguration : IEntityTypeConfiguration<TutorInterviewMeeting>
{
    public void Configure(EntityTypeBuilder<TutorInterviewMeeting> builder)
    {
        builder.HasKey(x => x.TutorInterviewMeetingId);
        builder.HasIndex(x => new { x.TutorId, x.ApplicationSubmittedAt }).IsUnique();
        builder.Property(x => x.OrganizerObjectId).HasMaxLength(36);
        builder.Property(x => x.RequestJson).HasMaxLength(30000);
        builder.Property(x => x.EventId).HasMaxLength(1024);
        builder.Property(x => x.JoinUrl).HasMaxLength(2048);
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
