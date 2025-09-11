using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuilderAssistantApi.Application.Ports;

namespace BuilderAssistantApi.Infrastructure.Adapters;

public class HttpImageStorageAdapter : IImageStorage
{
    private readonly Infrastructure.HttpClients.ImageStorageClient _client;

    public HttpImageStorageAdapter(Infrastructure.HttpClients.ImageStorageClient client)
    {
        _client = client;
    }

    public async Task<Uri> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Seek(0, SeekOrigin.Begin);

        using var contentPart = new StreamContent(ms);
        contentPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        using var form = new MultipartFormDataContent();
        form.Add(contentPart, "file", fileName);

        var resp = await _client.UploadAsync(form);
        var json = await resp.Content.ReadAsStringAsync(ct);

        // Expect JSON like { "url": "https://..." }
        using var doc = JsonDocument.Parse(json);
        var url = doc.RootElement.GetProperty("url").GetString();
        return new Uri(url!);
    }

    public async Task<Uri?> GetUrlAsync(long imageId, CancellationToken ct = default)
    {
        var resp = await _client.GetAsync(imageId);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var url = doc.RootElement.GetProperty("url").GetString();
        return url is null ? null : new Uri(url);
    }

    public async Task DeleteAsync(long imageId, CancellationToken ct = default)
    {
        await _client.DeleteAsync(imageId);
    }
}
