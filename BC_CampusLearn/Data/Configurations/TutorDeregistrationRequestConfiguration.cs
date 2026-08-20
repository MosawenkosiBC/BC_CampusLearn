using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorDeregistrationRequestConfiguration : IEntityTypeConfiguration<TutorDeregistrationRequest>
{
    public void Configure(EntityTypeBuilder<TutorDeregistrationRequest> builder)
    {
        builder.HasKey(request => request.TutorDeregistrationRequestId);
        builder.Property(request => request.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(request => request.Status).HasConversion<int>()
            .HasDefaultValue(TutorAccountRequestStatus.Pending);
        builder.Property(request => request.SubmittedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(request => new { request.TutorId, request.Status });
        builder.HasOne(request => request.Tutor)
            .WithMany(tutor => tutor.DeregistrationRequests)
            .HasForeignKey(request => request.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
