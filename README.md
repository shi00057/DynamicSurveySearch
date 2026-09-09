# Dynamic Survey Search Starter

A small ASP.NET Core 8 Razor Pages and SQL Server project that demonstrates:

- versioned, configurable survey forms;
- typed questions and reusable choice definitions;
- dynamic form rendering and result submission;
- creation of a search definition from an already published form;
- automatic search-control generation from question metadata;
- combined filtering in SQL Server through EF Core `Any` / SQL `EXISTS` semantics.

## Why More Than Three Tables Are Needed

The original starting concepts remain central:

| Original concept | Table | Responsibility |
| --- | --- | --- |
| Form type | `form_type` | Classifies forms, such as safety or customer satisfaction |
| Question list | `question_list` | Defines the questions in one form version |
| Survey result | `survey_result` | Stores the header for one submitted form |

A dynamic survey result cannot safely store all answers in the `survey_result` row. The starter therefore adds:

| Table | Responsibility |
| --- | --- |
| `survey_form` | Logical form identity |
| `survey_form_version` | Immutable draft/published form version |
| `question_option` | Stable options for single- and multiple-choice questions |
| `survey_answer` | One typed answer for one question in one result |
| `survey_answer_option` | Selected choice options for an answer |
| `search_definition` | A saved search panel created from a published form |
| `search_field_definition` | Questions selected for that panel, including label, operator, and order |

## Implemented Workflow

1. Open **Forms** and create a form.
2. Add questions to its draft version. For choice options, enter one per line as either `CODE|Display text` or only `Display text`.
3. Publish the form version. Published versions are read-only.
4. Fill the dynamically rendered form and submit results.
5. Choose **Create search** on the published form.
6. Select existing form questions and choose the operator used by each search field.
7. Open the generated search panel and combine any supplied criteria.
8. Open an individual matching result to review its answers.

Question order is never used as an identifier. Search fields reference stable question IDs and keys.

## Requirements

- .NET 8 SDK
- SQL Server, SQL Server Express, or SQL Server LocalDB
- Visual Studio 2022, VS Code, or the `dotnet` CLI

## Run Locally

The default connection string uses SQL Server LocalDB on Windows:

```json
"Server=(localdb)\\MSSQLLocalDB;Database=DynamicSurveySearchDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Update `ConnectionStrings:SurveyDatabase` in `appsettings.json` if another SQL Server instance is used.

Then run:

```powershell
dotnet restore
dotnet run
```

Open `https://localhost:7188` or the URL printed by `dotnet run`.

`Database:InitializeOnStartup` is `true` for the starter. The application uses `EnsureCreatedAsync`, creates the schema on first run, and inserts:

- one published safety-inspection form;
- typed example questions and options;
- one generated search definition;
- two sample survey results.

For a controlled environment, run `SQL/create-database.sql`, replace `EnsureCreatedAsync` with EF Core migrations, and disable automatic initialization.

## Query Design

Every generated search field is parsed into a typed `SearchCriterion`. Independent handlers then add conditions to one `IQueryable<SurveyResult>`:

```csharp
foreach (SearchCriterion criterion in criteria)
{
    query = ApplyCriterion(query, criterion);
}
```

EF Core translates answer checks based on `Any` into SQL `EXISTS` conditions. Multiple conditions therefore perform the database equivalent of intersecting submission-ID sets without loading separate arrays into application memory.

The initial rules are intentionally predictable:

- different search fields are combined with `AND`;
- `ContainsAny` uses OR semantics inside one multiple-choice field;
- `ContainsAll` requires every selected option;
- arbitrary nested AND/OR groups are not included.

## Important Production Additions

This is an architectural starter, not a production-complete application. Before production use, add:

- authentication, roles, and result-level authorization;
- EF Core migrations and deployment scripts;
- audit history for form/search-definition changes;
- a UI for creating a new version from a published version;
- concurrency handling and administrative edit/delete operations;
- anti-malware handling if file questions are added;
- full-text search if large-scale comment searching is required;
- tests against the intended SQL Server version and collation;
- a common `LocationId`, `AssignmentId`, or `EmployeeId` on `survey_result` when those are shared business fields rather than genuinely dynamic answers.

## Main Implementation Files

- `Models/SurveyEntities.cs` — entity and enum definitions
- `Data/SurveyDbContext.cs` — SQL Server mapping, relationships, and indexes
- `Data/DbInitializer.cs` — demo form, search, and results
- `Services/SurveySearchService.cs` — combined typed filtering
- `Pages/Forms` — form creation, design, publishing, and submission
- `Pages/Searches` — search creation and generated search panel
- `SQL/create-database.sql` — explicit SQL Server schema
