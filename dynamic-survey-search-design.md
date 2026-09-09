# Dynamic Search Panels for Configurable Surveys

## 1. Purpose

This design supports a survey system in which administrators can create different forms with different questions, answer options, and question order. After a form has been designed and published, an administrator can create a search definition for that form. The system then generates a search panel for ordinary users from the selected questions.

The search UI and query logic are metadata-driven. No form-specific search page is required.

## 2. Core Workflow

1. A form designer creates and publishes a survey version.
2. An administrator selects **Create Search** for that published form.
3. The system loads the form's existing questions.
4. The administrator selects which questions may be used as search criteria.
5. For each selected question, the system proposes a search control and valid operators based on the question type.
6. The administrator can adjust the label, default operator, display order, and whether the criterion is optional.
7. The search definition is saved against the published survey version.
8. When an ordinary user opens the search page, the system renders the configured controls dynamically.
9. The completed search request is sent to the API as structured JSON.
10. The API validates the filters, builds one database query, and returns a paged result set.

Question position must never be used as an identifier. Search fields reference stable `QuestionId` or `QuestionKey` values, so changing question order has no effect.

## 3. Search-Control Mapping

| Survey question type | Generated search control | Typical operators |
| --- | --- | --- |
| Single choice | Dropdown | Equals, Not Equals, Any Of |
| Multiple choice | Multi-select | Contains Any, Contains All |
| Yes/No | Yes / No / Any selector | Equals |
| Date | Date picker or date range | On, Before, After, Between |
| Number | Number input or range | Equals, Greater Than, Less Than, Between |
| Short text | Text input | Contains, Starts With, Exact Match |
| Long text | Keyword input | Contains |
| File or signature | Checkbox | Exists, Does Not Exist |

Choice controls reuse the options already defined for the selected survey question. They should not derive their option lists from historical responses.

For text search, users enter ordinary text. The server creates a parameterized `LIKE` expression such as `LIKE '%' + @Keyword + '%'`; users do not enter SQL wildcard syntax.

## 4. Suggested Data Model

```text
Survey
SurveyVersion
QuestionDefinition
QuestionOption

SearchDefinition
SearchFieldDefinition

SurveySubmission
SurveyAnswer
SurveyAnswerOption
```

Example search-definition tables:

```text
SearchDefinition
----------------
SearchDefinitionId
SurveyVersionId
Name
IsActive

SearchFieldDefinition
---------------------
SearchFieldDefinitionId
SearchDefinitionId
QuestionId
ControlType
DefaultOperator
AllowedOperators
DisplayLabel
DisplayOrder
```

The system should keep typed answer values rather than storing every answer only as text:

```text
SurveyAnswer
------------
SubmissionId
QuestionId
TextValue
NumberValue
DateValue
BooleanValue
```

Selected options for single-choice and multiple-choice questions should be stored by stable `OptionId` in `SurveyAnswerOption`.

If fields such as `LocationId`, `EmployeeId`, `AssignmentId`, status, and submission date are shared across most forms, they should normally be first-class columns on `SurveySubmission`. They can still appear in the form, but they should not exist only as dynamic answers. This makes common filtering, authorization, indexing, reporting, and joins much easier.

## 5. Search Definition Returned to the UI

The API can return a UI-neutral search schema:

```json
{
  "searchDefinitionId": 24,
  "surveyVersionId": 12,
  "fields": [
    {
      "questionId": 101,
      "questionKey": "LOCATION",
      "label": "Location",
      "controlType": "singleChoice",
      "defaultOperator": "equals",
      "allowedOperators": ["equals", "anyOf"],
      "options": [
        { "optionId": 501, "label": "Station 12" },
        { "optionId": 502, "label": "Station 18" }
      ]
    },
    {
      "questionId": 108,
      "questionKey": "INSPECTION_DATE",
      "label": "Inspection Date",
      "controlType": "date",
      "defaultOperator": "between"
    },
    {
      "questionId": 110,
      "questionKey": "COMMENTS",
      "label": "Comments Contain",
      "controlType": "text",
      "defaultOperator": "contains"
    }
  ]
}
```

The client uses a component registry rather than form-specific code:

```typescript
const searchComponents = {
  singleChoice: SingleChoiceSearch,
  multipleChoice: MultipleChoiceSearch,
  date: DateSearch,
  number: NumberSearch,
  text: TextSearch,
  boolean: BooleanSearch
};

function renderSearchField(field: SearchFieldDefinition) {
  const Component = searchComponents[field.controlType];
  return <Component key={field.questionId} definition={field} />;
}
```

The same pattern can be implemented with Razor partials or View Components if React is not used.

## 6. Search Request

The browser sends structured filters, not SQL:

