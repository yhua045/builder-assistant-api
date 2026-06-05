using BuilderAssistantApi.Application.Dtos;

namespace BuilderAssistantApi.Application.Interfaces;

public interface IRoleService
{
    /// <summary>Returns all role names configured in the system.</summary>
    Task<IReadOnlyList<string>> ListAllRolesAsync(CancellationToken ct = default);

    /// <summary>Returns the roles currently assigned to the user.</summary>
    Task<UserRolesDto?> GetUserRolesAsync(long userId, CancellationToken ct = default);

    /// <summary>Assigns a role to a user. Returns false if the role does not exist or the user does not exist.</summary>
    Task<bool> AssignRoleAsync(long userId, string roleName, CancellationToken ct = default);

    /// <summary>Removes a role from a user. Returns false if the user did not have the role or does not exist.</summary>
    Task<bool> RemoveRoleAsync(long userId, string roleName, CancellationToken ct = default);
}
