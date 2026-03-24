using App;
using Microsoft.AspNetCore.Mvc;
using RStreamCore.Contracts;
using RStreamCore.DependencyInjection;
using RStreamCore.Engine;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddRStreamCore("localhost:6379", opts =>
{
    opts.ConcurrentWorkers = 4;
    opts.Retry = new RetryPolicy
    {
        MaxAttempts = 5,
        BaseDelayMs = 1000
    };
});

builder.Services.AddScoped<OrderCreatedHandler>();
builder.Services.AddHostedService<OrderSubscriberService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/publish", async ([FromServices] IEventBus eventbus) =>
{
    await eventbus.PublishAsync(new OrderCreatedEvent("order-42", 299.90m));
    return "";
});

app.Run();
