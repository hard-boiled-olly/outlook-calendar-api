namespace OutlookCalendarApi.Models.Dto;

// POST /api/identities/refine
public record CreateIdentityRequest(string RoughStatement);

// POST /api/identities
public record ConfirmIdentityRequest(string Statement);

// Response from /api/identities/refine
public record IdentityRefinementResult(string RefinedStatement, string Explanation);

// GET /api/identities — list item
public record IdentityListItem(
    Guid Id,
    string Statement,
    bool Active,
    DateTime CreatedAt,
    SummitSummary? ActiveSummit
);

public record SummitSummary(Guid Id, string Description, string Status,
    ActiveMilestoneSummary? ActiveMilestone);

public record ActiveMilestoneSummary(
    Guid Id,
    string Description,
    DateOnly TargetDate,
    int SortOrder,
    string Status,
    ActiveSprintSummary? ActiveSprint
);

public record ActiveSprintSummary(Guid Id, int SprintNumber);

// GET /api/identities/{id} — detail
public record IdentityDetail(
    Guid Id,
    string Statement,
    bool Active,
    DateTime CreatedAt,
    List<SummitDetail> Summits
);

public record SummitDetail(
    Guid Id,
    string Description,
    string ProofCriteria,
    DateOnly? TargetDate,
    string Status,
    DateTime CreatedAt
);
