namespace PodBridge.Core.Notifications;

/// <summary>One Windows notification, reduced to what PodBridge needs.</summary>
/// <param name="Id">Windows' notification id (unique while the notification is on screen / in the action centre).</param>
/// <param name="AppId">Stable identifier of the app that sent it (its App User Model ID).</param>
/// <param name="AppName">Name shown to people, for example "Messages".</param>
public sealed record NotificationInfo(string Id, string AppId, string AppName, string Title, string Body);

/// <summary>An app that has sent a notification since PodBridge started listening.</summary>
public sealed class SeenApp
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
