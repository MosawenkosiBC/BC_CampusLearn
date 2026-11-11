using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class FeedbackSurveyConfiguration : IEntityTypeConfiguration<FeedbackSurvey>
{
    public void Configure(EntityTypeBuilder<FeedbackSurvey> builder)
    {
        builder.HasKey(survey => survey.FeedbackSurveyId);
        builder.Property(survey => survey.FeedbackSurveyId).ValueGeneratedNever();
        builder.Property(survey => survey.Title).HasMaxLength(160);
        builder.Property(survey => survey.Description).HasMaxLength(2000);
        builder.Property(survey => survey.Revision).IsConcurrencyToken();
        builder.HasIndex(survey => new { survey.Audience, survey.Title });
    }
}
