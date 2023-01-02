
using Account.API.Extensions;
using Account.API.Middlewares;
using KissLog;
using KissLog.AspNetCore;
using KissLog.CloudListeners.Auth;
using KissLog.CloudListeners.RequestLogsListener;
using Shared.Data.Contexts;

var builder = WebApplication.CreateBuilder(args);

string myAllowSpecificOrigins = "_myAllowSpecificOrigins";
IConfiguration config = builder.Configuration;

builder.Services.AddDbContext<ApplicationDbContext>();
builder.Services.AddSingleton(config);

ServiceExtension.RegisterServices(builder.Services, config, myAllowSpecificOrigins);

builder.Services.AddEndpointsApiExplorer();



void ConfigureKissLog(IOptionsBuilder options)
{
    KissLogConfiguration.Listeners
        .Add(new RequestLogsApiListener(new Application(config["KissLog.OrganizationId"], config["KissLog.ApplicationId"]))
        {
            ApiUrl = config["KissLog.ApiUrl"]
        });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseAuthentication();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AccountMicroservice v1");
    c.RoutePrefix = string.Empty;
});


app.UseMiddleware<ExceptionMiddleware>();
app.UseKissLogMiddleware(options => ConfigureKissLog(options));
app.UseHttpsRedirection();

app.UseRouting();
app.UseCors(myAllowSpecificOrigins);

app.UseAuthorization();

app.MapControllers();

app.Run();
