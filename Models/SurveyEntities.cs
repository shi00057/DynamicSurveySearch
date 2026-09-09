using System.ComponentModel.DataAnnotations;

namespace DynamicSurveySearch.Models;

public enum FormVersionStatus
{
    Draft,
    Published,
    Retired
}

public enum QuestionType
{
    ShortText,
    LongText,
    SingleChoice,
    MultipleChoice,
    Date,
    Number,
    Boolean
}

public enum SearchOperator
{
    Equals,
    Contains,
    StartsWith,
    Between,
    GreaterThanOrEqual,
    LessThanOrEqual,
    ContainsAny,
    ContainsAll
}

public sealed class FormType
{
    public int FormTypeId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public ICollection<SurveyForm> Forms { get; set; } = new List<SurveyForm>();
}

public sealed class SurveyForm
{
    public int SurveyFormId { get; set; }
    public int FormTypeId { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public FormType FormType { get; set; } = null!;
    public ICollection<SurveyFormVersion> Versions { get; set; } = new List<SurveyFormVersion>();
}

public sealed class SurveyFormVersion
{
    public int SurveyFormVersionId { get; set; }
    public int SurveyFormId { get; set; }
    public int VersionNumber { get; set; }
    public FormVersionStatus Status { get; set; } = FormVersionStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }

    public SurveyForm SurveyForm { get; set; } = null!;
    public ICollection<QuestionDefinition> Questions { get; set; } = new List<QuestionDefinition>();
    public ICollection<SurveyResult> Results { get; set; } = new List<SurveyResult>();
    public ICollection<SearchDefinition> SearchDefinitions { get; set; } = new List<SearchDefinition>();
}

public sealed class QuestionDefinition
{
    public int QuestionDefinitionId { get; set; }
    public int SurveyFormVersionId { get; set; }

    [MaxLength(100)]
    public string QuestionKey { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string QuestionText { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? HelpText { get; set; }

    public QuestionType QuestionType { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }

    public SurveyFormVersion SurveyFormVersion { get; set; } = null!;
    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    public ICollection<SurveyAnswer> Answers { get; set; } = new List<SurveyAnswer>();
    public ICollection<SearchFieldDefinition> SearchFields { get; set; } = new List<SearchFieldDefinition>();
}

public sealed class QuestionOption
{
    public int QuestionOptionId { get; set; }
    public int QuestionDefinitionId { get; set; }

    [MaxLength(100)]
    public string OptionValue { get; set; } = string.Empty;

    [MaxLength(300)]
    public string OptionText { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public QuestionDefinition QuestionDefinition { get; set; } = null!;
    public ICollection<SurveyAnswerOption> SelectedAnswers { get; set; } = new List<SurveyAnswerOption>();
}

public sealed class SurveyResult
{
    public long SurveyResultId { get; set; }
    public int SurveyFormVersionId { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? SubmittedBy { get; set; }

    public SurveyFormVersion SurveyFormVersion { get; set; } = null!;
    public ICollection<SurveyAnswer> Answers { get; set; } = new List<SurveyAnswer>();
}

public sealed class SurveyAnswer
{
    public long SurveyAnswerId { get; set; }
    public long SurveyResultId { get; set; }
    public int QuestionDefinitionId { get; set; }

    [MaxLength(2000)]
    public string? TextValue { get; set; }

    public decimal? NumberValue { get; set; }
    public DateTime? DateValue { get; set; }
    public bool? BooleanValue { get; set; }

    public SurveyResult SurveyResult { get; set; } = null!;
    public QuestionDefinition QuestionDefinition { get; set; } = null!;
    public ICollection<SurveyAnswerOption> SelectedOptions { get; set; } = new List<SurveyAnswerOption>();
}

public sealed class SurveyAnswerOption
{
    public long SurveyAnswerOptionId { get; set; }
    public long SurveyAnswerId { get; set; }
    public int QuestionOptionId { get; set; }

    public SurveyAnswer SurveyAnswer { get; set; } = null!;
    public QuestionOption QuestionOption { get; set; } = null!;
}

public sealed class SearchDefinition
{
    public int SearchDefinitionId { get; set; }
    public int SurveyFormVersionId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SurveyFormVersion SurveyFormVersion { get; set; } = null!;
    public ICollection<SearchFieldDefinition> Fields { get; set; } = new List<SearchFieldDefinition>();
}

public sealed class SearchFieldDefinition
{
    public int SearchFieldDefinitionId { get; set; }
    public int SearchDefinitionId { get; set; }
    public int QuestionDefinitionId { get; set; }

    [MaxLength(200)]
    public string DisplayLabel { get; set; } = string.Empty;

    public SearchOperator DefaultOperator { get; set; }
    public int DisplayOrder { get; set; }

    public SearchDefinition SearchDefinition { get; set; } = null!;
    public QuestionDefinition QuestionDefinition { get; set; } = null!;
}
