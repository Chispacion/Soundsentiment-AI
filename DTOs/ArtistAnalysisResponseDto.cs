namespace Soundsentiment.DTOs
{
    public class ArtistAnalysisResponseDto
    {
        /// <summary>Nombre del artista.</summary>
        public string ArtistName { get; set; } = string.Empty;
        /// <summary>Biografía completa (texto) del artista.</summary>
        public string Biography { get; set; } = string.Empty; // ← biografía completa
        /// <summary>Puntuación heurística derivada de TheAudioDB.</summary>
        public double Score { get; set; }
        /// <summary>URL de la imagen en miniatura si está disponible.</summary>
        public string ImageUrl { get; set; } = string.Empty;
        /// <summary>Cadena JSON que representa la línea de tiempo (array) o texto crudo.</summary>
        public string Timeline { get; set; } = string.Empty;
        /// <summary>Clasificación de la biografía (texto traducido al español).</summary>
        public string Classification { get; set; } = string.Empty;
    }
}