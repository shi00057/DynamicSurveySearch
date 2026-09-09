using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Pages.Forms;

public sealed class CreateModel(SurveyDbContext db) : PageModel
{
    [BindProperty]
    public FormInput Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> FormTypes { get; private set; } = Array.Empty<SelectListItem>();

    public async Task OnGetAsync() => await LoadFormTypesAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Code = NormalizeKey(Input.Code ?? string.Empty);

        if (Input.FormTypeId is null && string.IsNullOrWhiteSpace(Input.NewFormTypeName))
        {
            ModelState.AddModelError("Input.FormTypeId", "Select a form type or enter a new one.");
        }

        if (await db.SurveyForms.AnyAsync(x => x.Code == Input.Code))
        {
            ModelState.AddModelError("Input.Code", "This form code already exists.");
        }

        if (!ModelState.IsValid)
        {
            await LoadFormTypesAsync();
            return Page();
        }

        FormType formType;
        if (!string.IsNullOrWhiteSpace(Input.NewFormTypeName))
        {
            string typeName = Input.NewFormTypeName.Trim();
            formType = await db.FormTypes.FirstOrDefaultAsync(x => x.Name == typeName)
                ?? new FormType { Name = typeName };
        }
        else
        {
            formType = await db.FormTypes.FindAsync(Input.FormTypeId!.Value)
                ?? throw new InvalidOperationException("The selected form type no longer exists.");
        }

        var version = new SurveyFormVersion
        {
            VersionNumber = 1,
            Status = FormVersionStatus.Draft
        };

        var form = new SurveyForm
        {
            FormType = formType,
            Code = Input.Code,
            Name = (Input.Name ?? string.Empty).Trim(),
            Description = Input.Description?.Trim(),
            Versions = new List<SurveyFormVersion> { version }
        };

        db.SurveyForms.Add(form);
        await db.SaveChangesAsync();

        return RedirectToPage("Designer", new { versionId = version.SurveyFormVersionId });
    }

    private async Task LoadFormTypesAsync()
    {
        FormTypes = await db.FormTypes
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.FormTypeId.ToString()))
            .ToListAsync();
    }

    private static string NormalizeKey(string value)
    {
        string normalized = Regex.Replace(value.Trim().ToUpperInvariant(), "[^A-Z0-9]+", "_");
        return normalized.Trim('_');
    }

    public sealed class FormInput
    {
        public int? FormTypeId { get; set; }

        [MaxLength(100)]
        [Display(Name = "New form type (optional)")]
        public string? NewFormTypeName { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
