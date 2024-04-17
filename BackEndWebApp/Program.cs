using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());

var app = builder.Build();

app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));

app.Use(async (context, next) =>
{
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

var todos = new List<Todo>();

app.MapGet("/todos", (ITaskService service)  => service.GetTodos());

//app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService service) =>
app.MapGet("/todos/{id}", async(int id, ITaskService service) =>
{
    var targetTodo = service.GetTodoById(id);
    if (targetTodo is null)
    {
        return TypedResults.NotFound();
    }
    else
    {
        return TypedResults.Ok(targetTodo);
    }
});

app.MapPost("/todos", (Todo task, ITaskService service) =>
{
    service.AddTodo(task);
    todos.Add(task);
    return TypedResults.Created("/todos/{id}", task);
})

.AddEndpointFilter(async (context, next) =>
 {
     var taskArgument = context.GetArgument<Todo>(0);
     var errors = new Dictionary<string, string[]>();
     if (taskArgument.DueDate < DateTime.UtcNow)
     {
         errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past."]);
     }

     if (taskArgument.IsCompleted)
     {
         errors.Add(nameof(Todo.IsCompleted), ["Cannot add completed todo."]);
     }

     if (errors.Count > 0)
     {
         return Results.ValidationProblem(errors);
     }
     return await next(context);
 });

app.MapDelete("/todos/{id}", (int id, ITaskService service) => 
{ 
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.Run();

public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted);

interface ITaskService
{
    void AddTodo(Todo task);
    void DeleteTodoById(int id);
    object GetTodoById(int id);
    object GetTodos();
}

class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos = [];
    public Todo? GetTodoById(int id)
    {
        return _todos.SingleOrDefault(t => id == t.Id);
    }
    public List<Todo> GetTodos()
    {
        return _todos;
    }
    public void DeleteTodoById(int id)
    {
        _todos.RemoveAll(task => id == task.Id);
    }
    public Todo AddTodo(Todo task)
    {
        _todos.Add(task);
        return task;
    }

    void ITaskService.AddTodo(Todo task)
    {
        throw new NotImplementedException();
    }

    object ITaskService.GetTodoById(int id)
    {
        throw new NotImplementedException();
    }

    object ITaskService.GetTodos()
    {
        throw new NotImplementedException();
    }
}

