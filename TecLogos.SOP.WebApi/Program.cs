using Serilog;
using System.Text;
using Serilog.Events;
using TecLogos.SOP.AuthBAL;
using TecLogos.SOP.BAL.Auth;
using TecLogos.SOP.BAL.SOP;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using TecLogos.SOP.DataModel.Auth;
using Microsoft.IdentityModel.Tokens;
//using TecLogos.SOP.WebApi.Middleware;
using TecLogos.SOP.DAL.Auth;
using TecLogos.SOP.DAL.SOP;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TecLogos.SOP.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logBasePath = @"C:\Application-Logs\TecLogos-SOP";

            Directory.CreateDirectory(Path.Combine(logBasePath, "Debug"));
            Directory.CreateDirectory(Path.Combine(logBasePath, "Error"));
            Directory.CreateDirectory(Path.Combine(logBasePath, "Warning"));

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()

                // DEBUG LOGS
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Debug)
                    .WriteTo.Async(a => a.File(
                        Path.Combine(logBasePath, "Debug", "teclogos-SOP-debug-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")))

                // WARNING LOGS
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
                    .WriteTo.Async(a => a.File(
                        Path.Combine(logBasePath, "Warning", "teclogos-SOP-warning-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")))

                // ERROR LOGS
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                    .WriteTo.Async(a => a.File(
                        Path.Combine(logBasePath, "Error", "teclogos-SOP-error-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 90,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")))

                .CreateLogger();


            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog();
            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var secretKey = jwtSettings["Key"]; 
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            if (string.IsNullOrEmpty(secretKey))
                throw new InvalidOperationException("JWT Key missing in appsettings.json");

            if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
                throw new InvalidOperationException("JWT configuration is missing in appsettings.json");
            }

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var key = Encoding.UTF8.GetBytes(secretKey);
            
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
            
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
            
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
            
                    ValidateAudience = true,
                    ValidAudience = audience,
            
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
            
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClient", policy =>
                {
                    policy.WithOrigins("http://localhost:5173", "https://localhost:5173", "https://teclogoshr.netlify.app")
                          .AllowCredentials()
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .WithExposedHeaders("Content-Disposition");
                });
            });

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "TecLogos SOP API",
                    Version = "v1",
                    Description = "Complete Authentication & Authorization API with Employee Management"
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Enter your JWT token in the format: Bearer {token}"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] { }
                    }
                });
            });

            ServiceRegistration(builder);

            var app = builder.Build();


            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            app.UseCors("AllowClient");
            app.UseSerilogRequestLogging();
            //app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }

        private static void ServiceRegistration(WebApplicationBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();

            //  Auth
            builder.Services.AddScoped<IAuthBAL, TecLogos.SOP.BAL.Auth.AuthBAL>();
            builder.Services.AddScoped<IAuthDAL, AuthDAL>();

            //  Employee
            builder.Services.AddScoped<IEmployeeBAL, EmployeeBAL>();
            builder.Services.AddScoped<IEmployeeDAL, EmployeeDAL>();

            //  Employee Group
            builder.Services.AddScoped<IEmployeeGroupBAL, EmployeeGroupBAL>();
            builder.Services.AddScoped<IEmployeeGroupDAL, EmployeeGroupDAL>();

            builder.Services.AddScoped<IEGDetailBAL, EGDetailBAL>();
            builder.Services.AddScoped<IEGDetailDAL, EGDetailDAL>();

            //  Roles
            builder.Services.AddScoped<IRolesBAL, RolesBAL>();
            builder.Services.AddScoped<IRolesDAL, RolesDAL>();


            //  Employee Roles
            builder.Services.AddScoped<IEmployeeRoleBAL, EmployeeRoleBAL>();
            builder.Services.AddScoped<IEmployeeRoleDAL, EmployeeRoleDAL>();


            //  EmployeeDDL
            builder.Services.AddScoped<IEmployeeDDLBAL, EmployeeDDLBAL>();
            builder.Services.AddScoped<IEmployeeDDLDAL, EmployeeDDLDAL>();

            //  User
            builder.Services.AddScoped<IUserContextBAL, UserContextBAL>();


            //On Boarding
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
            builder.Services.AddScoped<IPasswordHasherBAL, PasswordHasherBAL>();
            builder.Services.AddScoped<IEmailBAL, EmailBAL>();
            builder.Services.AddScoped<IAuthOnboardingBAL, AuthOnboardingBAL>();
            builder.Services.AddScoped<IAuthOnboardingDAL, AuthOnboardingDAL>();

            //SOP
            builder.Services.AddScoped<ISopDetailDAL, SopDetailDAL>();
            builder.Services.AddScoped<ISopDetailBAL, SopDetailBAL>();

        }
    }
}
