using System.Text.Json;
using AppFitness.Shared.Services;
using Microsoft.JSInterop;

namespace AppFitness.Wasm.Services;

public class BrowserLocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public BrowserLocalStorageService(IJSRuntime js) => _js = js;

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json, _options);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, _options);
        await _js.InvokeVoidAsync("localStorage.setItem", key, json);
    }

    public async Task RemoveAsync(string key)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", key);
    }
}
