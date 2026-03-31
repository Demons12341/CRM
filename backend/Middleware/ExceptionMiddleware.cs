using System.Net;
using System.Text.Json;

namespace ProjectManagementSystem.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发生未处理的异常");
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = exception switch
            {
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var friendlyMessage = exception switch
            {
                UnauthorizedAccessException => "当前操作未授权，请重新登录后再试",
                KeyNotFoundException => exception.Message,
                InvalidOperationException => exception.Message,
                ArgumentException => exception.Message,
                _ => "系统繁忙，请稍后重试"
            };

            var response = new
            {
                success = false,
                message = friendlyMessage,
                statusCode = context.Response.StatusCode
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
