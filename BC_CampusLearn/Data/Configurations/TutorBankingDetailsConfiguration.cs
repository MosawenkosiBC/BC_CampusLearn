using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorBankingDetailsConfiguration : IEntityTypeConfiguration<TutorBankingDetails>
{
    public void Configure(EntityTypeBuilder<TutorBankingDetails> builder)
    {
        builder.ToTable("TutorBankingDetails");
        builder.HasKey(details => details.Id);
        builder.Property(details => details.AccountHolder).HasMaxLength(200).IsRequired();
        builder.Property(details => details.RecipientAccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(details => details.AccountType).HasMaxLength(50).IsRequired();
        builder.Property(details => details.BranchCode).HasMaxLength(20).IsRequired();
        builder.Property(details => details.BankName).HasMaxLength(150).IsRequired();
        builder.HasOne(details => details.Tutor)
            .WithOne(tutor => tutor.BankingDetails)
            .HasForeignKey<TutorBankingDetails>(details => details.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
