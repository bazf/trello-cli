using TrelloCli.Models;

namespace TrelloCli.Services;

/// <summary>Workspaces. Trello's API calls them organizations.</summary>
public partial class TrelloApiService
{
    public Task<ApiResponse<List<Organization>>> GetOrganizationsAsync() =>
        GetListAsync<Organization>(BuildUrl("/members/me/organizations"), notFoundMessage: null);

    public Task<ApiResponse<Organization>> GetOrganizationAsync(string organizationId) =>
        GetObjectAsync<Organization>(
            BuildUrl($"/organizations/{organizationId}"),
            "Workspace not found",
            "NOT_FOUND",
            "Workspace not found");

    public Task<ApiResponse<List<Board>>> GetOrganizationBoardsAsync(string organizationId) =>
        GetListAsync<Board>(BuildUrl($"/organizations/{organizationId}/boards"), "Workspace not found");

    public Task<ApiResponse<List<Member>>> GetOrganizationMembersAsync(string organizationId) =>
        GetListAsync<Member>(BuildUrl($"/organizations/{organizationId}/members"), "Workspace not found");
}
