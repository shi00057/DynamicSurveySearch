using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Pages.Forms;

public sealed class DesignerModel(SurveyDbContext db) : PageModel
{
    public SurveyFormVersion Version { get; private set; } = null!;

    [BindProperty]
    public QuestionInput NewQuestion { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int versionId)
    {
        if (!await LoadVersionAsync(versionId))
        {
            return NotFound();
        }

        NewQuestion.DisplayOrder = Version.Questions.Any()
            ? Version.Questions.Max(x => x.DisplayOrder) + 10
            : 10;
        return Page();
    }

    public async Task<IActionResult> OnPostAddQuestionAsync(int versionId)
    {
        if (!await LoadVersionAsync(versionId))
        {
            return NotFound();
        }

        if (Version.Status != FormVersionStatus.Draft)
        {
            ModelState.AddModelError(string.Empty, "Published form versions cannot be edited.");
        }

        NewQuestion.QuestionKey = NormalizeKey(NewQuestion.QuestionKey ?? string.Empty);
        if (Version.Questions.Any(x => x.QuestionKey == NewQuestion.QuestionKey))
        {
            ModelState.AddModelError("NewQuestion.QuestionKey", "This question key already exists in the form version.");
        }

        bool choiceQuestion = NewQuestion.QuestionType is QuestionType.SingleChoice or QuestionType.MultipleChoice;
        string[] optionLines = SplitLines(NewQuestion.Options);
        if (choiceQuestion && optionLines.Length == 0)
        {
            ModelState.AddModelError("NewQuestion.Options", "A choice question requires at least one option.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var question = new QuestionDefinition
        {
            SurveyFormVersionId = versionId,
            QuestionKey = NewQuestion.QuestionKey,
            QuestionText = (NewQuestion.QuestionText ?? string.Empty).Trim(),
            HelpText = NewQuestion.HelpText?.Trim(),
            QuestionType = NewQuestion.QuestionType,
            IsRequired = NewQuestion.IsRequired,
            DisplayOrder = NewQuestion.DisplayOrder
        };

        if (choiceQuestion)
        {
            int order = 10;
            var usedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in optionLines)
            {
                string[] parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
                string text = parts.Length == 2 ? parts[1] : parts[0];
                string baseValue = parts.Length == 2 ? NormalizeKey(parts[0]) : NormalizeKey(text);
                string value = MakeUnique(baseValue, usedValues);

                question.Options.Add(new QuestionOption
                {
                    OptionValue = value,
                    OptionText = text,
                    DisplayOrder = order
                });
                order += 10;
            }
        }

        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return RedirectToPage(new { versionId });
    }

    public async Task<IActionResult> OnPostPublishAsync(int versionId)
    {
        SurveyFormVersion? version = await db.SurveyFormVersions
            .Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.SurveyFormVersionId == versionId);

        if (version is null)
        {
            return NotFound();
        }

        if (version.Status != FormVersionStatus.Draft)
        {
            return RedirectToPage(new { versionId });
        }

        if (version.Questions.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one question before publishing.");
            await LoadVersionAsync(versionId);
            return Page();
        }

        version.Status = FormVersionStatus.Published;
        version.PublishedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { versionId });
    }

    private async Task<bool> LoadVersionAsync(int versionId)
    {
        SurveyFormVersion? version = await db.SurveyFormVersions
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

    private static string[] SplitLines(string? value) =>
        (value ?? string.Empty).Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeKey(string value)
    {
        string normalized = Regex.Replace(value.Trim().ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "ITEM" : normalized;
    }

    private static string MakeUnique(string baseValue, ISet<string> used)
    {
        string value = baseValue;
        int suffix = 2;
        while (!used.Add(value))
        {
            value = $"{baseValue}_{suffix++}";
        }
        return value;
    }

    public sealed class QuestionInput
    {
        [Required, MaxLength(100)]
        [Display(Name = "Question key")]
        public string QuestionKey { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        [Display(Name = "Question text")]
        public string QuestionText { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Help text")]
        public string? HelpText { get; set; }

        [Display(Name = "Question type")]
        public QuestionType QuestionType { get; set; } = QuestionType.ShortText;

        [Display(Name = "Required")]
        public bool IsRequired { get; set; }

        [Range(1, 100000)]
        [Display(Name = "Display order")]
        public int DisplayOrder { get; set; } = 10;

        [Display(Name = "Choice options")]
        public string? Options { get; set; }
    }
}
