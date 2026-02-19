namespace Soundsentiment.DTOs
{
    /// <summary>
    /// Representa un evento en la línea de tiempo de un artista.
    /// </summary>
    public class TimelineEventDto
    {
        /// <summary> Año del evento (ej. "1998" o null si no disponible). </summary>
        public string Year { get; set; } = string.Empty;

        /// <summary> Título corto del evento para mostrar en listados. </summary>
        public string Event { get; set; } = string.Empty;

        /// <summary> Descripción detallada del evento. </summary>
        public string Details { get; set; } = string.Empty;
    }
}
