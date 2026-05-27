namespace BuilderAssistantApi.Domain.Constants;

public static class ApplicationRoles
{
    public const string Admin          = "Admin";
    public const string SiteManager    = "SiteManager";
    public const string ProjectManager = "ProjectManager";
    public const string Owner          = "Owner";

    public static IReadOnlyList<string> All =>
        [Admin, SiteManager, ProjectManager, Owner];
}
