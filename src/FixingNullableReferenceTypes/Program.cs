using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x => x.SchemaFilter<RequiredSchemaFilter>());

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();


app.MapPost("/greeting", (GreetingRequest request) =>
    {
        var loud = $"HELLO {request.Name.ToUpper()}";
        return new GreetingResponse { Greeting = loud };
    })
    .AddEndpointFilter<EnsureRequiredProps>()
    .WithOpenApi();

app.Run();

public record GreetingRequest
{
    public required string Name { get; set; }
}

public record GreetingResponse
{
    public required string Greeting { get; set; }
}

public class EnsureRequiredProps : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.FirstOrDefault();
        if (request is null)
            return await next(context);

        // Find all properties on the current type with the 'required' keyword
        // Find all values on the current request that are null
        // Transform into a Dictionary for ValidationProblem

        var nullFailures = request.GetType().GetProperties()
            .Where(x => x.GetCustomAttribute<RequiredMemberAttribute>() is not null)
            .Where(x => x.GetValue(request) is null)
            .ToDictionary(x => x.Name, x => new[] { $"{x.Name} is required. It can't be deserialzed to null." });

        if (nullFailures.Any())
            return TypedResults.ValidationProblem(nullFailures);

        return await next(context);
    }
}

public class RequiredSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Find all properties on the current type with the 'required' keyword
        var requiredClrProps = context.Type.GetProperties()
            .Where(x => x.GetCustomAttribute<RequiredMemberAttribute>() is not null)
            .ToList();

        // Find all the matching properties in the openAPI schema (adjust for case differences)
        var requiredJsonProps = schema.Properties
            .Where(j => requiredClrProps.Any(p => p.Name == j.Key || p.Name == ToPascalCase(j.Key)))
            .ToList();

        // Set properties as required
        schema.Required = requiredJsonProps.Select(x => x.Key).ToHashSet();

        // Set them non nullable too
        foreach (var requiredJsonProp in requiredJsonProps)
            requiredJsonProp.Value.Nullable = false;
    }

    private string ToPascalCase(string str)
    {
        return char.ToUpper(str[0]) + str.Substring(1);
    }
}