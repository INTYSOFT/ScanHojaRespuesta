using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContrlAcademico.Services;

public class EvaluacionProgramadaService
{
    private readonly HttpClient _httpClient;

    public EvaluacionProgramadaService()
        : this(new HttpClient())
    {
    }

    public EvaluacionProgramadaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<EvaluacionProgramadaSummaryDto>> ObtenerPorEstadoAsync(
        string baseUrl,
        int estadoId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("La URL base del servicio no está configurada.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("La URL base del servicio es inválida.");
        }

        PrepareClient(baseUri, token);

        using var response = await _httpClient.GetAsync($"api/EvaluacionProgramada/estado/{estadoId}", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<List<EvaluacionProgramadaSummaryDto>>(contentStream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return result ?? new List<EvaluacionProgramadaSummaryDto>();
    }

    public async Task<IReadOnlyList<EvaluacionProgramadaConsultaDto>> ObtenerDetalleAsync(
        string baseUrl,
        int evaluacionProgramadaId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("La URL base del servicio no está configurada.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("La URL base del servicio es inválida.");
        }

        PrepareClient(baseUri, token);

        using var response = await _httpClient.GetAsync($"api/EvaluacionProgramada/{evaluacionProgramadaId}", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<List<EvaluacionProgramadaConsultaDto>>(contentStream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return result ?? new List<EvaluacionProgramadaConsultaDto>();
    }

    private void PrepareClient(Uri baseUri, string token)
    {
        _httpClient.BaseAddress = baseUri;
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };
}

public sealed class EvaluacionProgramadaSummaryDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("fechaInicio")]
    public DateOnly FechaInicio { get; set; }

    [JsonIgnore]
    public string DisplayText => $"{Nombre} - {FechaInicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
}

public sealed class EvaluacionProgramadaConsultaDto
{
    [JsonPropertyName("estadoId")]
    public int? EstadoId { get; init; }

    [JsonPropertyName("evaluacionId")]
    public int EvaluacionId { get; init; }

    [JsonPropertyName("evaluacionProgramadaId")]
    public int EvaluacionProgramadaId { get; init; }

    [JsonPropertyName("sede")]
    public string? Sede { get; init; }

    [JsonPropertyName("ciclo")]
    public string? Ciclo { get; init; }

    [JsonPropertyName("seccion")]
    public string? Seccion { get; init; }

    [JsonPropertyName("alumnoDni")]
    public string? AlumnoDni { get; init; }

    [JsonPropertyName("alumnoApellidos")]
    public string? AlumnoApellidos { get; init; }

    [JsonPropertyName("alumnoNombres")]
    public string? AlumnoNombres { get; init; }

    [JsonPropertyName("alumnoCelular")]
    public string? AlumnoCelular { get; init; }
}
