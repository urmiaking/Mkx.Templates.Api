namespace Mkx.Templates.Shared.Contracts;

/// <summary>Non-sensitive user fields available only to administrators.</summary>
public sealed record UserSummaryResponse(Guid Id, string? UserName, string? DisplayName);

/// <summary>Stable paginated user list for OpenAPI and generated clients.</summary>
public sealed record PagedUsersResponse(IReadOnlyList<UserSummaryResponse> Items, int Total, int Page, int PageSize);
