using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TecLogos.SOP.Auth.Helpers;
using TecLogos.SOP.Auth.Services;
using TecLogos.SOP.BAL.SOP;
using TecLogos.SOP.DAL.SOP;

namespace TecLogos.SOP.WebApi.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services, IConfiguration configuration)
        {
            // ── JWT Settings ──────────────────────────────────────────────────
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            // ── JWT Authentication ────────────────────────────────────────────
            var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // ── Authorization ─────────────────────────────────────────────────
            services.AddAuthorization();

            // ── DAL (pure ADO.NET — no DbContext) ────────────────────────────
            services.AddScoped<IAuthDAL, AuthDAL>();
            services.AddScoped<IEmployeeDAL, EmployeeDAL>();
            services.AddScoped<IEmployeeDDLDAL, EmployeeDDLDAL>();
            services.AddScoped<IEmployeeRoleDAL, EmployeeRoleDAL>();
            services.AddScoped<IEmployeeGroupDAL, EmployeeGroupDAL>();
            services.AddScoped<IEGDetailDAL, EGDetailDAL>();
            services.AddScoped<IRoleDAL, RoleDAL>();
            services.AddScoped<ISopDetailDAL, SopDetailDAL>();

            // ── BAL ───────────────────────────────────────────────────────────
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthBAL, AuthBAL>();
            services.AddScoped<IEmployeeBAL, EmployeeBAL>();
            services.AddScoped<IEmployeeDDLBAL, EmployeeDDLBAL>();
            services.AddScoped<IEmployeeRoleBAL, EmployeeRoleBAL>();
            services.AddScoped<IEmployeeGroupBAL, EmployeeGroupBAL>();
            services.AddScoped<IEGDetailBAL, EGDetailBAL>();
            services.AddScoped<IRoleBAL, RoleBAL>();
            services.AddScoped<ISopDetailBAL, SopDetailBAL>();

            services.AddHttpContextAccessor();

            return services;
        }

        public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "TecLogos SOP API",
                    Version = "v1",
                    Description = "Digital SOP Management System — Pure ADO.NET"
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter: Bearer {your JWT token}"
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
                        Array.Empty<string>()
                    }
                });
            });

            return services;
        }
    }
}
