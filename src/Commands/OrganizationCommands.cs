using TrelloCli.Models;
using TrelloCli.Services;

namespace TrelloCli.Commands;

public class OrganizationCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetOrganizationsAsync() => Write(await Api.GetOrganizationsAsync());

    public async Task GetOrganizationAsync(string organizationId)
    {
        if (!RequireOrganization(organizationId)) return;

        Write(await Api.GetOrganizationAsync(organizationId));
    }

    public async Task GetOrganizationBoardsAsync(string organizationId)
    {
        if (!RequireOrganization(organizationId)) return;

        Write(await Api.GetOrganizationBoardsAsync(organizationId));
    }

    public async Task GetOrganizationMembersAsync(string organizationId)
    {
        if (!RequireOrganization(organizationId)) return;

        Write(await Api.GetOrganizationMembersAsync(organizationId));
    }

    private bool RequireOrganization(string organizationId)
    {
        if (!string.IsNullOrEmpty(organizationId)) return true;

        Write(ApiResponse<object>.Fail("Workspace ID required", "MISSING_PARAM"));
        return false;
    }
}
