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
                IsSkipped = task.IsSkipped,
                EstimatedMinutes = task.EstimatedMinutes,
                Supplies = task.Supplies,
                Instructions = task.Instructions,
                LastPurchase = task.LastPurchase,
                RepeatFromCompletion = task.RepeatFromCompletion,
                CompletedAt = task.CompletedAt,
                CompletionNotes = task.CompletionNotes,
                RepeatEveryMonths = task.RepeatEveryMonths,
                PreviousCompletionNotes = _dbContext.MaintenanceTasks.Where(previous => previous.Id == task.GeneratedFromTaskId).Select(previous => previous.CompletionNotes).FirstOrDefault(),
                PropertyId = task.PropertyId
            })
            .ToListAsync();

        return Ok(maintenanceTasks);
    }

    [HttpGet("/api/maintenance-tasks/upcoming")]
    public async Task<ActionResult<IEnumerable<MaintenanceTaskResponse>>> GetUpcomingTasks(
        [FromQuery] DateOnly? today)
    {
        if (today is null || today > DateOnly.MaxValue.AddDays(-7))
        {
            return BadRequest("A valid local date is required.");
        }

        var lastDay = today.Value.AddDays(7);
        var tasks = await _dbContext.MaintenanceTasks
            .AsNoTracking()
            .Where(task => !task.IsCompleted && task.DueDate <= lastDay)
            .OrderBy(task => task.DueDate)
            .ThenBy(task => task.Id)
            .ToListAsync();

        return Ok(tasks.Select(ToResponse));
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
                IsSkipped = task.IsSkipped,
                EstimatedMinutes = task.EstimatedMinutes,
                Supplies = task.Supplies,
                Instructions = task.Instructions,
                LastPurchase = task.LastPurchase,
                RepeatFromCompletion = task.RepeatFromCompletion,
                CompletedAt = task.CompletedAt,
                CompletionNotes = task.CompletionNotes,
                RepeatEveryMonths = task.RepeatEveryMonths,
                PreviousCompletionNotes = _dbContext.MaintenanceTasks.Where(previous => previous.Id == task.GeneratedFromTaskId).Select(previous => previous.CompletionNotes).FirstOrDefault(),
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
            ScheduleAnchor = request.DueDate,
            EstimatedMinutes = request.EstimatedMinutes,
            Supplies = request.Supplies,
            Instructions = request.Instructions,
            LastPurchase = request.LastPurchase,
            RepeatFromCompletion = request.RepeatFromCompletion,
            PropertyId = propertyId,
            RepeatEveryMonths = request.RepeatEveryMonths
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

        if (request.IsSkipped && (!request.IsCompleted || maintenanceTask.RepeatEveryMonths == 0))
            return BadRequest("Only a recurring occurrence can be skipped.");
        if (request.IsCompleted && !maintenanceTask.IsCompleted && maintenanceTask.RepeatFromCompletion && maintenanceTask.RepeatEveryMonths > 0 && request.LocalDate is null)
            return BadRequest("Your local date is required for this schedule.");
        string? statusMessage = null;
        if (!request.IsCompleted && maintenanceTask.IsCompleted && maintenanceTask.HasCreatedNextOccurrence)
        {
            var next = await _dbContext.MaintenanceTasks.FirstOrDefaultAsync(task =>
                task.PropertyId == propertyId && task.GeneratedFromTaskId == id);
            if (next is not null && !next.IsCompleted && !next.HasBeenEdited && !next.HasCreatedNextOccurrence)
            {
                _dbContext.MaintenanceTasks.Remove(next);
                maintenanceTask.HasCreatedNextOccurrence = false;
                statusMessage = "Task reopened. Its untouched next occurrence was removed.";
            }
            else
            {
                statusMessage = "Task reopened. Any existing next occurrence was kept; completing this task again will not create another.";
            }
        }

        if (request.IsCompleted && !maintenanceTask.IsCompleted &&
            maintenanceTask.RepeatEveryMonths > 0 && !maintenanceTask.HasCreatedNextOccurrence)
        {
            DateOnly nextDueDate;
            try
            {
                if (maintenanceTask.RepeatFromCompletion && !request.IsSkipped)
                    nextDueDate = request.LocalDate!.Value.AddMonths(maintenanceTask.RepeatEveryMonths);
                else
                {
                    var anchor = maintenanceTask.ScheduleAnchor ?? maintenanceTask.DueDate;
                    var months = Math.Max(1, ((maintenanceTask.DueDate.Year - anchor.Year) * 12 + maintenanceTask.DueDate.Month - anchor.Month) / maintenanceTask.RepeatEveryMonths) * maintenanceTask.RepeatEveryMonths;
                    nextDueDate = anchor.AddMonths(months);
                    // A postponement can fall before this month's scheduled day.
                    if (nextDueDate <= maintenanceTask.DueDate)
                        nextDueDate = anchor.AddMonths(months + maintenanceTask.RepeatEveryMonths);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest("The next occurrence would exceed the supported date range.");
            }
            _dbContext.MaintenanceTasks.Add(new MaintenanceTask
            {
                Title = maintenanceTask.Title,
                Description = maintenanceTask.Description,
                DueDate = nextDueDate,
                PropertyId = propertyId,
                RepeatEveryMonths = maintenanceTask.RepeatEveryMonths,
                ScheduleAnchor = maintenanceTask.RepeatFromCompletion ? nextDueDate : maintenanceTask.ScheduleAnchor ?? maintenanceTask.DueDate,
                EstimatedMinutes = maintenanceTask.EstimatedMinutes,
                Supplies = maintenanceTask.Supplies,
                Instructions = maintenanceTask.Instructions,
                LastPurchase = maintenanceTask.LastPurchase,
                RepeatFromCompletion = maintenanceTask.RepeatFromCompletion,
                GeneratedFromTaskId = maintenanceTask.Id
            });
            maintenanceTask.HasCreatedNextOccurrence = true;
        }
        if (request.IsCompleted && !maintenanceTask.IsCompleted)
        {
            maintenanceTask.CompletedAt = request.IsSkipped ? null : DateTimeOffset.UtcNow;
            maintenanceTask.IsSkipped = request.IsSkipped;
        }
        else if (!request.IsCompleted)
        {
            maintenanceTask.IsSkipped = false;
            maintenanceTask.CompletedAt = null;
            maintenanceTask.CompletionNotes = null;
        }
        maintenanceTask.IsCompleted = request.IsCompleted;
        try { await _dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This task changed while saving. Refresh and try again.");
        }

        var response = ToResponse(maintenanceTask);
        response.StatusMessage = statusMessage;
        return Ok(response);
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

        if ((!maintenanceTask.IsCompleted || maintenanceTask.IsSkipped) && !string.IsNullOrWhiteSpace(request.CompletionNotes))
        {
            return BadRequest("Completion notes can only be added to completed tasks.");
        }
        if (maintenanceTask.IsCompleted && request.CompletionNotes is not null)
        {
            maintenanceTask.CompletionNotes = string.IsNullOrWhiteSpace(request.CompletionNotes)
                ? null : request.CompletionNotes.Trim();
        }
        if (maintenanceTask.DueDate != request.DueDate || maintenanceTask.RepeatEveryMonths != request.RepeatEveryMonths || maintenanceTask.RepeatFromCompletion != request.RepeatFromCompletion)
            maintenanceTask.ScheduleAnchor = request.DueDate;
        maintenanceTask.EstimatedMinutes = request.EstimatedMinutes;
        maintenanceTask.Supplies = request.Supplies;
        maintenanceTask.Instructions = request.Instructions;
        maintenanceTask.LastPurchase = request.LastPurchase;
        maintenanceTask.RepeatFromCompletion = request.RepeatFromCompletion;
        maintenanceTask.HasBeenEdited = true;
        maintenanceTask.RepeatEveryMonths = request.RepeatEveryMonths;
        maintenanceTask.Title = request.Title;
        maintenanceTask.Description = request.Description;
        maintenanceTask.DueDate = request.DueDate.GetValueOrDefault();
        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(maintenanceTask));
    }

    [HttpPatch("{id:int}/postpone")]
    public async Task<ActionResult<MaintenanceTaskResponse>> PostponeTask(int propertyId, int id, PostponeTaskRequest request)
    {
        var task = await _dbContext.MaintenanceTasks.FirstOrDefaultAsync(task => task.PropertyId == propertyId && task.Id == id);
        if (task is null) return NotFound();
        if (task.IsCompleted || request.DueDate <= task.DueDate) return BadRequest("Choose a later due date for an open task.");
        task.ScheduleAnchor ??= task.DueDate;
        task.DueDate = request.DueDate!.Value;
        task.HasBeenEdited = true;
        try { await _dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict("This task changed. Refresh and try again."); }
        return Ok(ToResponse(task));
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
                IsSkipped = task.IsSkipped,
                EstimatedMinutes = task.EstimatedMinutes,
                Supplies = task.Supplies,
                Instructions = task.Instructions,
                LastPurchase = task.LastPurchase,
                RepeatFromCompletion = task.RepeatFromCompletion,
                CompletedAt = task.CompletedAt,
                CompletionNotes = task.CompletionNotes,
                RepeatEveryMonths = task.RepeatEveryMonths,
            PropertyId = task.PropertyId
        };
    }
}
