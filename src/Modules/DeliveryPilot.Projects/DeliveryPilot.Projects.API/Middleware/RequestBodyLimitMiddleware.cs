using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.API.Middleware;

public sealed class RequestBodyLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            var error = new ApiErrorResponse(
                "Request.PayloadTooLarge",
                "The request body exceeds the permitted size.",
                TraceId: context.TraceIdentifier);
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(error), context.RequestAborted);
        }
    }
}
