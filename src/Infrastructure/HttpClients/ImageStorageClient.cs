using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BuilderAssistantApi.Infrastructure.HttpClients;

public class ImageStorageClient
{
    private readonly HttpClient _http;

    public ImageStorageClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<HttpResponseMessage> UploadAsync(MultipartFormDataContent content)
    {
        var resp = await _http.PostAsync("/uploads", content);
        resp.EnsureSuccessStatusCode();
        return resp;
    }

    public async Task<HttpResponseMessage> GetAsync(long imageId)
    {
        var resp = await _http.GetAsync($"/uploads/{imageId}");
        return resp;
    }

    public async Task<HttpResponseMessage> DeleteAsync(long imageId)
    {
        var resp = await _http.DeleteAsync($"/uploads/{imageId}");
        resp.EnsureSuccessStatusCode();
        return resp;
    }
}
