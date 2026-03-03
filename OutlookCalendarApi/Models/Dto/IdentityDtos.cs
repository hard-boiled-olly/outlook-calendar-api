namespace OutlookCalendarApi.Models.Dto;

// POST /api/identities/refine
public record CreateIdentityRequest(string AreaOfLife, string RoughStatement);

// POST /api/identities
public record ConfirmIdentityRequest(string AreaOfLife, string Statement);

// Response from /api/identities/refine
public record IdentityRefinementResult(string RefinedStatement, string Explanation);

// GET /api/identities — list item
public record IdentityListItem(
    Guid Id,
    string AreaOfLife,
    string Statement,
    string Status,
    DateTime CreatedAt,
    SummitSummary? ActiveSummit
);

public record SummitSummary(Guid Id, string Description, string Status);

// GET /api/identities/{id} — detail
public record IdentityDetail(
    Guid Id,
    string AreaOfLife,
    string Statement,
    string Status,
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
