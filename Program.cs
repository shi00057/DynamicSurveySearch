using DynamicSurveySearch.Data;
using DynamicSurveySearch.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<SurveyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SurveyDatabase")));
builder.Services.AddScoped<SurveySearchService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    using IServiceScope scope = app.Services.CreateScope();
    SurveyDbContext db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
    await DbInitializer.InitializeAsync(db);
}

app.Run();
