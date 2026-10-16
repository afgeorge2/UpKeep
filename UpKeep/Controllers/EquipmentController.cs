using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UpKeep.Services;

namespace UpKeep.Controllers;

[ApiController]
[Route("api/equipment")]
public sealed class EquipmentController(EquipmentAnalysis analysis) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { available = analysis.Available });

    [HttpPost("analyze")]
    [EnableRateLimiting("equipment")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Analyze(EquipmentAnalysisRequest request, CancellationToken cancellation)
    {
        if (request.Model is null || request.Photo is null || request.FilterPhoto is null ||
            (string.IsNullOrWhiteSpace(request.Model) && request.Photo.Length == 0) ||
            !EquipmentAnalysis.ValidPhoto(request.Photo) || !EquipmentAnalysis.ValidPhoto(request.FilterPhoto))
            return BadRequest(new { message = "Enter a model or provide a JPG, PNG, or WebP equipment photo." });
        if (!analysis.Available) return StatusCode(503, new { message = "AI equipment lookup is not connected yet. Local label reading is still available below." });
        try { return Ok(await analysis.Analyze(request, cancellation)); }
        catch (HttpRequestException) { return StatusCode(502, new { message = "The AI provider could not complete the lookup. Check the server API configuration and try again." }); }
        catch (System.Text.Json.JsonException) { return StatusCode(502, new { message = "The analysis was incomplete. Please try again with a clearer label photo." }); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return StatusCode(504, new { message = "The lookup timed out. Please try again." }); }
    }
}
