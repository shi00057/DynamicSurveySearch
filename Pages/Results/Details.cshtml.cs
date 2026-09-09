using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Pages.Results;

public sealed class DetailsModel(SurveyDbContext db) : PageModel
{
    public SurveyResult Result { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        SurveyResult? result = await db.SurveyResults
            .AsNoTracking()
            .Include(x => x.SurveyFormVersion)
                .ThenInclude(x => x.SurveyForm)
            .Include(x => x.Answers)
                .ThenInclude(x => x.QuestionDefinition)
            .Include(x => x.Answers)
                .ThenInclude(x => x.SelectedOptions)
                    .ThenInclude(x => x.QuestionOption)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.SurveyResultId == id);

        if (result is null)
        {
            return NotFound();
        }

        result.Answers = result.Answers
            .OrderBy(x => x.QuestionDefinition.DisplayOrder)
            .ToList();
        Result = result;
        return Page();
    }

    public static string FormatAnswer(SurveyAnswer answer) => answer.QuestionDefinition.QuestionType switch
    {
        QuestionType.ShortText or QuestionType.LongText => answer.TextValue ?? "—",
        QuestionType.Number => answer.NumberValue?.ToString() ?? "—",
        QuestionType.Date => answer.DateValue?.ToString("yyyy-MM-dd") ?? "—",
        QuestionType.Boolean => answer.BooleanValue switch { true => "Yes", false => "No", _ => "—" },
        QuestionType.SingleChoice or QuestionType.MultipleChoice =>
            string.Join(", ", answer.SelectedOptions
                .OrderBy(x => x.QuestionOption.DisplayOrder)
                .Select(x => x.QuestionOption.OptionText)),
        _ => "—"
    };
}
