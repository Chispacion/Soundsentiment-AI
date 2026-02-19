using System.Text.Json;

namespace Soundsentiment.Services
{
    public class AudioDbService
    {
        /// <summary>
        /// Servicio para interactuar con TheAudioDB (obtener biografías, imágenes y metadatos de artistas).
        /// </summary>
        private readonly HttpClient _httpClient;

        public AudioDbService(HttpClient httpClient)
        {
            /// <summary>
            /// Constructor que recibe un HttpClient inyectado.
            /// </summary>
            _httpClient = httpClient;
        }

        /// <summary>
        /// Obtiene la biografía de un artista desde TheAudioDB
        /// </summary>
        /// <param name="artistName">Nombre del artista a buscar.</param>
        /// <returns>Biografía en texto o null si no se encuentra.</returns>
        public async Task<string?> GetArtistBiography(string artistName)
        {
            if (string.IsNullOrWhiteSpace(artistName))
                throw new ArgumentException("El nombre del artista no puede estar vacío.");

            // Construimos la URL con el nombre del artista
            var url = $"https://www.theaudiodb.com/api/v1/json/123/search.php?s={Uri.EscapeDataString(artistName)}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error consultando TheAudioDB: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            // Verificamos si existe el array "artists"
            if (!doc.RootElement.TryGetProperty("artists", out var artistsElement) ||
                artistsElement.ValueKind != JsonValueKind.Array ||
                artistsElement.GetArrayLength() == 0)
            {
                return null;
            }

            var artist = artistsElement[0];

            // Intentamos obtener la biografía en inglés
            if (!artist.TryGetProperty("strBiographyEN", out var bioElement))
                return null;

            return bioElement.GetString();
        }

        /// <summary>
        /// Obtiene la imagen de miniatura del artista (si existe) y calcula un score heurístico.
        /// </summary>
        /// <param name="artistName">Nombre del artista a buscar.</param>
        /// <returns>Tupla con URL de la imagen (o null) y un score entre 0.0 y 1.0.</returns>
        public async Task<(string? imageUrl, double score)> GetArtistImageAndScore(string artistName)
        {
            if (string.IsNullOrWhiteSpace(artistName))
                throw new ArgumentException("El nombre del artista no puede estar vacío.");

            var url = $"https://www.theaudiodb.com/api/v1/json/123/search.php?s={Uri.EscapeDataString(artistName)}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error consultando TheAudioDB: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("artists", out var artistsElement) ||
                artistsElement.ValueKind != JsonValueKind.Array ||
                artistsElement.GetArrayLength() == 0)
            {
                return (null, 0);
            }

            var artist = artistsElement[0];

            string? imageUrl = null;
            double score = 0.0;

            if (artist.TryGetProperty("strArtistThumb", out var thumb))
                imageUrl = thumb.GetString();

            // TheAudioDB doesn't provide a 'score' metric; we fabricate a simple heuristic based on available fields
            // e.g., if artist has year formed, label, and website we consider them more established
            int weight = 0;
            if (artist.TryGetProperty("intFormedYear", out var year) && !string.IsNullOrWhiteSpace(year.GetString())) weight++;
            if (artist.TryGetProperty("strLabel", out var label) && !string.IsNullOrWhiteSpace(label.GetString())) weight++;
            if (artist.TryGetProperty("strWebsite", out var site) && !string.IsNullOrWhiteSpace(site.GetString())) weight++;

            // Map weight 0-3 to a 0..1 score
            score = Math.Round((weight / 3.0) * 1.0, 2);

            return (imageUrl, score);
        }
    }
}