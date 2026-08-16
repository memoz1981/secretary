using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

/// <summary>The Feedback module's tables, in one file for the same reason the Orders ones are:
/// six small, closely-related shapes that are only meaningful together.</summary>
public sealed class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> builder)
    {
        builder.ToTable("Surveys", DbSchemas.Feedback);
        builder.ConfigureBaseEntity();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(s => s.TenantId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SurveyQuestionConfiguration : IEntityTypeConfiguration<SurveyQuestion>
{
    public void Configure(EntityTypeBuilder<SurveyQuestion> builder)
    {
        builder.ToTable("SurveyQuestions", DbSchemas.Feedback);
        builder.ConfigureBaseEntity();
        builder.Property(q => q.Text).HasMaxLength(500).IsRequired();

        builder.Ignore(q => q.IsScored);

        builder.HasIndex(q => new { q.SurveyId, q.Position });
        builder.HasOne<Survey>().WithMany().HasForeignKey(q => q.SurveyId).OnDelete(DeleteBehavior.Cascade);

        // Cascade, because an option without its question is nothing. Deleting a question the
        // agent has already asked is prevented in the service, not here — the answers are the
        // reason, and they are a different table.
        builder.HasMany(q => q.Options).WithOne().HasForeignKey(o => o.SurveyQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SurveyQuestionOptionConfiguration : IEntityTypeConfiguration<SurveyQuestionOption>
{
    public void Configure(EntityTypeBuilder<SurveyQuestionOption> builder)
    {
        builder.ToTable("SurveyQuestionOptions", DbSchemas.Feedback);
        builder.ConfigureBaseEntity();
        builder.Property(o => o.Text).HasMaxLength(200).IsRequired();

        // A percentage with two places, because a 1-10 scale lands on 11.11 and rounding it to a
        // whole number would make ten steps that do not add up to a hundred.
        builder.Property(o => o.ScorePercent).HasPrecision(5, 2);

        builder.HasIndex(o => new { o.SurveyQuestionId, o.Position });
    }
}

public sealed class FeedbackSettingsConfiguration : IEntityTypeConfiguration<FeedbackSettings>
{
    public void Configure(EntityTypeBuilder<FeedbackSettings> builder)
    {
        builder.ToTable("FeedbackSettings", DbSchemas.Feedback);
        builder.ConfigureBaseEntity();

        // One row per tenant. A second would be a second quota.
        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FeedbackCallConfiguration : IEntityTypeConfiguration<FeedbackCall>
{
    public void Configure(EntityTypeBuilder<FeedbackCall> builder)
    {
        builder.ToTable("Calls", DbSchemas.Feedback);
        builder.ConfigureBaseEntity();
        builder.Property(c => c.PersonName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(c => c.AgentModel).HasMaxLength(64).IsRequired();

        // Same precision and the same reason as the other call logs: a call costs cents, the
        // line items behind it are fractions of one, and a month is their sum.
        builder.Property(c => c.CostUsd).HasPrecision(18, 8);

        builder.Ignore(c => c.TokenUsage);

        builder.HasIndex(c => new { c.TenantId, c.CreatedAtUtc });
        builder.HasIndex(c => c.SurveyId);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Survey>().WithMany().HasForeignKey(c => c.SurveyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FeedbackAnswerConfiguration : IEntityTypeConfiguration<FeedbackAnswer>
{
    public void Configure(EntityTypeBuilder<FeedbackAnswer> builder)
    {
        builder.ToTable("Answers", DbSchemas.Feedback);
        builder.ConfigureBaseEntity();

        // No length cap: this is what the caller actually said, transcribed. Truncating it would
        // lose the only part of a survey nobody could have predicted.
        builder.Property(a => a.Text);

        // One answer per question per call — the agent asks each once, and a second row would
        // double-count it on the dashboard.
        builder.HasIndex(a => new { a.FeedbackCallId, a.SurveyQuestionId }).IsUnique();

        builder.HasOne<FeedbackCall>().WithMany().HasForeignKey(a => a.FeedbackCallId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, deliberately: a question with answers against it cannot be deleted out from
        // under them, or the dashboard silently loses history it already reported.
        builder.HasOne<SurveyQuestion>().WithMany().HasForeignKey(a => a.SurveyQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SurveyQuestionOption>().WithMany().HasForeignKey(a => a.SurveyQuestionOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
