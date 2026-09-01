using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SessionExecutionConfiguration
    : IEntityTypeConfiguration<SessionExecution>
{
    public void Configure(EntityTypeBuilder<SessionExecution> builder)
    {
        builder.ToTable("SessionExecutions");
        builder.HasKey(item => item.BookingId);
        builder.Property(item => item.StartedAt)
            .HasColumnType("datetimeoffset");
        builder.Property(item => item.ExpectedCompletionAt)
            .HasColumnType("datetimeoffset");
        builder.Property(item => item.CompletedAt)
            .HasColumnType("datetimeoffset");
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne(item => item.Booking)
            .WithOne(booking => booking.SessionExecution)
            .HasForeignKey<SessionExecution>(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.ExpectedCompletionAt);
    }
}
