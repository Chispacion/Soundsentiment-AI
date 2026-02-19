using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace Soundsentiment.Services
{
    public class HuggingFaceService
    {
        /// <summary>
        /// Servicio responsable de comunicarse con los endpoints de Hugging Face
        /// para clasificación y generación de texto (línea de tiempo, traducciones, etc.).
        /// </summary>
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public HuggingFaceService(HttpClient httpClient, IConfiguration configuration)
        {
            // Constructor: requiere HuggingFace:ApiKey en configuration
            _httpClient = httpClient;

            _apiKey = configuration["HuggingFace:ApiKey"]
                      ?? throw new Exception("HuggingFace API Key no configurada");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ================================
        // 🎯 CLASIFICACIÓN DEL ARTISTA
        // ================================
        /// <summary>
        /// Pide al modelo de clasificación que devuelva un objeto JSON con label, score y explicación
        /// para la biografía proporcionada. Normaliza las distintas formas de respuesta que puede
        /// devolver la API de Hugging Face y retorna el texto generado o el JSON crudo.
        /// </summary>
        /// <param name="biography">Texto de la biografía a clasificar.</param>
        /// <returns>Cadena con el resultado de la clasificación (texto o JSON).</returns>
        public async Task<string> ClassifyBiography(string biography)
        {
            var url = "https://router.huggingface.co/hf-inference/models/facebook/bart-large-mnli";

            var requestBody = new
            {
                inputs =
                    "You are an expert music historian and critic.\n" +
                    "Read the following artist biography and choose the single BEST label from the provided list.\n\n" +
                    "Label definitions:\n" +
                    "- Tragic life\n" +
                    "- Controversial figure\n" +
                    "- Influential artist\n" +
                    "- Commercial superstar\n" +
                    "- Short-lived career\n" +
                    "- Underground icon\n" +
                    "- Successful career\n\n" +
                    $"Biography:\n{biography}\n\n" +
                    "Return ONLY a JSON object with: label, score, explanation.",
                parameters = new
                {
                    candidate_labels = new[]
                    {
                        "Tragic life",
                        "Controversial figure",
                        "Influential artist",
                        "Commercial superstar",
                        "Short-lived career",
                        "Underground icon",
                        "Successful career"
                    },
                    hypothesis_template = "This artist is best described as having a {}."
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(
                    $"Error consultando IA: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}"
                );

            // Hugging Face inference can return different shapes depending on the model:
            // - JSON object with { "generated_text": "..." }
            // - JSON array [ { "generated_text": "..." } ]
            // - Plain text
            // Normalize to the generated text when possible so the frontend can parse it.
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("generated_text", out var gen))
                {
                    return gen.GetString() ?? string.Empty;
                }

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var first = root[0];
                    if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("generated_text", out var gen2))
                        return gen2.GetString() ?? string.Empty;

                    // If array of strings
                    if (first.ValueKind == JsonValueKind.String)
                        return first.GetString() ?? string.Empty;
                }

                // Fallback: return the original JSON string
                return responseBody;
            }
            catch (JsonException)
            {
                // Not JSON, return raw text
                return responseBody;
            }
        }

        /// <summary>
        /// Genera una línea de tiempo (timeline) a partir de la biografía proporcionada.
        /// Devuelve el resultado crudo en formato JSON tal como lo retorna Hugging Face.
        /// </summary>
        /// <param name="biography">Biografía completa del artista.</param>
        /// <returns>Cadena con un JSON array representando la línea de tiempo o texto crudo si no se pudo parsear.</returns>
        public async Task<string> GenerateTimeline(string biography)
        {
            if (string.IsNullOrWhiteSpace(biography))
                throw new ArgumentException("La biografía no puede estar vacía.");

            // Modelo instruccional capaz de generar respuestas direccionadas
            var url = "https://router.huggingface.co/hf-inference/models/google/flan-ul2";

            // Instrucción clara y ejemplo (few-shot) para obtener una línea de tiempo en formato JSON
            var prompt =
                "You are an expert music historian and critic.\n" +
                "Extract a CHRONOLOGICAL timeline of important events from the biography.\n" +
                "RETURN ONLY a JSON ARRAY (no extra text). Each item must be an object with the keys: \"year\" (string or null), \"event\" (short title), \"details\" (optional longer description).\n\n" +
                "Example input:\nBiography: John Doe (born 1980) released his first album in 2000. Won a Grammy in 2005.\n\n" +
                "Example output:\n[ { \"year\": \"1980\", \"event\": \"Born\", \"details\": \"Born in City X.\" }, { \"year\": \"2000\", \"event\": \"First album released\", \"details\": \"Debut album '...' released to critical acclaim.\" } ]\n\n" +
                $"Biography:\n{biography}\n\n" +
                "Now produce only the JSON array for the biography above.";

            var requestBody = new
            {
                inputs = prompt,
                parameters = new
                {
                    max_new_tokens = 512,
                    temperature = 0.2,
                    top_p = 0.95
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(
                    $"Error consultando IA: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}"
                );

            // Try to normalize and extract a JSON array representing the timeline.
            // 1) If response is JSON with generated_text, use it.
            // 2) If response is a JSON array/object, return it.
            // 3) If response is plain text that contains a JSON array block, extract that block.
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("generated_text", out var gen))
                {
                    var genText = gen.GetString() ?? string.Empty;
                    // If generated text itself is JSON, return it, otherwise try to extract JSON array from it
                    var extracted = TryExtractJsonArray(genText);
                    return extracted ?? genText;
                }

                if (root.ValueKind == JsonValueKind.Array || root.ValueKind == JsonValueKind.Object)
                {
                    // The model returned a JSON array/object directly (likely the desired timeline)
                    return responseBody;
                }
            }
            catch (JsonException)
            {
                // responseBody is not a pure JSON document; continue to try extracting JSON block
            }

            // If plain text, attempt to find a JSON array substring
            var maybeArray = TryExtractJsonArray(responseBody);
            if (maybeArray != null)
                return maybeArray;

            // Fallback: return raw text
            return responseBody;

            static string? TryExtractJsonArray(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;

                var start = text.IndexOf('[');
                var end = text.LastIndexOf(']');
                if (start >= 0 && end > start)
                {
                    var candidate = text.Substring(start, end - start + 1);
                    try
                    {
                        using var d = JsonDocument.Parse(candidate);
                        if (d.RootElement.ValueKind == JsonValueKind.Array)
                            return candidate;
                    }
                    catch (JsonException)
                    {
                        // not valid JSON array
                    }
                }

                return null;
            }
        }

    }
}