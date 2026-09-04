using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UpKeep.Contracts.MaintenanceTasks;
using UpKeep.Data;
using UpKeep.Models;

namespace UpKeep.Controllers;

[ApiController]
[Route("api/properties/{propertyId:int}/maintenance-tasks")]
public class MaintenanceTasksController : ControllerBase
{
    private readonly UpKeepDbContext _dbContext;

    public MaintenanceTasksController(UpKeepDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaintenanceTaskResponse>>>
        GetMaintenanceTasks(int propertyId)
    {
        var propertyExists = await _dbContext.Properties
            .AnyAsync(property => property.Id == propertyId);

        if (!propertyExists)
        {
            return NotFound();
        }

        var maintenanceTasks = await _dbContext.MaintenanceTasks
            .AsNoTracking()
            .Where(task => task.PropertyId == propertyId)
            .OrderBy(task => task.DueDate)
            .Select(task => new MaintenanceTaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                IsCompleted = task.IsCompleted,
                PropertyId = task.PropertyId
            })
            .ToListAsync();

        return Ok(maintenanceTasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaintenanceTaskResponse>>
        GetMaintenanceTask(int propertyId, int id)
    {
        var maintenanceTask = await _dbContext.MaintenanceTasks
            .AsNoTracking()
            .Where(task => task.PropertyId == propertyId && task.Id == id)
            .Select(task => new MaintenanceTaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                IsCompleted = task.IsCompleted,
                PropertyId = task.PropertyId
            })
            .FirstOrDefaultAsync();

        if (maintenanceTask is null)
        {
            return NotFound();
        }

        return Ok(maintenanceTask);
    }

    [HttpPost]
    public async Task<ActionResult<MaintenanceTaskResponse>>
        CreateMaintenanceTask(
            int propertyId,
            CreateMaintenanceTaskRequest request)
    {
        var propertyExists = await _dbContext.Properties
            .AnyAsync(property => property.Id == propertyId);

        if (!propertyExists)
        {
            return NotFound();
        }

        var maintenanceTask = new MaintenanceTask
        {
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate.GetValueOrDefault(),
            PropertyId = propertyId
        };

        _dbContext.MaintenanceTasks.Add(maintenanceTask);
        await _dbContext.SaveChangesAsync();

        var response = ToResponse(maintenanceTask);

        return CreatedAtAction(
            nameof(GetMaintenanceTask),
            new { propertyId, id = maintenanceTask.Id },
            response);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<MaintenanceTaskResponse>>
        UpdateMaintenanceTaskStatus(
            int propertyId,
            int id,
            UpdateMaintenanceTaskStatusRequest request)
    {
        var maintenanceTask = await _dbContext.MaintenanceTasks
            .FirstOrDefaultAsync(task =>
                task.PropertyId == propertyId && task.Id == id);

        if (maintenanceTask is null)
        {
            return NotFound();
        }

        maintenanceTask.IsCompleted = request.IsCompleted;
        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(maintenanceTask));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MaintenanceTaskResponse>>
        UpdateMaintenanceTask(
            int propertyId,
            int id,
            UpdateMaintenanceTaskRequest request)
    {
        var maintenanceTask = await _dbContext.MaintenanceTasks
            .FirstOrDefaultAsync(task =>
                task.PropertyId == propertyId && task.Id == id);

        if (maintenanceTask is null)
        {
            return NotFound();
        }

        maintenanceTask.Title = request.Title;
        maintenanceTask.Description = request.Description;
        maintenanceTask.DueDate = request.DueDate.GetValueOrDefault();
        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(maintenanceTask));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMaintenanceTask(
        int propertyId,
        int id)
    {
        var maintenanceTask = await _dbContext.MaintenanceTasks
            .FirstOrDefaultAsync(task =>
                task.PropertyId == propertyId && task.Id == id);

        if (maintenanceTask is null)
        {
            return NotFound();
        }

        _dbContext.MaintenanceTasks.Remove(maintenanceTask);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private static MaintenanceTaskResponse ToResponse(MaintenanceTask task)
    {
        return new MaintenanceTaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            DueDate = task.DueDate,
            IsCompleted = task.IsCompleted,
            PropertyId = task.PropertyId
        };
    }
}
