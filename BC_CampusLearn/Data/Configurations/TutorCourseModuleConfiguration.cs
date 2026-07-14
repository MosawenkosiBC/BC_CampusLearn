using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorCourseModuleConfiguration
    : IEntityTypeConfiguration<TutorCourseModule>
{
    public void Configure(
        EntityTypeBuilder<TutorCourseModule> builder)
    {
        builder.HasKey(item => new
        {
            item.TutorId,
            item.CourseModuleId
        });

        builder.HasOne(item => item.Tutor)
            .WithMany(tutor => tutor.TutorCourseModules)
            .HasForeignKey(item => item.TutorId);

        builder.HasOne(item => item.CourseModule)
            .WithMany(module => module.TutorCourseModules)
            .HasForeignKey(item => item.CourseModuleId);
    }
}