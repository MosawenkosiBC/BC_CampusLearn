using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorContractConfiguration : IEntityTypeConfiguration<TutorContract>
{
    public void Configure(EntityTypeBuilder<TutorContract> builder)
    {
        builder.ToTable("TutorContracts");
        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.ContractFile).HasMaxLength(500).IsRequired();
        builder.Property(contract => contract.DateSubmitted).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(contract => contract.Tutor)
            .WithOne(tutor => tutor.Contract)
            .HasForeignKey<TutorContract>(contract => contract.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
