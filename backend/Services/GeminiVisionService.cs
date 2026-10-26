using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DefectInspection.Api.Dtos;
using DefectInspection.Api.Models;

namespace DefectInspection.Api.Services;

/// <summary>The image is too blurry / dark / irrelevant to inspect reliably.</summary>
public class ImageUnclearException(string message) : Exception(message);

/// <summary>The AI provider failed (auth, quota, outage, malformed output).</summary>
public class AiServiceException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public interface IGeminiVisionService
{
    Task<GeminiResult> AnalyzeAsync(byte[] imageBytes, string mimeType, string fileName,
        IReadOnlyList<DefectCategory> categories, CancellationToken ct);
}

public class GeminiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gemini-3.5-flash";
    public int MaxRetries { get; set; } = 3;
    /// <summary>Offline demo mode: derives a canned result from the file name. Never use in production.</summary>
    public bool UseMock { get; set; }
    public double MinConfidence { get; set; } = 0.4;
}

public class GeminiVisionService(HttpClient http, Microsoft.Extensions.Options.IOptions<GeminiOptions> options,
    ILogger<GeminiVisionService> log) : IGeminiVisionService
{
    private readonly GeminiOptions _opt = options.Value;

    public async Task<GeminiResult> AnalyzeAsync(byte[] imageBytes, string mimeType, string fileName,
        IReadOnlyList<DefectCategory> categories, CancellationToken ct)
    {
        if (_opt.UseMock) return Mock(fileName, categories);

        if (string.IsNullOrWhiteSpace(_opt.ApiKey))
            throw new AiServiceException("Gemini API key is not configured (Gemini:ApiKey).", HttpStatusCode.ServiceUnavailable);

        var body = BuildRequest(imageBytes, mimeType, categories);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_opt.Model}:generateContent";

        string? text = null;
        for (var attempt = 1; ; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-goog-api-key", _opt.ApiKey);

            HttpResponseMessage resp;
            try { resp = await http.SendAsync(req, ct); }
            catch (HttpRequestException ex) when (attempt < _opt.MaxRetries)
            {
                log.LogWarning(ex, "Gemini network error, retry {Attempt}", attempt);
                await Task.Delay(Backoff(attempt), ct);
                continue;
            }
            catch (HttpRequestException ex)
            {
                throw new AiServiceException("Cannot reach Gemini: " + ex.Message, HttpStatusCode.BadGateway);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // HttpClient.Timeout elapsed (the caller did not cancel).
                if (attempt < _opt.MaxRetries) { log.LogWarning("Gemini timeout, retry {Attempt}", attempt); continue; }
                throw new AiServiceException("Gemini did not respond in time. Please retry.", HttpStatusCode.GatewayTimeout);
            }

            var payload = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode) { text = ExtractText(payload); break; }

            // 429 (rate limit) and 5xx are transient; 400/401/403 are not.
            var transient = resp.StatusCode == HttpStatusCode.TooManyRequests || (int)resp.StatusCode >= 500;
            if (transient && attempt < _opt.MaxRetries)
            {
                log.LogWarning("Gemini {Status}, retry {Attempt}", (int)resp.StatusCode, attempt);
                await Task.Delay(Backoff(attempt), ct);
                continue;
            }
            log.LogError("Gemini failed {Status}: {Body}", (int)resp.StatusCode, payload);
            throw new AiServiceException(resp.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Gemini quota/rate limit exceeded (429). Please retry shortly.",
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => "Gemini rejected the API key (403/401).",
                HttpStatusCode.BadRequest => "Gemini rejected the request (400): " + ShortError(payload),
                HttpStatusCode.ServiceUnavailable => "Gemini is overloaded (503). Please retry in a moment.",
                _ => $"Gemini error {(int)resp.StatusCode}."
            }, resp.StatusCode);
        }

        GeminiResult? result;
        try
        {
            result = JsonSerializer.Deserialize<GeminiResult>(text!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new AiServiceException("Gemini returned malformed JSON: " + ex.Message, HttpStatusCode.BadGateway);
        }
        if (result is null) throw new AiServiceException("Gemini returned an empty result.", HttpStatusCode.BadGateway);

        if (!result.IsImageClear || result.Confidence < _opt.MinConfidence)
            throw new ImageUnclearException(string.IsNullOrWhiteSpace(result.UnclearReason)
                ? "Image is blurry or unclear, please retake."
                : result.UnclearReason!);

        // Normalise: guard against the model inventing ids / inconsistent status.
        var cat = categories.FirstOrDefault(c => c.DefectCategoryId == result.DefectCategoryId)
                  ?? categories.First(c => c.SeverityLevel == "None");
        var status = result.QualityStatus == "Fail" ? "Fail" : "Pass";
        if (cat.SeverityLevel is "Major" or "Critical") status = "Fail";
        if (cat.SeverityLevel == "None") status = "Pass";
        return result with { DefectCategoryId = cat.DefectCategoryId, QualityStatus = status };
    }

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    private static string ShortError(string payload)
    {
        try { return JsonNode.Parse(payload)?["error"]?["message"]?.GetValue<string>() ?? payload; }
        catch { return payload.Length > 200 ? payload[..200] : payload; }
    }

    private static string ExtractText(string payload)
    {
        var root = JsonNode.Parse(payload);
        var block = root?["promptFeedback"]?["blockReason"]?.GetValue<string>();
        if (block is not null) throw new AiServiceException("Gemini blocked the image: " + block, HttpStatusCode.UnprocessableEntity);
        var text = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(text)
            ? throw new AiServiceException("Gemini returned no content.", HttpStatusCode.BadGateway)
            : text;
    }

    private static JsonObject BuildRequest(byte[] imageBytes, string mimeType, IReadOnlyList<DefectCategory> categories)
    {
        var catList = string.Join("\n", categories.Select(c => $"- {c.DefectCategoryId}: {c.CategoryName} [{c.SeverityLevel}]"));
        var prompt = $$"""
            You are a manufacturing quality-control (QC) expert. Inspect the product in the image and detect defects.
            Write the description and unclearReason fields ENTIRELY in English, and follow the JSON schema exactly.

            Defect categories (choose exactly one defectCategoryId):
            {{catList}}

            Rules:
            - If the image is too blurry, too dark, glare-affected, obstructed, does not contain a product, or is insufficient to decide: isImageClear=false, give a short explanation in unclearReason, and a low confidence.
            - Good product: choose the [None] category, qualityStatus="Pass".
            - Minor defect: qualityStatus="Pass".
            - Major/Critical defect: qualityStatus="Fail".
            - description: describe the location, size and severity of the defect (or state clearly that there is no defect).
            - confidence: a number from 0 to 1 expressing certainty.
            """;

        var schema = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["isImageClear"] = new JsonObject { ["type"] = "BOOLEAN" },
                ["unclearReason"] = new JsonObject { ["type"] = "STRING", ["nullable"] = true },
                ["qualityStatus"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray("Pass", "Fail") },
                ["defectCategoryId"] = new JsonObject { ["type"] = "INTEGER" },
                ["description"] = new JsonObject { ["type"] = "STRING" },
                ["confidence"] = new JsonObject { ["type"] = "NUMBER" }
            },
            ["required"] = new JsonArray("isImageClear", "qualityStatus", "defectCategoryId", "description", "confidence")
        };

        return new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["parts"] = new JsonArray(
                    new JsonObject { ["text"] = prompt },
                    new JsonObject
                    {
                        ["inline_data"] = new JsonObject
                        {
                            ["mime_type"] = mimeType,
                            ["data"] = Convert.ToBase64String(imageBytes)
                        }
                    })
            }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.2,
                // Gemini 3.x thinks heavily by default (~60-90s per image); "minimal" garbled Vietnamese diacritics, "low" keeps it fast and clean.
                ["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "low" },
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = schema
            }
        };
    }

    private static GeminiResult Mock(string fileName, IReadOnlyList<DefectCategory> cats)
    {
        var n = fileName.ToLowerInvariant();
        if (n.Contains("blur") || n.Contains("unclear") || n.Contains("faulty"))
            throw new ImageUnclearException("[MOCK] Image is too blurry to inspect.");
        if (n.Contains("major"))
            return new GeminiResult(true, null, "Fail", 4, "[MOCK] Severe dent on the body of the product.", 0.91);
        if (n.Contains("minor"))
            return new GeminiResult(true, null, "Pass", 2, "[MOCK] Small scratch on the surface.", 0.84);
        return new GeminiResult(true, null, "Pass", 1, "[MOCK] No defect detected.", 0.95);
    }
}
