using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsTelegramOverlay.Bot.Configuration;
using ObsTelegramOverlay.Bot.Models;

namespace ObsTelegramOverlay.Bot.Services;

public sealed class OverlayRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly BlobServiceClient _blobServiceClient;
    private readonly BotOptions _options;
    private readonly ILogger<OverlayRegistryService> _logger;

    public OverlayRegistryService(
        BlobServiceClient blobServiceClient,
        IOptions<BotOptions> options,
        ILogger<OverlayRegistryService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OverlayRecord> CreateOverlayAsync(long chatId, string? chatTitle, CancellationToken cancellationToken)
    {
        var overlayId = Guid.NewGuid().ToString("N");
        var publicUrl = BuildOverlayUrl(overlayId);
        var record = new OverlayRecord
        {
            OverlayId = overlayId,
            ChatId = chatId,
            ChatTitle = chatTitle,
            PublicUrl = publicUrl,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = OverlayStatuses.Active
        };

        await EnsureContainersAsync(cancellationToken);
        await WriteOverlayRecordAsync(record, cancellationToken);

        var chatIndex = await LoadChatOverlayIndexAsync(chatId, cancellationToken);
        if (!chatIndex.Contains(overlayId, StringComparer.Ordinal))
        {
            chatIndex.Add(overlayId);
            await WriteChatOverlayIndexAsync(chatId, chatIndex, cancellationToken);
        }

        await PublishStaticOverlayAsync(record, cancellationToken);

        _logger.LogInformation("Created overlay {OverlayId} for chat {ChatId}", overlayId, chatId);

        return record;
    }

    public async Task<IReadOnlyList<OverlayRecord>> ListActiveOverlaysForChatAsync(long chatId, CancellationToken cancellationToken)
    {
        var overlayIds = await LoadChatOverlayIndexAsync(chatId, cancellationToken);
        var overlays = new List<OverlayRecord>(overlayIds.Count);

        foreach (var overlayId in overlayIds)
        {
            var record = await GetOverlayAsync(overlayId, cancellationToken);
            if (record is { Status: OverlayStatuses.Active })
            {
                overlays.Add(record);
            }
        }

        return overlays;
    }

    public async Task<IReadOnlyList<OverlayRecord>> ListOverlaysForChatAsync(long chatId, CancellationToken cancellationToken)
    {
        var overlayIds = await LoadChatOverlayIndexAsync(chatId, cancellationToken);
        var overlays = new List<OverlayRecord>(overlayIds.Count);

        foreach (var overlayId in overlayIds)
        {
            var record = await GetOverlayAsync(overlayId, cancellationToken);
            if (record is not null)
            {
                overlays.Add(record);
            }
        }

        return overlays;
    }

    public async Task<OverlayRecord?> GetOverlayAsync(string overlayId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(overlayId))
        {
            return null;
        }

        var client = GetControlContainerClient().GetBlobClient(GetOverlayRecordBlobName(overlayId));

        try
        {
            var download = await client.DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToObjectFromJson<OverlayRecord>(JsonOptions);
        }
        catch (RequestFailedException exception) when (exception.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<OverlayRecord?> RevokeOverlayAsync(string overlayId, string reason, CancellationToken cancellationToken)
    {
        var existing = await GetOverlayAsync(overlayId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var revokedRecord = new OverlayRecord
        {
            OverlayId = existing.OverlayId,
            ChatId = existing.ChatId,
            ChatTitle = existing.ChatTitle,
            PublicUrl = existing.PublicUrl,
            CreatedUtc = existing.CreatedUtc,
            RevokedUtc = DateTimeOffset.UtcNow,
            Status = OverlayStatuses.Revoked,
            RevokedReason = reason
        };

        await WriteOverlayRecordAsync(revokedRecord, cancellationToken);

        var chatIndex = await LoadChatOverlayIndexAsync(existing.ChatId, cancellationToken);
        if (chatIndex.RemoveAll(item => string.Equals(item, overlayId, StringComparison.Ordinal)) > 0)
        {
            await WriteChatOverlayIndexAsync(existing.ChatId, chatIndex, cancellationToken);
        }

        await DeleteStaticOverlayAsync(overlayId, cancellationToken);

        _logger.LogInformation("Revoked overlay {OverlayId} for chat {ChatId}", overlayId, existing.ChatId);

        return revokedRecord;
    }

    private async Task EnsureContainersAsync(CancellationToken cancellationToken)
    {
        await GetControlContainerClient().CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await GetStaticContainerClient().CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task PublishStaticOverlayAsync(OverlayRecord record, CancellationToken cancellationToken)
    {
        var container = GetStaticContainerClient();
        var indexBlob = container.GetBlobClient($"overlays/{record.OverlayId}/index.html");
        var configBlob = container.GetBlobClient($"overlays/{record.OverlayId}/config.json");

        await indexBlob.UploadAsync(
            BinaryData.FromString(BuildOverlayIndexHtml(record)),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "text/html; charset=utf-8" }
            },
            cancellationToken);

        var configPayload = JsonSerializer.Serialize(new
        {
            overlayId = record.OverlayId,
            negotiateUrl = BuildNegotiateUrl(record.OverlayId),
            siteTitle = _options.SiteTitle,
            ttsMode = "browser"
        }, JsonOptions);

        await configBlob.UploadAsync(
            BinaryData.FromString(configPayload),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json; charset=utf-8" }
            },
            cancellationToken);
    }

    private async Task DeleteStaticOverlayAsync(string overlayId, CancellationToken cancellationToken)
    {
        var container = GetStaticContainerClient();
        await container.DeleteBlobIfExistsAsync($"overlays/{overlayId}/index.html", cancellationToken: cancellationToken);
        await container.DeleteBlobIfExistsAsync($"overlays/{overlayId}/config.json", cancellationToken: cancellationToken);
    }

    private async Task<List<string>> LoadChatOverlayIndexAsync(long chatId, CancellationToken cancellationToken)
    {
        var client = GetControlContainerClient().GetBlobClient(GetChatIndexBlobName(chatId));

        try
        {
            var download = await client.DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToObjectFromJson<List<string>>(JsonOptions) ?? new List<string>();
        }
        catch (RequestFailedException exception) when (exception.Status == (int)HttpStatusCode.NotFound)
        {
            return new List<string>();
        }
    }

    private Task WriteChatOverlayIndexAsync(long chatId, List<string> overlayIds, CancellationToken cancellationToken)
    {
        var client = GetControlContainerClient().GetBlobClient(GetChatIndexBlobName(chatId));
        var payload = JsonSerializer.Serialize(overlayIds, JsonOptions);
        return client.UploadAsync(
            BinaryData.FromString(payload),
            overwrite: true,
            cancellationToken: cancellationToken);
    }

    private Task WriteOverlayRecordAsync(OverlayRecord record, CancellationToken cancellationToken)
    {
        var client = GetControlContainerClient().GetBlobClient(GetOverlayRecordBlobName(record.OverlayId));
        var payload = JsonSerializer.Serialize(record, JsonOptions);
        return client.UploadAsync(BinaryData.FromString(payload), overwrite: true, cancellationToken: cancellationToken);
    }

    private BlobContainerClient GetStaticContainerClient() => _blobServiceClient.GetBlobContainerClient(_options.OverlayStaticContainer);

    private BlobContainerClient GetControlContainerClient() => _blobServiceClient.GetBlobContainerClient(_options.OverlayControlContainer);

    private static string GetOverlayRecordBlobName(string overlayId) => $"overlays/{overlayId}.json";

    private static string GetChatIndexBlobName(long chatId) => $"chats/{chatId}.json";

    private string BuildOverlayUrl(string overlayId)
    {
        var baseUrl = _options.OverlayBaseUrl.TrimEnd('/');
        return $"{baseUrl}/overlays/{overlayId}/";
    }

    private string BuildNegotiateUrl(string overlayId)
    {
        var baseUrl = _options.OverlayApiBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/overlay/{overlayId}/negotiate";
    }

    private string BuildOverlayIndexHtml(OverlayRecord record)
    {
        var siteTitle = WebUtility.HtmlEncode(_options.SiteTitle);
        var assetsBasePath = _options.OverlayAssetsBasePath.TrimEnd('/');
        var overlayId = WebUtility.HtmlEncode(record.OverlayId);

        return $"""
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{siteTitle}</title>
    <meta name="overlay-id" content="{overlayId}" />
    <link rel="stylesheet" href="{assetsBasePath}/overlay.css" />
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="{assetsBasePath}/overlay.js"></script>
  </body>
</html>
""";
    }
}