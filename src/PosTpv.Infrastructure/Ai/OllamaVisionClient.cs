using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using PosTpv.Application.Common.Interfaces;

namespace PosTpv.Infrastructure.Ai;

/// <summary>
/// Calls a local Ollama server's vision model (default: moondream) to read a supplier delivery
/// note (albarán) photo and return it as JSON text. No paid/cloud OCR API involved.
/// </summary>
public class OllamaVisionClient : IOllamaVisionClient
{
    private const string Prompt = """
        Analiza la imagen de este albarán de proveedor (delivery note) y devuelve EXCLUSIVAMENTE
        un JSON con esta forma exacta, sin texto antes ni después, sin markdown:
        {"proveedor":"string o null","numero_albaran":"string o null","fecha":"YYYY-MM-DD o null",
        "lineas":[{"descripcion":"string","cantidad":0,"precio_unitario":0}],"total":0 o null}
        Si un dato no aparece en la imagen usa null. Los números usan punto decimal, sin
        separador de miles.
        """;

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public OllamaVisionClient(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<string> ExtractAlbaranJsonAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        var model = _config["Ollama:Model"] ?? "moondream";
        var client = _httpClientFactory.CreateClient("Ollama");

        // Small vision models often keep rambling past a complete JSON answer instead of
        // stopping on their own; num_predict caps that so a slow CPU host doesn't wait on
        // output no one needs.
        var request = new OllamaGenerateRequest(model, Prompt, [Convert.ToBase64String(imageBytes)], "json", false, new OllamaGenerateOptions(600));
        var body = JsonSerializer.Serialize(request, RequestJsonOptions);

        HttpResponseMessage response;
        try
        {
            using var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response = await client.PostAsync("api/generate", content, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new OllamaUnavailableException("No se pudo conectar con Ollama. ¿Está corriendo en local?", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new OllamaUnavailableException("Ollama tardó demasiado en responder.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new OllamaUnavailableException($"Ollama devolvió {(int)response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("response").GetString() ?? "";
        }
        catch (JsonException ex)
        {
            throw new OllamaUnavailableException("Respuesta de Ollama con formato inesperado.", ex);
        }
    }

    private record OllamaGenerateRequest(
        string Model,
        string Prompt,
        [property: JsonPropertyName("images")] string[] Images,
        string Format,
        bool Stream,
        OllamaGenerateOptions Options);

    private record OllamaGenerateOptions([property: JsonPropertyName("num_predict")] int NumPredict);
}
