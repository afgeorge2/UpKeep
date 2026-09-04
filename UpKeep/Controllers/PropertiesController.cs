using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UpKeep.Contracts.Properties;
using UpKeep.Data;
using UpKeep.Models;

namespace UpKeep.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly UpKeepDbContext _dbContext;

    public PropertiesController(UpKeepDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Property>>> GetProperties()
    {
        var properties = await _dbContext.Properties
            .AsNoTracking()
            .ToListAsync();

        return Ok(properties);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Property>> GetProperty(int id)
    {
        var property = await _dbContext.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(property => property.Id == id);

        if (property is null)
        {
            return NotFound();
        }

        return Ok(property);
    }

    [HttpPost]
    public async Task<ActionResult<Property>> CreateProperty(
        CreatePropertyRequest request)
    {
        var property = new Property
        {
            Name = request.Name,
            Address = request.Address
        };

        _dbContext.Properties.Add(property);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetProperty),
            new { id = property.Id },
            property);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProperty(int id)
    {
        var property = await _dbContext.Properties.FindAsync(id);

        if (property is null)
        {
            return NotFound();
        }

        _dbContext.Properties.Remove(property);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}
