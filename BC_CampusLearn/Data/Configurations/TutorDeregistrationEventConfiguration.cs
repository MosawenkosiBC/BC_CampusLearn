using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorDeregistrationEventConfiguration : IEntityTypeConfiguration<TutorDeregistrationEvent>
{
    public void Configure(EntityTypeBuilder<TutorDeregistrationEvent> builder)
    {
        builder.HasKey(item => item.TutorDeregistrationEventId);
        builder.Property(item => item.AdministratorName).HasMaxLength(200);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.HasOne(item => item.Tutor).WithMany(tutor => tutor.DeregistrationHistory)
            .HasForeignKey(item => item.TutorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TutorId, item.DeregisteredAt });
    }
}
