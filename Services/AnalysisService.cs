using Soundsentiment.DTOs;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Soundsentiment.Services
{
    /// <summary>
    /// Servicio encargado de orquestar el análisis de un artista:
    /// - obtiene biografía de TheAudioDB
    /// - pide clasificación a Hugging Face
    /// - genera una línea de tiempo (IA o heurística local)
    /// </summary>
    public class AnalysisService
    {
        private readonly AudioDbService _audioService;
        private readonly HuggingFaceService _aiService;

        public AnalysisService(AudioDbService audioService, HuggingFaceService aiService)
        {
            _audioService = audioService;
            _aiService = aiService;
        }

        /// <summary>
        /// Ejecuta el flujo completo de análisis para un artista.
        /// </summary>
        /// <param name="artistName">Nombre del artista a analizar</param>
        /// <returns>DTO con biografía, clasificación, imagen, score y timeline</returns>
        public async Task<ArtistAnalysisResponseDto> AnalyzeArtist(string artistName)
        {
            if (string.IsNullOrWhiteSpace(artistName))
                throw new ArgumentException("El nombre del artista es obligatorio.");

            // 1️⃣ Obtener biografía
            var biography = await _audioService.GetArtistBiography(artistName);

            if (string.IsNullOrWhiteSpace(biography))
                throw new KeyNotFoundException($"Artista '{artistName}' no encontrado.");

            // 2️⃣ Clasificación IA
            var aiResult = await _aiService.ClassifyBiography(biography);
            string bestLabel = ExtractBestLabel(aiResult);
            string classification = TranslateClassification(bestLabel);

            // 3️⃣ Generar línea de tiempo
            string timeline = await GenerateTimelineWithFallback(biography);

            // 4️⃣ Obtener imagen y score
            var (imageUrl, score) = await _audioService.GetArtistImageAndScore(artistName);

            // 5️⃣ Retornar todo
            return new ArtistAnalysisResponseDto
            {
                ArtistName = artistName,
                Biography = biography,
                Score = score,
                ImageUrl = imageUrl ?? string.Empty,
                Classification = classification,
                Timeline = timeline
            };
        }

        /// <summary>
        /// Intenta generar la línea de tiempo usando la IA; si falla, aplica una heurística local.
        /// </summary>
        private async Task<string> GenerateTimelineWithFallback(string biography)
        {
            try
            {
                var timeline = await _aiService.GenerateTimeline(biography);

                // Verificar si es un JSON array válido
                if (!string.IsNullOrWhiteSpace(timeline) && timeline.TrimStart().StartsWith("["))
                {
                    try
                    {
                        JsonDocument.Parse(timeline);
                        return timeline;
                    }
                    catch
                    {
                        // Si no es JSON válido, continuar con fallback
                    }
                }
            }
            catch
            {
                // Si hay error, continuar con fallback
            }

            return GenerateSimpleTimeline(biography);
        }

        /// <summary>
        /// Genera una línea de tiempo simple a partir de la biografía usando heurísticas de extracción de años y oraciones.
        /// Devuelve un JSON serializado con una lista de <see cref="TimelineEventDto"/>.
        /// </summary>
        private string GenerateSimpleTimeline(string biography)
        {
            var timeline = new List<TimelineEventDto>();
            var lines = biography.Split(new[] { '\n', '.' }, StringSplitOptions.RemoveEmptyEntries);

            var yearRegex = new Regex(@"\b(19|20)\d{2}\b");

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.Length < 20) continue;

                var yearMatch = yearRegex.Match(trimmedLine);
                string year = yearMatch.Success ? yearMatch.Value : "Unknown";

                // Use a longer event title so it doesn't cut mid-sentence prematurely in the UI.
                // Keep full details in `Details` and only shorten the `Event` title for display.
                int eventTitleLimit = 220;
                string eventText = trimmedLine.Length > eventTitleLimit
                    ? trimmedLine.Substring(0, eventTitleLimit) + "..."
                    : trimmedLine;

                timeline.Add(new TimelineEventDto
                {
                    Year = year,
                    Event = eventText,
                    Details = trimmedLine
                });

                // Allow a few more events so the timeline is richer
                if (timeline.Count >= 8) break;
            }

            return JsonSerializer.Serialize(timeline);
        }

        private string ExtractBestLabel(string aiResult)
        {
            try
            {
                using var doc = JsonDocument.Parse(aiResult);
                var root = doc.RootElement;

                // Zero-shot classification response
                if (root.TryGetProperty("labels", out var labels) &&
                    root.TryGetProperty("scores", out var scores) &&
                    labels.GetArrayLength() > 0)
                {
                    // Encontrar el índice del score más alto
                    double maxScore = 0;
                    int bestIndex = 0;

                    for (int i = 0; i < scores.GetArrayLength(); i++)
                    {
                        double score = scores[i].GetDouble();
                        if (score > maxScore)
                        {
                            maxScore = score;
                            bestIndex = i;
                        }
                    }

                    return labels[bestIndex].GetString() ?? "Unknown";
                }

                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string TranslateClassification(string label)
        {
            return label switch
            {
                "Tragic life" => "Vida trágica",
                "Controversial figure" => "Figura controversial",
                "Influential artist" => "Artista influyente",
                "Commercial superstar" => "Superestrella comercial",
                "Short-lived career" => "Carrera de corta duración",
                "Underground icon" => "Ícono underground",
                "Successful career" => "Carrera exitosa",
                _ => "No determinada"
            };
        }
    }

}
