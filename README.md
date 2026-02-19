# Soundsentiment AI

Herramienta experimental para analizar artistas: obtiene biografías desde TheAudioDB y usa modelos de Hugging Face para clasificar la biografía y generar una "línea de tiempo" (timeline) de eventos importantes.

## Características
- Búsqueda de artista y extracción de biografía (TheAudioDB).
- Clasificación semántica de la biografía con Hugging Face (zero-shot).
- Generación estructurada de línea de tiempo (IA + fallback heurístico).
- Interfaz web ligera incluida en `wwwroot`.

## Requisitos
- .NET 8 SDK
- Variable de entorno o configuración: `HuggingFace:ApiKey` (API key válida de Hugging Face)

## Cómo ejecutar
1. Clona el repositorio y abre la carpeta del proyecto.
2. Establece la API key (ejemplo PowerShell):

   ```powershell
   $Env:HuggingFace__ApiKey = "tu_hf_api_key"
   ```

3. Ejecuta la aplicación:

   ```bash
   dotnet run
   ```

4. Abre el navegador en `http://localhost:5000` (o el puerto que muestre la aplicación).

## Endpoints principales
- `GET /api/artist/analyze?name={artist}`
  - Realiza el flujo completo: obtiene biografía, clasificación, imagen, score y timeline (si la IA puede generarlo). Devuelve `ArtistAnalysisResponseDto`.

- `POST /api/artist/timeline`
  - Body: `{ "biography": "...texto completo..." }`
  - Genera solo la línea de tiempo usando el modelo instruccional en Hugging Face y devuelve `{ timeline: ... }`.

## DTOs importantes
- `ArtistAnalysisResponseDto` - resultado del análisis con propiedades: `artistName`, `biography`, `score`, `imageUrl`, `timeline`, `classification`.
- `TimelineEventDto` - evento de timeline con `year`, `event`, `details`.

## Frontend
La UI está en `wwwroot`:
- `index.html`, `app.js`, `styles.css` — interfaz simple para buscar artistas y mostrar resultados.
- El frontend espera `timeline` como cadena JSON (array) o usa un fallback cliente si el backend no devuelve timeline.

## Notas de implementación
- `HuggingFaceService` encapsula llamadas a la API de Hugging Face. El prompt para `GenerateTimeline` usa `google/flan-ul2` por defecto; puedes cambiar el modelo según cuota/calidad.
- Si la IA no devuelve un array JSON válido, `AnalysisService` aplica `GenerateSimpleTimeline` (heurística local) para asegurar resultados.
- Serialización JSON usa `camelCase` para facilitar consumo por JavaScript (configurado en `Program.cs`).

## Depuración
- Verifica que `HuggingFace:ApiKey` esté configurada si las peticiones a IA fallan (401 o 429).
- Revisa logs en la consola donde ejecutas `dotnet run` para excepciones del backend.



