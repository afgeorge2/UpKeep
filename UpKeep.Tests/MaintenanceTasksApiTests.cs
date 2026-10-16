using System.Net;
using System.Net.Http.Json;
using UpKeep.Contracts.MaintenanceTasks;
using Xunit;

namespace UpKeep.Tests;

public sealed class MaintenanceTasksApiTests :
    IClassFixture<UpKeepApiFactory>
{
    private readonly HttpClient _client;

    public MaintenanceTasksApiTests(UpKeepApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTask_CanBeRetrievedFromItsProperty()
    {
        var propertyResponse = await _client.PostAsJsonAsync(
            "/api/properties",
            new
            {
                name = "Lake house",
                address = "123 Main Street"
            });
        var property = await propertyResponse.Content
            .ReadFromJsonAsync<PropertyResponse>();

        var dueDate = new DateOnly(2026, 10, 1);
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/properties/{property!.Id}/maintenance-tasks",
            new
            {
                title = "Replace HVAC filter",
                description = "Use a 16x20 filter",
                dueDate
            });
        var createdTask = await createResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        var getResponse = await _client.GetAsync(
            $"/api/properties/{property.Id}/maintenance-tasks/{createdTask!.Id}");
        var retrievedTask = await getResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrievedTask);
        Assert.Equal("Replace HVAC filter", retrievedTask.Title);
        Assert.Equal("Use a 16x20 filter", retrievedTask.Description);
        Assert.Equal(dueDate, retrievedTask.DueDate);
        Assert.False(retrievedTask.IsCompleted);
        Assert.Equal(property.Id, retrievedTask.PropertyId);
    }

    [Fact]
    public async Task UpdateTask_PersistsEditedDetails()
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);
        var updatedDueDate = new DateOnly(2027, 4, 15);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/maintenance-tasks/{task.Id}",
            new
            {
                title = "Service the HVAC system",
                description = "Schedule the spring tune-up",
                dueDate = updatedDueDate
            });
        var updatedTask = await updateResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        var getResponse = await _client.GetAsync(
            $"/api/properties/{property.Id}/maintenance-tasks/{task.Id}");
        var retrievedTask = await getResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedTask);
        Assert.Equal("Service the HVAC system", updatedTask.Title);
        Assert.Equal("Schedule the spring tune-up", updatedTask.Description);
        Assert.Equal(updatedDueDate, updatedTask.DueDate);
        Assert.False(updatedTask.IsCompleted);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(updatedTask.Id, retrievedTask!.Id);
        Assert.Equal(updatedTask.Title, retrievedTask.Title);
        Assert.Equal(updatedTask.Description, retrievedTask.Description);
        Assert.Equal(updatedTask.DueDate, retrievedTask.DueDate);
    }

    [Fact]
    public async Task UpdateTaskStatus_CanCompleteAndReopenTask()
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);
        var taskUrl =
            $"/api/properties/{property.Id}/maintenance-tasks/{task.Id}";

        var completeResponse = await _client.PatchAsJsonAsync(
            $"{taskUrl}/status",
            new { isCompleted = true });
        var completedTask = await completeResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        var reopenResponse = await _client.PatchAsJsonAsync(
            $"{taskUrl}/status",
            new { isCompleted = false });
        var reopenedTask = await reopenResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        var getResponse = await _client.GetAsync(taskUrl);
        var retrievedTask = await getResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.True(completedTask!.IsCompleted);
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        Assert.False(reopenedTask!.IsCompleted);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.False(retrievedTask!.IsCompleted);
    }

    [Fact]
    public async Task DeleteTask_RemovesTaskFromItsProperty()
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);
        var taskUrl =
            $"/api/properties/{property.Id}/maintenance-tasks/{task.Id}";

        var deleteResponse = await _client.DeleteAsync(taskUrl);
        var getResponse = await _client.GetAsync(taskUrl);
        var list = await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(
            $"/api/properties/{property.Id}/maintenance-tasks");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task CreateProperty_WithMissingName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/properties",
            new { address = "123 Test Street" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithMissingRequiredFields_ReturnsBadRequest()
    {
        var property = await CreatePropertyAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/maintenance-tasks",
            new { description = "No title or due date" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithMissingRequiredFields_ReturnsBadRequest()
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/maintenance-tasks/{task.Id}",
            new { title = "Incomplete update" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_ForMissingProperty_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/properties/999999/maintenance-tasks",
            new
            {
                title = "Replace HVAC filter",
                description = "Use a 16x20 filter",
                dueDate = new DateOnly(2026, 10, 1)
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTask_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var property = await CreatePropertyAsync();

        var response = await _client.GetAsync(
            $"/api/properties/{property.Id}/maintenance-tasks/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TaskEndpoints_CannotAccessTaskThroughAnotherProperty()
    {
        var owningProperty = await CreatePropertyAsync();
        var otherProperty = await CreatePropertyAsync();
        var task = await CreateTaskAsync(owningProperty.Id);
        var wrongPropertyUrl =
            $"/api/properties/{otherProperty.Id}/maintenance-tasks/{task.Id}";

        var getResponse = await _client.GetAsync(wrongPropertyUrl);
        var updateResponse = await _client.PutAsJsonAsync(
            wrongPropertyUrl,
            new
            {
                title = "Wrong property update",
                description = "This must not be applied",
                dueDate = new DateOnly(2027, 1, 1)
            });
        var statusResponse = await _client.PatchAsJsonAsync(
            $"{wrongPropertyUrl}/status",
            new { isCompleted = true });
        var deleteResponse = await _client.DeleteAsync(wrongPropertyUrl);
        var ownerResponse = await _client.GetAsync(
            $"/api/properties/{owningProperty.Id}/maintenance-tasks/{task.Id}");
        var ownerTask = await ownerResponse.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>();

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal("Replace HVAC filter", ownerTask!.Title);
        Assert.False(ownerTask.IsCompleted);
    }

    [Theory]
    [InlineData(2026, 1, 31, 1, 2026, 2, 28)]
    [InlineData(2028, 2, 29, 12, 2029, 2, 28)]
    [InlineData(2026, 10, 1, 3, 2027, 1, 1)]
    public async Task RecurringTask_CreatesOneSuccessor(int year, int month, int day, int interval,
        int nextYear, int nextMonth, int nextDay)
    {
        var property = await CreatePropertyAsync();
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        var response = await _client.PostAsJsonAsync(url, new {
            title = "Recurring job", description = "Supplies", dueDate = new DateOnly(year, month, day),
            repeatEveryMonths = interval
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = (await response.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        Assert.Equal(interval, task.RepeatEveryMonths);
        foreach (var completed in new[] { true, true, false, true })
        {
            var status = await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = completed });
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        }
        var tasks = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!;
        Assert.Equal(2, tasks.Length);
        Assert.True(tasks.Single(t => t.Id == task.Id).IsCompleted);
        var next = tasks.Single(t => t.Id != task.Id);
        Assert.False(next.IsCompleted);
        Assert.Equal(new DateOnly(nextYear, nextMonth, nextDay), next.DueDate);
        Assert.Equal(interval, next.RepeatEveryMonths);
        Assert.Equal(task.Title, next.Title);
        Assert.Equal(task.Description, next.Description);
        Assert.Equal(property.Id, next.PropertyId);
        var nextStatus = await _client.PatchAsJsonAsync($"{url}/{next.Id}/status", new { isCompleted = true });
        Assert.Equal(HttpStatusCode.OK, nextStatus.StatusCode);
        Assert.Equal(3, (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!.Length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public async Task Recurrence_RejectsInvalidIntervals(int interval)
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        var payload = new { title = "Job", description = "Details", dueDate = new DateOnly(2026, 10, 1), repeatEveryMonths = interval };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(url, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"{url}/{task.Id}", payload)).StatusCode);
    }

    [Fact]
    public async Task Recurrence_CanBeEnabledAndDisabledThroughEditing()
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        foreach (var interval in new[] { 6, 0 })
        {
            var response = await _client.PutAsJsonAsync($"{url}/{task.Id}", new {
                title = task.Title, description = task.Description, dueDate = task.DueDate, repeatEveryMonths = interval
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(interval, (await _client.GetFromJsonAsync<MaintenanceTaskResponse>($"{url}/{task.Id}"))!.RepeatEveryMonths);
        }
        Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = true })).StatusCode);
        Assert.Single((await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!);
    }

    [Theory]
    [InlineData("untouched")]
    [InlineData("edited")]
    [InlineData("completed")]
    public async Task UndoRecurringCompletion_RemovesOnlyUntouchedSuccessor(string successorState)
    {
        var property = await CreatePropertyAsync();
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        var created = await _client.PostAsJsonAsync(url, new {
            title = "Undo test", description = "Details", dueDate = new DateOnly(2026, 10, 1), repeatEveryMonths = 3
        });
        var task = (await created.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
        var next = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!.Single(t => t.Id != task.Id);
        if (successorState == "edited")
        {
            (await _client.PutAsJsonAsync($"{url}/{next.Id}", new {
                title = "Changed successor", description = next.Description, dueDate = next.DueDate, repeatEveryMonths = 3
            })).EnsureSuccessStatusCode();
        }
        if (successorState == "completed")
        {
            (await _client.PatchAsJsonAsync($"{url}/{next.Id}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
        }
        var undo = await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = false });
        undo.EnsureSuccessStatusCode();
        var reopened = (await undo.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        Assert.False(reopened.IsCompleted);
        Assert.NotNull(reopened.StatusMessage);
        var remaining = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!;
        if (successorState == "untouched") Assert.Single(remaining);
        else Assert.Contains(remaining, t => t.Id == next.Id);
        // Retrying undo must not affect any other task.
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = false })).EnsureSuccessStatusCode();
        Assert.Equal(remaining.Length, (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!.Length);
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
        var final = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!;
        Assert.Equal(successorState == "untouched" ? 2 : remaining.Length, final.Length);
    }

    [Fact]
    public async Task UpcomingOverview_IncludesAllPropertiesThroughSevenDaysAndExcludesCompleted()
    {
        var first = await CreatePropertyAsync();
        var second = await CreatePropertyAsync();
        var today = new DateOnly(2026, 12, 28);
        var expected = new List<int>();
        foreach (var (propertyId, offset, completed) in new[] {
            (first.Id, -100, false), (second.Id, -1, false), (first.Id, 0, false),
            (second.Id, 1, false), (first.Id, 7, false), (second.Id, 8, false), (first.Id, 0, true)
        })
        {
            var response = await _client.PostAsJsonAsync($"/api/properties/{propertyId}/maintenance-tasks", new {
                title = "Overview test", description = "Details", dueDate = today.AddDays(offset)
            });
            response.EnsureSuccessStatusCode();
            var task = (await response.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
            if (completed) (await _client.PatchAsJsonAsync($"/api/properties/{propertyId}/maintenance-tasks/{task.Id}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
            else if (offset <= 7) expected.Add(task.Id);
        }
        var overview = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>("/api/maintenance-tasks/upcoming?today=2026-12-28"))!
            .Where(t => t.PropertyId == first.Id || t.PropertyId == second.Id).ToArray();
        Assert.Equal(expected, overview.Select(t => t.Id));
        Assert.All(overview, t => Assert.False(t.IsCompleted));
        var overdue = overview[0];
        var statusUrl = $"/api/properties/{overdue.PropertyId}/maintenance-tasks/{overdue.Id}/status";
        (await _client.PatchAsJsonAsync(statusUrl, new { isCompleted = true })).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>("/api/maintenance-tasks/upcoming?today=2026-12-28"))!, t => t.Id == overdue.Id);
        (await _client.PatchAsJsonAsync(statusUrl, new { isCompleted = false })).EnsureSuccessStatusCode();
        Assert.Contains((await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>("/api/maintenance-tasks/upcoming?today=2026-12-28"))!, t => t.Id == overdue.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?today=invalid")]
    [InlineData("?today=9999-12-31")]
    public async Task UpcomingOverview_RequiresValidLocalDate(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/maintenance-tasks/upcoming{query}")).StatusCode);
    }

    [Fact]
    public async Task CompletionHistory_RecordsTimePreservesRetriesAndClearsOnReopen()
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);
        var url = $"/api/properties/{property.Id}/maintenance-tasks/{task.Id}";
        Assert.Null(task.CompletedAt);
        var before = DateTimeOffset.UtcNow;
        var completedResponse = await _client.PatchAsJsonAsync($"{url}/status", new { isCompleted = true });
        completedResponse.EnsureSuccessStatusCode();
        var completed = (await completedResponse.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        Assert.NotNull(completed.CompletedAt);
        Assert.InRange(completed.CompletedAt.Value, before, DateTimeOffset.UtcNow);
        var edit = await _client.PutAsJsonAsync(url, new {
            title = task.Title, description = task.Description, dueDate = task.DueDate,
            completionNotes = "  Installed MERV 13 filter  "
        });
        edit.EnsureSuccessStatusCode();
        var retry = await _client.PatchAsJsonAsync($"{url}/status", new { isCompleted = true });
        retry.EnsureSuccessStatusCode();
        var retrieved = (await _client.GetFromJsonAsync<MaintenanceTaskResponse>(url))!;
        Assert.Equal(completed.CompletedAt, retrieved.CompletedAt);
        Assert.Equal("Installed MERV 13 filter", retrieved.CompletionNotes);
        var list = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>($"/api/properties/{property.Id}/maintenance-tasks"))!;
        Assert.Equal(retrieved.CompletedAt, Assert.Single(list).CompletedAt);
        Assert.Equal(retrieved.CompletionNotes, list[0].CompletionNotes);
        (await _client.PatchAsJsonAsync($"{url}/status", new { isCompleted = false })).EnsureSuccessStatusCode();
        var reopened = (await _client.GetFromJsonAsync<MaintenanceTaskResponse>(url))!;
        Assert.Null(reopened.CompletedAt);
        Assert.Null(reopened.CompletionNotes);
        var recompletedAt = DateTimeOffset.UtcNow;
        (await _client.PatchAsJsonAsync($"{url}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
        var recompleted = (await _client.GetFromJsonAsync<MaintenanceTaskResponse>(url))!;
        Assert.InRange(recompleted.CompletedAt!.Value, recompletedAt, DateTimeOffset.UtcNow);
        Assert.Null(recompleted.CompletionNotes);
    }

    [Fact]
    public async Task CompletionNotes_ValidateLengthAndCanBeCleared()
    {
        var property = await CreatePropertyAsync();
        var task = await CreateTaskAsync(property.Id);
        var url = $"/api/properties/{property.Id}/maintenance-tasks/{task.Id}";
        object Payload(string notes) => new { title = task.Title, description = task.Description, dueDate = task.DueDate, completionNotes = notes };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync(url, Payload("Notes before completion"))).StatusCode);
        (await _client.PatchAsJsonAsync($"{url}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync(url, Payload(new string('x', 1001)))).StatusCode);
        (await _client.PutAsJsonAsync(url, Payload(new string('x', 1000)))).EnsureSuccessStatusCode();
        Assert.Equal(1000, (await _client.GetFromJsonAsync<MaintenanceTaskResponse>(url))!.CompletionNotes!.Length);
        (await _client.PutAsJsonAsync(url, Payload(""))).EnsureSuccessStatusCode();
        Assert.Null((await _client.GetFromJsonAsync<MaintenanceTaskResponse>(url))!.CompletionNotes);
    }

    [Theory]
    [InlineData(false, "2026-02-28")]
    [InlineData(true, "2026-04-10")]
    public async Task Planning_UsesSelectedRecurrenceBasisAndCopiesDetails(bool fromCompletion, string expectedDate)
    {
        var property = await CreatePropertyAsync();
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        var response = await _client.PostAsJsonAsync(url, new { title = "Planning", description = "Details", dueDate = "2026-01-31", repeatEveryMonths = 1,
            repeatFromCompletion = fromCompletion, estimatedMinutes = 15, supplies = "16x20 filter", instructions = "See manual", lastPurchase = "Store, January" });
        response.EnsureSuccessStatusCode();
        var task = (await response.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = true, localDate = "2026-03-10" })).EnsureSuccessStatusCode();
        var next = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!.Single(t => !t.IsCompleted);
        Assert.Equal(DateOnly.Parse(expectedDate), next.DueDate);
        Assert.Equal(15, next.EstimatedMinutes);
        Assert.Equal("16x20 filter", next.Supplies);
        Assert.Equal("See manual", next.Instructions);
        Assert.Equal("Store, January", next.LastPurchase);
        Assert.Equal(fromCompletion, next.RepeatFromCompletion);
        if (!fromCompletion)
        {
            (await _client.PatchAsJsonAsync($"{url}/{next.Id}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
            var third = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!.Single(t => !t.IsCompleted);
            Assert.Equal(new DateOnly(2026, 3, 31), third.DueDate);
        }
    }

    [Fact]
    public async Task Planning_PostponePreservesCadenceAndSkipCanBeUndone()
    {
        var property = await CreatePropertyAsync();
        var other = await CreatePropertyAsync();
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        var response = await _client.PostAsJsonAsync(url, new { title = "Annual check", description = "Details", dueDate = "2026-10-01", repeatEveryMonths = 12 });
        var task = (await response.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsJsonAsync($"/api/properties/{other.Id}/maintenance-tasks/{task.Id}/postpone", new { dueDate = "2026-10-10" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PatchAsJsonAsync($"{url}/{task.Id}/postpone", new { dueDate = "2026-09-01" })).StatusCode);
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/postpone", new { dueDate = "2026-10-10" })).EnsureSuccessStatusCode();
        foreach (var attempt in new[] { 1, 2 })
            (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = true, isSkipped = true })).EnsureSuccessStatusCode();
        var tasks = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!;
        Assert.Equal(2, tasks.Length);
        var skipped = tasks.Single(t => t.Id == task.Id);
        Assert.True(skipped.IsSkipped);
        Assert.Null(skipped.CompletedAt);
        Assert.Equal(new DateOnly(2027, 10, 1), tasks.Single(t => !t.IsCompleted).DueDate);
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = false })).EnsureSuccessStatusCode();
        var reopened = Assert.Single((await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!);
        Assert.False(reopened.IsCompleted);
        Assert.False(reopened.IsSkipped);
    }

    [Fact]
    public async Task Planning_PreviousNotesRemainAvailableOnNextOccurrence()
    {
        var property = await CreatePropertyAsync();
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        var response = await _client.PostAsJsonAsync(url, new { title = "Filter", description = "Details", dueDate = "2026-10-01", repeatEveryMonths = 3 });
        var task = (await response.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"{url}/{task.Id}", new { title = task.Title, description = task.Description, dueDate = task.DueDate, repeatEveryMonths = 3, completionNotes = "Installed new filter" })).EnsureSuccessStatusCode();
        var next = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!.Single(t => !t.IsCompleted);
        Assert.Equal("Installed new filter", next.PreviousCompletionNotes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public async Task Planning_ValidatesEstimatedTime(int minutes)
    {
        var property = await CreatePropertyAsync();
        var response = await _client.PostAsJsonAsync($"/api/properties/{property.Id}/maintenance-tasks", new { title = "Invalid estimate", description = "Details", dueDate = "2026-10-01", estimatedMinutes = minutes });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("2026-02-01", "2026-02-28")]
    [InlineData("2026-02-28", "2026-03-31")]
    [InlineData("2026-03-01", "2026-03-31")]
    public async Task Planning_PostponeAcrossMonthsUsesNextScheduledDate(string postponedDate, string expectedDate)
    {
        var property = await CreatePropertyAsync();
        var url = $"/api/properties/{property.Id}/maintenance-tasks";
        var created = await _client.PostAsJsonAsync(url, new {
            title = "Monthly check", description = "Details", dueDate = "2026-01-31", repeatEveryMonths = 1
        });
        created.EnsureSuccessStatusCode();
        var task = (await created.Content.ReadFromJsonAsync<MaintenanceTaskResponse>())!;
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/postpone", new { dueDate = postponedDate })).EnsureSuccessStatusCode();
        (await _client.PatchAsJsonAsync($"{url}/{task.Id}/status", new { isCompleted = true })).EnsureSuccessStatusCode();
        var next = (await _client.GetFromJsonAsync<MaintenanceTaskResponse[]>(url))!.Single(t => !t.IsCompleted);
        Assert.Equal(DateOnly.Parse(expectedDate), next.DueDate);
    }

    private async Task<PropertyResponse> CreatePropertyAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/properties",
            new
            {
                name = $"Test property {Guid.NewGuid()}",
                address = "123 Test Street"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PropertyResponse>())!;
    }

    private async Task<MaintenanceTaskResponse> CreateTaskAsync(int propertyId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/properties/{propertyId}/maintenance-tasks",
            new
            {
                title = "Replace HVAC filter",
                description = "Use a 16x20 filter",
                dueDate = new DateOnly(2026, 10, 1)
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content
            .ReadFromJsonAsync<MaintenanceTaskResponse>())!;
    }

    private sealed record PropertyResponse(
        int Id,
        string Name,
        string Address);
}
