using System.Globalization;
using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using DynamicSurveySearch.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Pages.Searches;

public sealed class RunModel(SurveyDbContext db, SurveySearchService searchService) : PageModel
{
    public SearchDefinition Definition { get; private set; } = null!;
    public SurveySearchPage? Results { get; private set; }
    public bool HasSearched { get; private set; }
    public Dictionary<string, string[]> SubmittedValues { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(int searchDefinitionId)
    {
        return await LoadDefinitionAsync(searchDefinitionId) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(int searchDefinitionId, int page = 1, int pageSize = 30)
    {
        if (!await LoadDefinitionAsync(searchDefinitionId))
        {
            return NotFound();
        }

        IFormCollection form = await Request.ReadFormAsync();
        SubmittedValues = form.ToDictionary(
            x => x.Key,
            x => x.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        var criteria = new List<SearchCriterion>();
        foreach (SearchFieldDefinition field in Definition.Fields)
        {
            SearchCriterion? criterion = ParseCriterion(field, form);
            if (criterion is not null)
            {
                criteria.Add(criterion);
            }
        }

        if (ModelState.IsValid)
        {
            Results = await searchService.SearchAsync(
                Definition.SurveyFormVersionId,
                criteria,
                page,
                pageSize,
                HttpContext.RequestAborted);
            HasSearched = true;
        }

        return Page();
    }

    public string GetFirst(string name) =>
        SubmittedValues.TryGetValue(name, out string[]? values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;

    public bool IsSelected(string name, string value) =>
        SubmittedValues.TryGetValue(name, out string[]? values) && values.Contains(value, StringComparer.OrdinalIgnoreCase);

    private SearchCriterion? ParseCriterion(SearchFieldDefinition field, IFormCollection form)
    {
        QuestionDefinition question = field.QuestionDefinition;
        string key = $"f_{question.QuestionDefinitionId}";

        switch (question.QuestionType)
        {
            case QuestionType.ShortText:
            case QuestionType.LongText:
            {
                string value = form[key].FirstOrDefault()?.Trim() ?? string.Empty;
                return value.Length == 0 ? null : new SearchCriterion
                {
                    QuestionId = question.QuestionDefinitionId,
                    QuestionType = question.QuestionType,
                    Operator = field.DefaultOperator,
                    TextValue = value
                };
            }

            case QuestionType.SingleChoice:
            case QuestionType.MultipleChoice:
            {
                string[] rawValues = form[key]
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();
                int[] ids = rawValues
                    .Select(x => int.TryParse(x, out int id) ? id : 0)
                    .Where(x => x > 0)
                    .Distinct()
                    .ToArray();

                if (rawValues.Length == 0)
                {
                    return null;
                }

                HashSet<int> validIds = question.Options.Select(x => x.QuestionOptionId).ToHashSet();
                if (ids.Length != rawValues.Length || ids.Any(x => !validIds.Contains(x)))
                {
                    ModelState.AddModelError(string.Empty, $"'{field.DisplayLabel}' contains an invalid option.");
                    return null;
                }

                if (question.QuestionType == QuestionType.SingleChoice && ids.Length != 1)
                {
                    ModelState.AddModelError(string.Empty, $"'{field.DisplayLabel}' accepts one option.");
                    return null;
                }

                return new SearchCriterion
                {
                    QuestionId = question.QuestionDefinitionId,
                    QuestionType = question.QuestionType,
                    Operator = field.DefaultOperator,
                    OptionIds = ids
                };
            }

            case QuestionType.Date:
            {
                DateTime? from = ParseDate(form[$"{key}_from"].FirstOrDefault(), field.DisplayLabel);
                DateTime? to = ParseDate(form[$"{key}_to"].FirstOrDefault(), field.DisplayLabel);
                if (!from.HasValue && !to.HasValue)
                {
                    return null;
                }

                if (from.HasValue && to.HasValue && from > to)
                {
                    ModelState.AddModelError(string.Empty, $"'{field.DisplayLabel}' start date must not be after its end date.");
                }

                return new SearchCriterion
                {
                    QuestionId = question.QuestionDefinitionId,
                    QuestionType = question.QuestionType,
                    Operator = field.DefaultOperator,
                    FromDate = from,
                    ToDate = to
                };
            }

            case QuestionType.Number:
            {
                decimal? from = ParseNumber(form[$"{key}_from"].FirstOrDefault(), field.DisplayLabel);
                decimal? to = ParseNumber(form[$"{key}_to"].FirstOrDefault(), field.DisplayLabel);
                if (!from.HasValue && !to.HasValue)
                {
                    return null;
                }

                if (from.HasValue && to.HasValue && from > to)
                {
                    ModelState.AddModelError(string.Empty, $"'{field.DisplayLabel}' minimum must not exceed its maximum.");
                }

                return new SearchCriterion
                {
                    QuestionId = question.QuestionDefinitionId,
                    QuestionType = question.QuestionType,
                    Operator = field.DefaultOperator,
                    FromNumber = from,
                    ToNumber = to
                };
            }

            case QuestionType.Boolean:
            {
                string value = form[key].FirstOrDefault() ?? string.Empty;
                if (value.Length == 0)
                {
                    return null;
                }

                if (!bool.TryParse(value, out bool parsed))
                {
                    ModelState.AddModelError(string.Empty, $"'{field.DisplayLabel}' must be Yes, No, or Any.");
                    return null;
                }

                return new SearchCriterion
                {
                    QuestionId = question.QuestionDefinitionId,
                    QuestionType = question.QuestionType,
                    Operator = field.DefaultOperator,
                    BooleanValue = parsed
                };
            }

            default:
                return null;
        }
    }

    private DateTime? ParseDate(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        ModelState.AddModelError(string.Empty, $"'{label}' contains an invalid date.");
        return null;
    }

    private decimal? ParseNumber(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
        {
            return number;
        }

        ModelState.AddModelError(string.Empty, $"'{label}' contains an invalid number.");
        return null;
    }

    private async Task<bool> LoadDefinitionAsync(int searchDefinitionId)
    {
        SearchDefinition? definition = await db.SearchDefinitions
            .AsNoTracking()
            .Include(x => x.SurveyFormVersion)
                .ThenInclude(x => x.SurveyForm)
                    .ThenInclude(x => x.FormType)
            .Include(x => x.Fields)
                .ThenInclude(x => x.QuestionDefinition)
                    .ThenInclude(x => x.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.SearchDefinitionId == searchDefinitionId && x.IsActive);

        if (definition is null)
        {
            return false;
        }

        definition.Fields = definition.Fields.OrderBy(x => x.DisplayOrder).ToList();
        foreach (SearchFieldDefinition field in definition.Fields)
        {
            field.QuestionDefinition.Options = field.QuestionDefinition.Options
                .OrderBy(x => x.DisplayOrder)
                .ToList();
        }

        Definition = definition;
        return true;
    }
}
