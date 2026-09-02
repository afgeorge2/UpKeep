using Microsoft.AspNetCore.Mvc;
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
}
