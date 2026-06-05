using Microsoft.AspNetCore.Authorization;

namespace BuilderAssistantApi.Api.Authorization;

/// <summary>
/// Marker requirement for the <c>ManageFeatureFlags</c> policy.
/// Satisfied when the caller holds the <c>Admin</c> role.
/// </summary>
public sealed class ManageFeatureFlagsRequirement : IAuthorizationRequirement { }
