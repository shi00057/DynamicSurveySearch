using DynamicSurveySearch.Models;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Data;

public sealed class SurveyDbContext(DbContextOptions<SurveyDbContext> options) : DbContext(options)
{
    public DbSet<FormType> FormTypes => Set<FormType>();
    public DbSet<SurveyForm> SurveyForms => Set<SurveyForm>();
    public DbSet<SurveyFormVersion> SurveyFormVersions => Set<SurveyFormVersion>();
    public DbSet<QuestionDefinition> Questions => Set<QuestionDefinition>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<SurveyResult> SurveyResults => Set<SurveyResult>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();
    public DbSet<SurveyAnswerOption> SurveyAnswerOptions => Set<SurveyAnswerOption>();
    public DbSet<SearchDefinition> SearchDefinitions => Set<SearchDefinition>();
    public DbSet<SearchFieldDefinition> SearchFieldDefinitions => Set<SearchFieldDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormType>(entity =>
        {
            entity.ToTable("form_type");
            entity.HasKey(x => x.FormTypeId);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<SurveyForm>(entity =>
        {
            entity.ToTable("survey_form");
            entity.HasKey(x => x.SurveyFormId);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasOne(x => x.FormType).WithMany(x => x.Forms)
                .HasForeignKey(x => x.FormTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SurveyFormVersion>(entity =>
        {
            entity.ToTable("survey_form_version");
            entity.HasKey(x => x.SurveyFormVersionId);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.SurveyFormId, x.VersionNumber }).IsUnique();
            entity.HasOne(x => x.SurveyForm).WithMany(x => x.Versions)
                .HasForeignKey(x => x.SurveyFormId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionDefinition>(entity =>
        {
            entity.ToTable("question_list");
            entity.HasKey(x => x.QuestionDefinitionId);
            entity.Property(x => x.QuestionType).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(x => new { x.SurveyFormVersionId, x.QuestionKey }).IsUnique();
            entity.HasIndex(x => new { x.SurveyFormVersionId, x.DisplayOrder });
            entity.HasOne(x => x.SurveyFormVersion).WithMany(x => x.Questions)
                .HasForeignKey(x => x.SurveyFormVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.ToTable("question_option");
            entity.HasKey(x => x.QuestionOptionId);
            entity.HasIndex(x => new { x.QuestionDefinitionId, x.OptionValue }).IsUnique();
            entity.HasOne(x => x.QuestionDefinition).WithMany(x => x.Options)
                .HasForeignKey(x => x.QuestionDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyResult>(entity =>
        {
            entity.ToTable("survey_result");
            entity.HasKey(x => x.SurveyResultId);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.SurveyFormVersionId, x.SubmittedAtUtc });
            entity.HasOne(x => x.SurveyFormVersion).WithMany(x => x.Results)
                .HasForeignKey(x => x.SurveyFormVersionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SurveyAnswer>(entity =>
        {
            entity.ToTable("survey_answer");
            entity.HasKey(x => x.SurveyAnswerId);
            entity.Property(x => x.NumberValue).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.SurveyResultId, x.QuestionDefinitionId }).IsUnique();
            entity.HasIndex(x => new { x.QuestionDefinitionId, x.DateValue, x.SurveyResultId });
            entity.HasIndex(x => new { x.QuestionDefinitionId, x.NumberValue, x.SurveyResultId });
            entity.HasIndex(x => new { x.QuestionDefinitionId, x.BooleanValue, x.SurveyResultId });
            entity.HasOne(x => x.SurveyResult).WithMany(x => x.Answers)
                .HasForeignKey(x => x.SurveyResultId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.QuestionDefinition).WithMany(x => x.Answers)
                .HasForeignKey(x => x.QuestionDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SurveyAnswerOption>(entity =>
        {
            entity.ToTable("survey_answer_option");
            entity.HasKey(x => x.SurveyAnswerOptionId);
            entity.HasIndex(x => new { x.SurveyAnswerId, x.QuestionOptionId }).IsUnique();
            entity.HasIndex(x => new { x.QuestionOptionId, x.SurveyAnswerId });
            entity.HasOne(x => x.SurveyAnswer).WithMany(x => x.SelectedOptions)
                .HasForeignKey(x => x.SurveyAnswerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.QuestionOption).WithMany(x => x.SelectedAnswers)
                .HasForeignKey(x => x.QuestionOptionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SearchDefinition>(entity =>
        {
            entity.ToTable("search_definition");
            entity.HasKey(x => x.SearchDefinitionId);
            entity.HasIndex(x => new { x.SurveyFormVersionId, x.Name }).IsUnique();
            entity.HasOne(x => x.SurveyFormVersion).WithMany(x => x.SearchDefinitions)
                .HasForeignKey(x => x.SurveyFormVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SearchFieldDefinition>(entity =>
        {
            entity.ToTable("search_field_definition");
            entity.HasKey(x => x.SearchFieldDefinitionId);
            entity.Property(x => x.DefaultOperator).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(x => new { x.SearchDefinitionId, x.QuestionDefinitionId }).IsUnique();
            entity.HasOne(x => x.SearchDefinition).WithMany(x => x.Fields)
                .HasForeignKey(x => x.SearchDefinitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.QuestionDefinition).WithMany(x => x.SearchFields)
                .HasForeignKey(x => x.QuestionDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
