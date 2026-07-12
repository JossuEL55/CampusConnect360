using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("Gateway");
var app = builder.Build();
app.UseCampusRequestLogging();

app.MapGet("/", () => "Hello World!");

app.Run();
