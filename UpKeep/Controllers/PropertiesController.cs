using Microsoft.AspNetCore.Mvc;
using UpKeep.Contracts.Properties;
using UpKeep.Models;

namespace UpKeep.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private static readonly List<Property> Properties =
    [
        new()
        {
            Id = 1,
            Name = "Primary Home",
            Address = "123 Main Street"
        },
        new()
        {
            Id = 2,
            Name = "Rental Property",
            Address = "456 Oak Avenue"
        }
    ];

    [HttpGet]
    public ActionResult<IEnumerable<Property>> GetProperties()
    {
        return Ok(Properties);
    }

    [HttpGet("{id:int}")]
    public ActionResult<Property> GetProperty(int id)
    {
        var property = Properties.FirstOrDefault(property => property.Id == id);

        if (property is null)
        {
            return NotFound();
        }

        return Ok(property);
    }

    [HttpPost]
    public ActionResult<Property> CreateProperty(CreatePropertyRequest request)
    {
        var nextId = Properties.Count == 0
            ? 1
            : Properties.Max(property => property.Id) + 1;

        var property = new Property
        {
            Id = nextId,
            Name = request.Name,
            Address = request.Address
        };

        Properties.Add(property);

        return CreatedAtAction(
            nameof(GetProperty),
            new { id = property.Id },
            property);
    }
}
