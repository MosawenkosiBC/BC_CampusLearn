using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class ResourceCommentConfiguration
    : IEntityTypeConfiguration<ResourceComment>
{
    public void Configure(EntityTypeBuilder<ResourceComment> builder)
    {
        builder.ToTable("ResourceComments");
        builder.HasKey(comment => comment.CommentId);
        builder.Property(comment => comment.CommentId)
            .ValueGeneratedOnAdd();
        builder.Property(comment => comment.CommentText)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(comment => comment.DateCreated)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(comment => comment.IsEdited).HasDefaultValue(false);
        builder.Property(comment => comment.IsDeleted).HasDefaultValue(false);
        builder.Property(comment => comment.IsPinned).HasDefaultValue(false);

        builder.HasOne(comment => comment.Resource)
            .WithMany(resource => resource.Comments)
            .HasForeignKey(comment => comment.ResourceId)
            .HasPrincipalKey(resource => resource.LearningResourceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ResourceComments_Resources");

        builder.HasOne(comment => comment.Author)
            .WithMany(user => user.ResourceComments)
            .HasForeignKey(comment => comment.AuthorUserId)
            .HasPrincipalKey(user => user.BcUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ResourceComments_Authors");

        builder.HasOne(comment => comment.ParentComment)
            .WithMany(comment => comment.Replies)
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ResourceComments_Parent");

        builder.HasIndex(comment => new
        {
            comment.ResourceId,
            comment.IsPinned,
            comment.DateCreated
        });
        builder.HasIndex(comment => comment.ParentCommentId);
    }
}
