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