```json
{
  "searchDefinitionId": 24,
  "surveyVersionId": 12,
  "matchMode": "all",
  "page": 1,
  "pageSize": 30,
  "filters": [
    {
      "questionId": 101,
      "operator": "equals",
      "optionIds": [501]
    },
    {
      "questionId": 108,
      "operator": "between",
      "from": "2026-09-01",
      "to": "2026-09-30"
    },
    {
      "questionId": 110,
      "operator": "contains",
      "value": "equipment"
    }
  ]
}
```

Before querying, the API must verify that:

- the search definition belongs to the requested survey version;
- every question belongs to that version and is enabled in the search definition;
- the operator is valid for the question type;
- submitted option IDs belong to the question;
- values have the expected data type;
- the current user is authorized to see the requested submissions.

## 7. Query Composition

Each question type should have an independent filter handler, but all handlers should contribute to one database query:

```text
SingleChoiceFilterHandler
MultipleChoiceFilterHandler
DateFilterHandler
NumberFilterHandler
TextFilterHandler
BooleanFilterHandler
```

Example EF Core flow:

```csharp
IQueryable<SurveySubmission> query = db.SurveySubmissions
    .Where(x => x.SurveyVersionId == request.SurveyVersionId);

foreach (SearchFilter filter in request.Filters)
{
    query = filterDispatcher.Apply(query, filter);
}

int totalCount = await query.CountAsync();

List<SurveySearchResult> results = await query
    .OrderByDescending(x => x.SubmittedDate)
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .Select(x => new SurveySearchResult
    {
        SubmissionId = x.SubmissionId,
        SubmittedDate = x.SubmittedDate,
        LocationId = x.LocationId
    })
    .ToListAsync();
```

Example single-choice handler:

```csharp
public IQueryable<SurveySubmission> ApplySingleChoice(
    IQueryable<SurveySubmission> query,
    SearchFilter filter)
{
    int questionId = filter.QuestionId;
    int optionId = filter.OptionIds.Single();

    return query.Where(submission =>
        submission.Answers.Any(answer =>
            answer.QuestionId == questionId &&
            answer.SelectedOptions.Any(option =>
                option.OptionId == optionId)));
}
```

Example text handler:

```csharp
public IQueryable<SurveySubmission> ApplyTextContains(
    IQueryable<SurveySubmission> query,
    SearchFilter filter)
{
    string pattern = $"%{EscapeLikePattern(filter.Value)}%";

    return query.Where(submission =>
        submission.Answers.Any(answer =>
            answer.QuestionId == filter.QuestionId &&
            answer.TextValue != null &&
            EF.Functions.Like(answer.TextValue, pattern, "\\")));
}
```

EF Core will normally translate repeated `Any` conditions into SQL `EXISTS` clauses. Multiple `AND EXISTS` clauses provide the same logical intersection as producing a separate array of submission IDs for every criterion, while allowing SQL Server to optimize, sort, count, and paginate the complete query.

The alternative of loading multiple ID arrays into .NET and intersecting them is acceptable only for a small prototype. It introduces extra database round trips, transfers unnecessary IDs, and makes pagination, ordering, counting, and consistent reads more difficult.

## 8. Multiple-Choice Semantics

Multiple-choice search must define its matching behavior clearly:

- **Contains Any:** the submitted answer contains at least one selected option.
- **Contains All:** the submitted answer contains every selected option.
- **Exact Set:** the submitted answer contains exactly the selected options and no others.

For the initial implementation, support `Contains Any` and optionally `Contains All`. `Exact Set` can be added later if a real requirement appears.

## 9. Recommended Initial Scope

To keep the first version predictable:

- combine different search fields with `AND`;
- use `OR` inside a multi-select field through `Contains Any`;
- optionally allow `Contains All` for multi-select fields;
- do not initially support nested user-defined AND/OR groups;
- bind each search definition to one published survey version;
- use stable question and option IDs rather than labels or display order;
- execute filtering, sorting, counting, and paging in SQL Server;
- use parameterized EF Core queries or a validated stored procedure, never raw SQL from the client.

## 10. Versioning

A published survey version should be immutable. If questions or options change, create a new version.

A search definition is initially bound to a specific `SurveyVersionId`. When a new form version is published, the system can offer a migration screen that maps fields by stable `QuestionKey` or `QuestionConceptId`. This allows a search such as `LOCATION` to survive question reordering or wording changes without incorrectly treating unrelated questions as equivalent.

## Conclusion

This is a mature and maintainable design. Form authors define the survey first; search administrators then create a search configuration from that existing form. The UI is generated from question metadata, while independent server-side filter handlers compose one parameterized database query. This provides flexible form-specific search panels without creating separate pages or stored procedures for every survey type.
