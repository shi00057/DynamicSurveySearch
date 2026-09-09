using System.ComponentModel.DataAnnotations;
using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using DynamicSurveySearch.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Pages.Searches;

public sealed class CreateModel(SurveyDbContext db) : PageModel
{
    public SurveyFormVersion Version { get; private set; } = null!;

    [BindProperty]
    public SearchInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int versionId)
    {
        if (!await LoadVersionAsync(versionId))
        {
            return NotFound();
        }

        if (Version.Status != FormVersionStatus.Published)
        {
            return BadRequest("A search can be created only from a published form version.");
        }

        Input.Name = $"{Version.SurveyForm.Name} Search";
        Input.Fields = Version.Questions.Select(question => new SearchFieldInput
        {
            Include = true,
            QuestionId = question.QuestionDefinitionId,
            DisplayLabel = question.QuestionText,
            DefaultOperator = SearchOperatorRules.GetDefault(question.QuestionType),
            DisplayOrder = question.DisplayOrder
        }).ToList();

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
            return BadRequest("A search can be created only from a published form version.");
        }

        Dictionary<int, QuestionDefinition> questions = Version.Questions
            .ToDictionary(x => x.QuestionDefinitionId);
        List<SearchFieldInput> selectedFields = Input.Fields.Where(x => x.Include).ToList();

        if (selectedFields.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Select at least one search field.");
        }

        foreach (SearchFieldInput field in selectedFields)
        {
            if (!questions.TryGetValue(field.QuestionId, out QuestionDefinition? question))
            {
                ModelState.AddModelError(string.Empty, "The search contains a question that does not belong to this form version.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(field.DisplayLabel))
            {
                ModelState.AddModelError(string.Empty, $"A display label is required for '{question.QuestionText}'.");
            }

            if (!SearchOperatorRules.IsAllowed(question.QuestionType, field.DefaultOperator))
            {
                ModelState.AddModelError(string.Empty, $"The selected operator is invalid for '{question.QuestionText}'.");
            }
        }

        string name = (Input.Name ?? string.Empty).Trim();
        if (await db.SearchDefinitions.AnyAsync(x =>
                x.SurveyFormVersionId == versionId && x.Name == name))
        {
            ModelState.AddModelError("Input.Name", "A search with this name already exists for the form version.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var definition = new SearchDefinition
        {
            SurveyFormVersionId = versionId,
            Name = name,
            Fields = selectedFields.Select(field => new SearchFieldDefinition
            {
                QuestionDefinitionId = field.QuestionId,
                DisplayLabel = (field.DisplayLabel ?? string.Empty).Trim(),
                DefaultOperator = field.DefaultOperator,
                DisplayOrder = field.DisplayOrder
            }).ToList()
        };

        db.SearchDefinitions.Add(definition);
        await db.SaveChangesAsync();

        return RedirectToPage("Run", new { searchDefinitionId = definition.SearchDefinitionId });
    }

    public IReadOnlyList<SearchOperator> AllowedOperators(int questionId)
    {
        QuestionDefinition question = Version.Questions.Single(x => x.QuestionDefinitionId == questionId);
        return SearchOperatorRules.GetAllowed(question.QuestionType);
    }

    private async Task<bool> LoadVersionAsync(int versionId)
    {
        SurveyFormVersion? version = await db.SurveyFormVersions
            .AsNoTracking()
            .Include(x => x.SurveyForm)
                .ThenInclude(x => x.FormType)
            .Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.SurveyFormVersionId == versionId);

        if (version is null)
        {
            return false;
        }

        version.Questions = version.Questions.OrderBy(x => x.DisplayOrder).ToList();
        Version = version;
        return true;
    }

    public sealed class SearchInput
    {
        [Required, MaxLength(200)]
        [Display(Name = "Search name")]
        public string Name { get; set; } = string.Empty;

        public List<SearchFieldInput> Fields { get; set; } = new();
    }

    public sealed class SearchFieldInput
    {
        public bool Include { get; set; }
        public int QuestionId { get; set; }
        public string DisplayLabel { get; set; } = string.Empty;
        public SearchOperator DefaultOperator { get; set; }
        public int DisplayOrder { get; set; }
    }
}
