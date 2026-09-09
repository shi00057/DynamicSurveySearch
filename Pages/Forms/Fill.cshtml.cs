using System.Globalization;
using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace DynamicSurveySearch.Pages.Forms;

public sealed class FillModel(SurveyDbContext db) : PageModel
{
    public SurveyFormVersion Version { get; private set; } = null!;
    public Guid? SubmittedPublicId { get; private set; }

    [BindProperty]
    public string? SubmittedBy { get; set; }

    public async Task<IActionResult> OnGetAsync(int versionId, Guid? submitted)
    {
        if (!await LoadVersionAsync(versionId))
        {
            return NotFound();
        }

        SubmittedPublicId = submitted;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int versionId)
    {
        if (!await LoadVersionAsync(versionId))
        {
            return NotFound();
        }

        if (Version.Status != FormVersionStatus.Published)
        {
            return BadRequest("Only published form versions can receive submissions.");
        }

        var result = new SurveyResult
        {
            SurveyFormVersionId = versionId,
            SubmittedBy = string.IsNullOrWhiteSpace(SubmittedBy) ? null : SubmittedBy.Trim()
        };

        foreach (QuestionDefinition question in Version.Questions)
        {
            string fieldName = $"q_{question.QuestionDefinitionId}";
            StringValues values = Request.Form[fieldName];
            string raw = values.FirstOrDefault()?.Trim() ?? string.Empty;
            bool hasValue = values.Any(x => !string.IsNullOrWhiteSpace(x));

            if (question.IsRequired && !hasValue)
            {
                ModelState.AddModelError(string.Empty, $"'{question.QuestionText}' is required.");
                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            var answer = new SurveyAnswer
            {
                QuestionDefinitionId = question.QuestionDefinitionId
            };

            switch (question.QuestionType)
            {
                case QuestionType.ShortText:
                case QuestionType.LongText:
                    answer.TextValue = raw;
                    break;

                case QuestionType.Number:
                    if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
                    {
                        ModelState.AddModelError(string.Empty, $"'{question.QuestionText}' must be a valid number.");
                        continue;
                    }
                    answer.NumberValue = number;
                    break;

                case QuestionType.Date:
                    if (!DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTime date))
                    {
                        ModelState.AddModelError(string.Empty, $"'{question.QuestionText}' must be a valid date.");
                        continue;
                    }
                    answer.DateValue = date;
                    break;

                case QuestionType.Boolean:
                    if (!bool.TryParse(raw, out bool booleanValue))
                    {
                        ModelState.AddModelError(string.Empty, $"'{question.QuestionText}' must be Yes or No.");
                        continue;
                    }
                    answer.BooleanValue = booleanValue;
                    break;

                case QuestionType.SingleChoice:
                case QuestionType.MultipleChoice:
                    int[] selectedIds = values
                        .Select(x => int.TryParse(x, out int id) ? id : 0)
                        .Where(x => x > 0)
                        .Distinct()
                        .ToArray();

                    HashSet<int> validIds = question.Options
                        .Select(x => x.QuestionOptionId)
                        .ToHashSet();

                    if (selectedIds.Length != values.Count || selectedIds.Any(x => !validIds.Contains(x)))
                    {
                        ModelState.AddModelError(string.Empty, $"'{question.QuestionText}' contains an invalid option.");
                        continue;
                    }

                    if (question.QuestionType == QuestionType.SingleChoice && selectedIds.Length != 1)
                    {
                        ModelState.AddModelError(string.Empty, $"'{question.QuestionText}' accepts one option.");
                        continue;
                    }

                    foreach (int optionId in selectedIds)
                    {
                        answer.SelectedOptions.Add(new SurveyAnswerOption
                        {
                            QuestionOptionId = optionId
                        });
                    }
                    break;
            }

            result.Answers.Add(answer);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        db.SurveyResults.Add(result);
        await db.SaveChangesAsync();

        return RedirectToPage(new { versionId, submitted = result.PublicId });
    }

    private async Task<bool> LoadVersionAsync(int versionId)
    {
        SurveyFormVersion? version = await db.SurveyFormVersions
            .AsNoTracking()
            .Include(x => x.SurveyForm)
                .ThenInclude(x => x.FormType)
            .Include(x => x.Questions)
                .ThenInclude(x => x.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.SurveyFormVersionId == versionId);

        if (version is null)
        {
            return false;
        }

        version.Questions = version.Questions.OrderBy(x => x.DisplayOrder).ToList();
        foreach (QuestionDefinition question in version.Questions)
        {
            question.Options = question.Options.OrderBy(x => x.DisplayOrder).ToList();
        }

        Version = version;
        return true;
    }
}
