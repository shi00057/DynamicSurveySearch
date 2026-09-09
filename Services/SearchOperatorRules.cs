using DynamicSurveySearch.Models;

namespace DynamicSurveySearch.Services;

public static class SearchOperatorRules
{
    public static IReadOnlyList<SearchOperator> GetAllowed(QuestionType type) => type switch
    {
        QuestionType.ShortText or QuestionType.LongText =>
            new[] { SearchOperator.Contains, SearchOperator.StartsWith, SearchOperator.Equals },
        QuestionType.SingleChoice =>
            new[] { SearchOperator.Equals, SearchOperator.ContainsAny },
        QuestionType.MultipleChoice =>
            new[] { SearchOperator.ContainsAny, SearchOperator.ContainsAll },
        QuestionType.Date or QuestionType.Number =>
            new[]
            {
                SearchOperator.Between,
                SearchOperator.Equals,
                SearchOperator.GreaterThanOrEqual,
                SearchOperator.LessThanOrEqual
            },
        QuestionType.Boolean => new[] { SearchOperator.Equals },
        _ => Array.Empty<SearchOperator>()
    };

    public static SearchOperator GetDefault(QuestionType type) => type switch
    {
        QuestionType.ShortText or QuestionType.LongText => SearchOperator.Contains,
        QuestionType.MultipleChoice => SearchOperator.ContainsAny,
        QuestionType.Date or QuestionType.Number => SearchOperator.Between,
        _ => SearchOperator.Equals
    };

    public static bool IsAllowed(QuestionType type, SearchOperator value) =>
        GetAllowed(type).Contains(value);

    public static string GetLabel(SearchOperator value) => value switch
    {
        SearchOperator.StartsWith => "Starts with",
        SearchOperator.GreaterThanOrEqual => "Greater than or equal",
        SearchOperator.LessThanOrEqual => "Less than or equal",
        SearchOperator.ContainsAny => "Contains any selected option",
        SearchOperator.ContainsAll => "Contains all selected options",
        _ => value.ToString()
    };
}
