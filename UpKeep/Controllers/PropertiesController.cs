using Microsoft.AspNetCore.Mvc;
using UpKeep.Models;

namespace UpKeep.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<Property>> GetProperties()
    {
        var properties = new List<Property>
        {
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
        };

        return Ok(properties);
    }
}
