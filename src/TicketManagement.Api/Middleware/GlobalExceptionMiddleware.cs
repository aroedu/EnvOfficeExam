using Microsoft.AspNetCore.Mvc;
using TicketManagement.Api.Exceptions;

namespace TicketManagement.Api.Middleware;

// Maps domain exceptions to RFC 7807 ProblemDetails responses (section 2).
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (TicketNotFoundException ex)
        {
            await WriteProblem(context, StatusCodes.Status404NotFound, "Ticket not found", ex.Message);
        }
        catch (InvalidStatusTransitionException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict, "Invalid status transition", ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict, "Concurrency conflict", ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected/cancelled the request; nothing to write back.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext context, int statusCode, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problem);
    }
}
