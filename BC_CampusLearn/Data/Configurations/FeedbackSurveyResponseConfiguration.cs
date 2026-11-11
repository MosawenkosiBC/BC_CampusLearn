using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class FeedbackSurveyResponseConfiguration : IEntityTypeConfiguration<FeedbackSurveyResponse>
{
    public void Configure(EntityTypeBuilder<FeedbackSurveyResponse> builder)
    {
        builder.HasKey(response => new { response.FeedbackSurveyId, response.BcUserId });
        builder.HasOne<FeedbackSurvey>().WithMany().HasForeignKey(response => response.FeedbackSurveyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<BcUser>().WithMany().HasForeignKey(response => response.BcUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
