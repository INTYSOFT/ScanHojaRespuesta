using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ContrlAcademico.Services;

public sealed class EvaluacionRespuestaService
{
    private readonly HttpClient _httpClient;

    public EvaluacionRespuestaService()
        : this(new HttpClient())
    {
    }

    public EvaluacionRespuestaService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task RegistrarRespuestasAsync(
        string baseUrl,
        string token,
        IEnumerable<EvaluacionRespuestaCreateDto> respuestas,
        CancellationToken cancellationToken = default)
    {
        if (respuestas is null)
        {
            throw new ArgumentNullException(nameof(respuestas));
        }

        var payload = respuestas.ToList();
        if (payload.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("La URL base del servicio no está configurada.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("La URL base del servicio es inválida.");
        }

        PrepareClient(baseUri, token);

        foreach (var respuesta in payload)
        {
            var json = JsonSerializer.Serialize(respuesta, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient
                .PostAsync("api/EvaluacionRespuestums", content, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
        }
    }

    private void PrepareClient(Uri baseUri, string token)
    {
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = baseUri;
        }
        else if (_httpClient.BaseAddress != baseUri)
        {
            throw new InvalidOperationException("El cliente HTTP ya está configurado con una URL base diferente.");
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed class EvaluacionRespuestaCreateDto
{
    [JsonPropertyName("evaluacionId")]
    public int EvaluacionId { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("preguntaOrden")]
    public int PreguntaOrden { get; set; }

    [JsonPropertyName("respuesta")]
    public string? Respuesta { get; set; }

    [JsonPropertyName("fuente")]
    public string Fuente { get; set; } = string.Empty;

    [JsonPropertyName("confianza")]
    public decimal? Confianza { get; set; }

    [JsonPropertyName("tiempoMarcaMs")]
    public int? TiempoMarcaMs { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }

    [JsonPropertyName("fechaRegistro")]
    public DateTime? FechaRegistro { get; set; }

    [JsonPropertyName("fechaActualizacion")]
    public DateTime? FechaActualizacion { get; set; }

    [JsonPropertyName("usuaraioRegistroId")]
    public int? UsuaraioRegistroId { get; set; }

    [JsonPropertyName("usuaraioActualizacionId")]
    public int? UsuaraioActualizacionId { get; set; }

    [JsonPropertyName("evaluacionProgramadaId")]
    public int? EvaluacionProgramadaId { get; set; }

    [JsonPropertyName("seccionId")]
    public int? SeccionId { get; set; }

    [JsonPropertyName("alumnoId")]
    public int? AlumnoId { get; set; }

    [JsonPropertyName("dniAlumno")]
    public string? DniAlumno { get; set; }
}
