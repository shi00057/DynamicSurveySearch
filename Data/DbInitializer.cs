using DynamicSurveySearch.Models;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(SurveyDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.FormTypes.AnyAsync())
        {
            return;
        }

        var safetyType = new FormType
        {
            Name = "Safety Inspection",
            Description = "Operational safety and compliance surveys."
        };

        var form = new SurveyForm
        {
            FormType = safetyType,
            Code = "SAFETY_INSPECTION",
            Name = "Assignment Safety Inspection",
            Description = "Seeded example showing dynamic questions and search fields."
        };

        var version = new SurveyFormVersion
        {
            SurveyForm = form,
            VersionNumber = 1,
            Status = FormVersionStatus.Published,
            PublishedAtUtc = DateTime.UtcNow
        };

        QuestionDefinition location = ChoiceQuestion(
            version, "LOCATION", "Location", QuestionType.SingleChoice, 10,
            "STATION_12|Station 12", "STATION_18|Station 18", "STATION_24|Station 24");

        var inspectionDate = new QuestionDefinition
        {
            SurveyFormVersion = version,
            QuestionKey = "INSPECTION_DATE",
            QuestionText = "Inspection date",
            QuestionType = QuestionType.Date,
            IsRequired = true,
            DisplayOrder = 20
        };

        QuestionDefinition issues = ChoiceQuestion(
            version, "ISSUES", "Issues identified", QuestionType.MultipleChoice, 30,
            "EQUIPMENT|Equipment issue", "DOCUMENTATION|Documentation issue", "SAFETY|Safety issue");
        issues.IsRequired = false;

        var comments = new QuestionDefinition
        {
            SurveyFormVersion = version,
            QuestionKey = "COMMENTS",
            QuestionText = "Additional comments",
            QuestionType = QuestionType.LongText,
            IsRequired = false,
            DisplayOrder = 40
        };

        var compliant = new QuestionDefinition
        {
            SurveyFormVersion = version,
            QuestionKey = "COMPLIANT",
            QuestionText = "Was the assignment compliant?",
            QuestionType = QuestionType.Boolean,
            IsRequired = true,
            DisplayOrder = 50
        };

        version.Questions.Add(location);
        version.Questions.Add(inspectionDate);
        version.Questions.Add(issues);
        version.Questions.Add(comments);
        version.Questions.Add(compliant);

        db.SurveyForms.Add(form);
        await db.SaveChangesAsync();

        var search = new SearchDefinition
        {
            SurveyFormVersionId = version.SurveyFormVersionId,
            Name = "Default Safety Search",
            Fields = new List<SearchFieldDefinition>
            {
                SearchField(location, "Location", SearchOperator.Equals, 10),
                SearchField(inspectionDate, "Inspection date", SearchOperator.Between, 20),
                SearchField(issues, "Issues identified", SearchOperator.ContainsAny, 30),
                SearchField(comments, "Comments contain", SearchOperator.Contains, 40),
                SearchField(compliant, "Compliant", SearchOperator.Equals, 50)
            }
        };

        db.SearchDefinitions.Add(search);

        db.SurveyResults.Add(CreateResult(
            version,
            "Demo User 1",
            location.Options.Single(x => x.OptionValue == "STATION_12"),
            new DateTime(2026, 9, 1),
            issues.Options.Where(x => x.OptionValue == "EQUIPMENT").ToArray(),
            "Damaged equipment was replaced.",
            false,
            location, inspectionDate, issues, comments, compliant));

        db.SurveyResults.Add(CreateResult(
            version,
            "Demo User 2",
            location.Options.Single(x => x.OptionValue == "STATION_18"),
            new DateTime(2026, 9, 3),
            Array.Empty<QuestionOption>(),
            "No concerns were reported.",
            true,
            location, inspectionDate, issues, comments, compliant));

        await db.SaveChangesAsync();
    }

    private static QuestionDefinition ChoiceQuestion(
        SurveyFormVersion version,
        string key,
        string text,
        QuestionType type,
        int order,
        params string[] options)
    {
        var question = new QuestionDefinition
        {
            SurveyFormVersion = version,
            QuestionKey = key,
            QuestionText = text,
            QuestionType = type,
            IsRequired = true,
            DisplayOrder = order
        };

        int optionOrder = 10;
        foreach (string option in options)
        {
            string[] parts = option.Split('|', 2);
            question.Options.Add(new QuestionOption
            {
                OptionValue = parts[0],
                OptionText = parts[1],
                DisplayOrder = optionOrder
            });
            optionOrder += 10;
        }

        return question;
    }

    private static SearchFieldDefinition SearchField(
        QuestionDefinition question,
        string label,
        SearchOperator searchOperator,
        int order) => new()
        {
            QuestionDefinitionId = question.QuestionDefinitionId,
            DisplayLabel = label,
            DefaultOperator = searchOperator,
            DisplayOrder = order
        };

    private static SurveyResult CreateResult(
        SurveyFormVersion version,
        string submittedBy,
        QuestionOption locationOption,
        DateTime date,
        IReadOnlyCollection<QuestionOption> issueOptions,
        string comment,
        bool compliantValue,
        QuestionDefinition location,
        QuestionDefinition inspectionDate,
        QuestionDefinition issues,
        QuestionDefinition comments,
        QuestionDefinition compliant)
    {
        var locationAnswer = new SurveyAnswer { QuestionDefinitionId = location.QuestionDefinitionId };
        locationAnswer.SelectedOptions.Add(new SurveyAnswerOption
        {
            QuestionOptionId = locationOption.QuestionOptionId
        });

        var issueAnswer = new SurveyAnswer { QuestionDefinitionId = issues.QuestionDefinitionId };
        foreach (QuestionOption option in issueOptions)
        {
            issueAnswer.SelectedOptions.Add(new SurveyAnswerOption
            {
                QuestionOptionId = option.QuestionOptionId
            });
        }

        return new SurveyResult
        {
            SurveyFormVersionId = version.SurveyFormVersionId,
            SubmittedBy = submittedBy,
            SubmittedAtUtc = date.AddHours(14),
            Answers = new List<SurveyAnswer>
            {
                locationAnswer,
                new() { QuestionDefinitionId = inspectionDate.QuestionDefinitionId, DateValue = date },
                issueAnswer,
                new() { QuestionDefinitionId = comments.QuestionDefinitionId, TextValue = comment },
                new() { QuestionDefinitionId = compliant.QuestionDefinitionId, BooleanValue = compliantValue }
            }
        };
    }
}
