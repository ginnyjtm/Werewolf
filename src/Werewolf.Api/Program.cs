using System.Text.Json.Serialization;
using Werewolf.Api.Hubs;
using Werewolf.Api.Rooms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR().AddJsonProtocol(o =>
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())); // "Werewolf", not 1

builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddHostedService<PhaseTicker>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));   // SignalR needs credentials allowed

var app = builder.Build();

app.UseCors();
app.MapGet("/", () => "Werewolf server is running 🐺");
app.MapHub<GameHub>("/hubs/game");

app.Run();