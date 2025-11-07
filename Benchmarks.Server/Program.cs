using Benchmarks.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddControllers();

var app = builder.Build();

app.MapGrpcService<GrpcCalculatorService>();
app.MapControllers();

app.Run();
