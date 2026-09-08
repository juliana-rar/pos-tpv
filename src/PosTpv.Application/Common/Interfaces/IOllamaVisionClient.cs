namespace PosTpv.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the local Ollama vision model used to read supplier delivery notes
/// (albaranes) from a photo. Implemented in the Infrastructure layer so the Application layer
/// stays free of HTTP-client concerns.
/// </summary>
public interface IOllamaVisionClient
{
    /// <summary>
    /// Sends the photo to the configured vision model and returns its raw text response.
    /// Parsing that text into structured data is the caller's responsibility.
    /// </summary>
    Task<string> ExtractAlbaranJsonAsync(byte[] imageBytes, CancellationToken ct = default);
}

/// <summary>Thrown when the Ollama server can't be reached or returns a non-success response.</summary>
public class OllamaUnavailableException : Exception
{
    public OllamaUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
