using DynamicSurveySearch.Models;

namespace DynamicSurveySearch.Services;

public sealed class SearchCriterion
{
    public int QuestionId { get; init; }
    public QuestionType QuestionType { get; init; }
    public SearchOperator Operator { get; init; }
    public string? TextValue { get; init; }
    public decimal? FromNumber { get; init; }
    public decimal? ToNumber { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool? BooleanValue { get; init; }
    public IReadOnlyList<int> OptionIds { get; init; } = Array.Empty<int>();
}

public sealed class SurveySearchResultRow
{
    public long SurveyResultId { get; init; }
    public Guid PublicId { get; init; }
    public DateTime SubmittedAtUtc { get; init; }
    public string? SubmittedBy { get; init; }
}

public sealed class SurveySearchPage
{
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public IReadOnlyList<SurveySearchResultRow> Rows { get; init; } = Array.Empty<SurveySearchResultRow>();
}
