using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using SpazaSure.NotificationService.Data;

namespace SpazaSure.NotificationService.Services;

/// <summary>
/// Sends phone notifications through Firebase Cloud Messaging. Needs the Firebase service
/// account key file at Firebase:CredentialsPath; without it push is off and in-app
/// notifications keep working as before.
/// </summary>
public sealed class PushSender
{
    /// <summary>Android channel created by the app (MainActivity.kt) so pushes pop up on screen.</summary>
    private const string AndroidChannelId = "spazasure_alerts";

    private readonly FirebaseMessaging? _messaging;
    private readonly ILogger<PushSender> _logger;

    public PushSender(IConfiguration config, ILogger<PushSender> logger)
    {
        _logger = logger;
        var path = config["Firebase:CredentialsPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("Push notifications are off: no Firebase service account key at '{Path}'.", path);
            return;
        }
        try
        {
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(path),
            });
            _messaging = FirebaseMessaging.GetMessaging(app);
            _logger.LogInformation("Push notifications are on (Firebase project {Project}).", app.Options.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push notifications are off: the Firebase service account key could not be loaded.");
        }
    }

    public bool Enabled => _messaging is not null;

    /// <summary>Sends to every phone the user is signed in on, and forgets phones Firebase says are gone.</summary>
    public async Task SendAsync(NotificationDbContext db, string userId, string title, string body,
        string type, string? referenceId, bool highPriority, CancellationToken ct = default)
    {
        if (_messaging is null) return;

        var tokens = await db.DeviceTokens.Where(d => d.UserId == userId).Select(d => d.Token).ToListAsync(ct);
        if (tokens.Count == 0) return;

        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
            Data = new Dictionary<string, string>
            {
                ["type"] = type,
                ["referenceId"] = referenceId ?? "",
            },
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification
                {
                    ChannelId = AndroidChannelId,
                    Color = "#25449A",
                    Sound = "default",
                    Priority = highPriority ? NotificationPriority.MAX : NotificationPriority.HIGH,
                },
            },
        };

        try
        {
            var result = await _messaging.SendEachForMulticastAsync(message, ct);
            if (result.FailureCount == 0) return;

            // Uninstalled apps and signed-out phones: stop sending to them
            var dead = new List<string>();
            for (var i = 0; i < result.Responses.Count; i++)
            {
                var error = result.Responses[i].Exception?.MessagingErrorCode;
                if (error is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument or MessagingErrorCode.SenderIdMismatch)
                    dead.Add(tokens[i]);
            }
            if (dead.Count > 0)
                await db.DeviceTokens.Where(d => dead.Contains(d.Token)).ExecuteDeleteAsync(ct);
            _logger.LogWarning("Push to {UserId}: {Failed} of {Total} failed ({Removed} dead tokens removed).",
                userId, result.FailureCount, tokens.Count, dead.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push to {UserId} failed.", userId);
        }
    }
}
