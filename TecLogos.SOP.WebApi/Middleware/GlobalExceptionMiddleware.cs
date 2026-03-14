using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Text.Json;
using TecLogos.SOP.Common.Exceptions;

namespace TecLogos.SOP.WebApi.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var userId = context.User?.FindFirst("Id")?.Value ?? "Anonymous";

                _logger.LogDebug(
                    "➡️ Request Started | {Method} {Path} | User:{UserId} | CorrelationId:{CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    userId,
                    correlationId);

                await _next(context);

                stopwatch.Stop();

                _logger.LogDebug(
                    "✅ Request Finished | Status:{StatusCode} | Duration:{Duration}ms | CorrelationId:{CorrelationId}",
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            catch (ConcurrencyException ex)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = ex.Message,
                    correlationId
                });
            }
            catch (SqlException ex)
            {
                await HandleSqlException(context, ex, correlationId);
            }
            catch (ArgumentException ex)
            {
                await HandleBadRequest(context, ex, correlationId);
            }
            // Generic Exception
            catch (Exception ex)
            {
                await HandleServerError(context, ex, correlationId);
            }
        }

        private async Task HandleSqlException(HttpContext context, SqlException ex, string correlationId)
        {
            _logger.LogError(ex, "❌ SQL Exception | CorrelationId:{CorrelationId}", correlationId);

            context.Response.ContentType = "application/json";

            if (ex.Number == 50001)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = ex.Message,
                    correlationId
                }));
                return;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                message = "Database error occurred",
                correlationId
            }));
        }

        private async Task HandleBadRequest(HttpContext context, ArgumentException ex, string correlationId)
        {
            _logger.LogWarning(ex, "⚠️ Bad Request | CorrelationId:{CorrelationId}", correlationId);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                message = ex.Message,
                correlationId
            }));
        }

        private async Task HandleServerError(HttpContext context, Exception ex, string correlationId)
        {
            _logger.LogError(ex, "🔥 Unhandled Exception | CorrelationId:{CorrelationId}", correlationId);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                message = "Internal server error",
                correlationId
            }));
        }
    }
}