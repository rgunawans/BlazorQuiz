var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<QuizHub>("/quizhub");

app.Run();