using DynamicSurveySearch.Data;
using DynamicSurveySearch.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DynamicSurveySearch.Pages.Forms;

public sealed class IndexModel(SurveyDbContext db) : PageModel
{
    public IReadOnlyList<SurveyForm> Forms { get; private set; } = Array.Empty<SurveyForm>();

    public async Task OnGetAsync()
    {
        Forms = await db.SurveyForms
            .AsNoTracking()
            .Include(x => x.FormType)
            .Include(x => x.Versions)
                .ThenInclude(x => x.SearchDefinitions)
            .AsSplitQuery()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}
