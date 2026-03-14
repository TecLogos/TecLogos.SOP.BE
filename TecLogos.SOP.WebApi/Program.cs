using TecLogos.SOP.WebApi.Extensions;
using TecLogos.SOP.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSwaggerWithJwt();

// Fix: resolve Swagger schemaId conflict between
// TecLogos.SOP.WebModel.SOP.Role and TecLogos.SOP.DataModel.Auth.Role
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type =>
        type.FullName!.Replace("+", ".").Replace("TecLogos.SOP.", ""));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TecLogos SOP API v1");
    c.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();