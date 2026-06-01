namespace BuilderAssistantApi.Domain.Constants;

public static class ApplicationPolicies
{
    /// <summary>
    /// Grants the ability to create, update, and delete role-feature entitlements
    /// via <c>FeatureFlagsController</c>.
    /// Satisfied when the caller is authenticated and holds the
    /// <see cref="ApplicationRoles.Admin"/> role.
    /// </summary>
    public const string ManageFeatureFlags = "ManageFeatureFlags";

    /// <summary>
    /// Grants the ability to view, assign, and remove roles from users
    /// via <c>UsersController</c>.
    /// Satisfied when the caller is authenticated and holds the
    /// <see cref="ApplicationRoles.Admin"/> role.
    /// </summary>
    public const string ManageUserRoles = "ManageUserRoles";
}
