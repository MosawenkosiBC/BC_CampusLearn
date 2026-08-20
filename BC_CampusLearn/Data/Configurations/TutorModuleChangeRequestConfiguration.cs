using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorModuleChangeRequestConfiguration : IEntityTypeConfiguration<TutorModuleChangeRequest>
{
    public void Configure(EntityTypeBuilder<TutorModuleChangeRequest> builder)
    {
        builder.HasKey(request => request.TutorModuleChangeRequestId);
        builder.Property(request => request.RequestType).HasConversion<int>().IsRequired();
        builder.Property(request => request.Status).HasConversion<int>()
            .HasDefaultValue(TutorAccountRequestStatus.Pending);
        builder.Property(request => request.Reason).HasMaxLength(500);
        builder.Property(request => request.SubmittedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(request => new { request.TutorId, request.ProgrammeModuleId, request.Status });

        builder.HasOne(request => request.Tutor)
            .WithMany(tutor => tutor.ModuleChangeRequests)
            .HasForeignKey(request => request.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(request => request.ProgrammeModule)
            .WithMany(module => module.TutorModuleChangeRequests)
            .HasForeignKey(request => request.ProgrammeModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
