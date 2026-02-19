using Microsoft.AspNetCore.Mvc;
using Soundsentiment.Services;
using Soundsentiment.DTOs;

namespace Soundsentiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArtistController : ControllerBase
    {
        /// <summary>
        /// API Controller para endpoints relacionados con artistas.
        /// </summary>
        private readonly AnalysisService _analysisService;

        /// <summary>
        /// Constructor con servicios inyectados.
        /// </summary>
        /// <param name="analysisService">Servicio de análisis a inyectar.</param>
        public ArtistController(AnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        /// <summary>
        /// Analiza un artista, obtiene su biografía y clasificación.
        /// </summary>
        /// <param name="name">Nombre del artista</param>
        /// <returns>Información analizada del artista</returns>
        [HttpGet("analyze")]
        [ProducesResponseType(typeof(ArtistAnalysisResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ArtistAnalysisResponseDto>> Analyze([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("El nombre del artista es obligatorio.");

            try
            {
                var result = await _analysisService.AnalyzeArtist(name);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error interno al analizar el artista.");
            }
        }
    }
}
