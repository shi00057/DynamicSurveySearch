using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Services;

public sealed class SurveySearchService(SurveyDbContext db)
{
    public async Task<SurveySearchPage> SearchAsync(
        int surveyFormVersionId,
        IReadOnlyCollection<SearchCriterion> criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<SurveyResult> query = db.SurveyResults
            .AsNoTracking()
            .Where(x => x.SurveyFormVersionId == surveyFormVersionId);

        foreach (SearchCriterion criterion in criteria)
        {
            query = ApplyCriterion(query, criterion);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<SurveySearchResultRow> rows = await query
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ThenByDescending(x => x.SurveyResultId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SurveySearchResultRow
            {
                SurveyResultId = x.SurveyResultId,
                PublicId = x.PublicId,
                SubmittedAtUtc = x.SubmittedAtUtc,
                SubmittedBy = x.SubmittedBy
            })
            .ToListAsync(cancellationToken);

        return new SurveySearchPage
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Rows = rows
        };
    }

    private static IQueryable<SurveyResult> ApplyCriterion(
        IQueryable<SurveyResult> query,
        SearchCriterion criterion)
    {
        int questionId = criterion.QuestionId;

        return criterion.QuestionType switch
        {
            QuestionType.ShortText or QuestionType.LongText =>
                ApplyText(query, questionId, criterion),
            QuestionType.SingleChoice or QuestionType.MultipleChoice =>
                ApplyOptions(query, questionId, criterion),
            QuestionType.Date => ApplyDate(query, questionId, criterion),
            QuestionType.Number => ApplyNumber(query, questionId, criterion),
            QuestionType.Boolean => ApplyBoolean(query, questionId, criterion),
            _ => query
        };
    }

    private static IQueryable<SurveyResult> ApplyText(
        IQueryable<SurveyResult> query,
        int questionId,
        SearchCriterion criterion)
    {
        string value = criterion.TextValue?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return query;
        }

        return criterion.Operator switch
        {
            SearchOperator.Equals => query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.TextValue == value)),
            SearchOperator.StartsWith => query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId &&
                answer.TextValue != null && answer.TextValue.StartsWith(value))),
            _ => query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId &&
                answer.TextValue != null && answer.TextValue.Contains(value)))
        };
    }

    private static IQueryable<SurveyResult> ApplyOptions(
        IQueryable<SurveyResult> query,
        int questionId,
        SearchCriterion criterion)
    {
        int[] optionIds = criterion.OptionIds.Distinct().ToArray();
        if (optionIds.Length == 0)
        {
            return query;
        }

        if (criterion.Operator == SearchOperator.ContainsAll)
        {
            foreach (int optionId in optionIds)
            {
                int currentOptionId = optionId;
                query = query.Where(result => result.Answers.Any(answer =>
                    answer.QuestionDefinitionId == questionId &&
                    answer.SelectedOptions.Any(selected =>
                        selected.QuestionOptionId == currentOptionId)));
            }

            return query;
        }

        return query.Where(result => result.Answers.Any(answer =>
            answer.QuestionDefinitionId == questionId &&
            answer.SelectedOptions.Any(selected =>
                optionIds.Contains(selected.QuestionOptionId))));
    }

    private static IQueryable<SurveyResult> ApplyDate(
        IQueryable<SurveyResult> query,
        int questionId,
        SearchCriterion criterion)
    {
        DateTime? from = criterion.FromDate?.Date;
        DateTime? toExclusive = criterion.ToDate?.Date.AddDays(1);

        if (criterion.Operator == SearchOperator.Equals && from.HasValue)
        {
            DateTime start = from.Value;
            DateTime end = start.AddDays(1);
            return query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId &&
                answer.DateValue >= start && answer.DateValue < end));
        }

        if (criterion.Operator == SearchOperator.GreaterThanOrEqual && from.HasValue)
        {
            DateTime start = from.Value;
            return query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.DateValue >= start));
        }

        if (criterion.Operator == SearchOperator.LessThanOrEqual && toExclusive.HasValue)
        {
            DateTime end = toExclusive.Value;
            return query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.DateValue < end));
        }

        if (from.HasValue)
        {
            DateTime start = from.Value;
            query = query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.DateValue >= start));
        }

        if (toExclusive.HasValue)
        {
            DateTime end = toExclusive.Value;
            query = query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.DateValue < end));
        }

        return query;
    }

    private static IQueryable<SurveyResult> ApplyNumber(
        IQueryable<SurveyResult> query,
        int questionId,
        SearchCriterion criterion)
    {
        decimal? from = criterion.FromNumber;
        decimal? to = criterion.ToNumber;

        if (criterion.Operator == SearchOperator.Equals && from.HasValue)
        {
            decimal value = from.Value;
            return query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.NumberValue == value));
        }

        if (criterion.Operator == SearchOperator.GreaterThanOrEqual && from.HasValue)
        {
            decimal value = from.Value;
            return query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.NumberValue >= value));
        }

        if (criterion.Operator == SearchOperator.LessThanOrEqual && to.HasValue)
        {
            decimal value = to.Value;
            return query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.NumberValue <= value));
        }

        if (from.HasValue)
        {
            decimal value = from.Value;
            query = query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.NumberValue >= value));
        }

        if (to.HasValue)
        {
            decimal value = to.Value;
            query = query.Where(result => result.Answers.Any(answer =>
                answer.QuestionDefinitionId == questionId && answer.NumberValue <= value));
        }

        return query;
    }

    private static IQueryable<SurveyResult> ApplyBoolean(
        IQueryable<SurveyResult> query,
        int questionId,
        SearchCriterion criterion)
    {
        if (!criterion.BooleanValue.HasValue)
        {
            return query;
        }

        bool value = criterion.BooleanValue.Value;
        return query.Where(result => result.Answers.Any(answer =>
            answer.QuestionDefinitionId == questionId && answer.BooleanValue == value));
    }
}
