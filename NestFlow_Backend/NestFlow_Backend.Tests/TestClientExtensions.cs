using System.Net.Http.Json;

namespace NestFlow_Backend.Tests;

public static class TestClientExtensions
{
    /// <summary>以開發登入取得 Session Cookie，模擬一位 LINE 使用者。</summary>
    public static async Task LoginAsync(this HttpClient client, string externalSubject, string displayName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/dev/auth/login",
            new { externalSubject, displayName });

        response.EnsureSuccessStatusCode();
    }

    public static async Task<Guid> CreateWorkspaceAsync(this HttpClient client, string name, string type)
    {
        var response = await client.PostAsJsonAsync("/api/workspaces", new { name, type });
        response.EnsureSuccessStatusCode();

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        return workspace!.Id;
    }

    public static async Task<string> CreateInvitationAsync(this HttpClient client, Guid workspaceId)
    {
        var response = await client.PostAsync($"/api/workspaces/{workspaceId}/invitations", null);
        response.EnsureSuccessStatusCode();

        var invitation = await response.Content.ReadFromJsonAsync<InvitationResponse>();
        return invitation!.Code;
    }

    public record WorkspaceResponse(Guid Id, string Name, string Type, string MembershipType, bool IsDefault);

    public record InvitationResponse(string Code, DateTimeOffset ExpiresAt);

    public record MemberResponse(Guid UserId, string DisplayName, string MembershipType);

    public record CurrentUserResponse(Guid Id, string DisplayName, Guid? DefaultWorkspaceId, bool IsLineLinked);
}
