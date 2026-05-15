using NotificationService.Domain;

namespace NotificationService.Api.Extensions
{
    public static class ResultExtensions
    {
        public static IResult ToProblemDetails(this Result result)
        {
            if (result.IsSuccess)
            {
                throw new InvalidOperationException("Can't convert success result to problem details");
            }

            return Results.Problem(
                statusCode: GetStatusCode(result.Error.Type),
                title: GetTitle(result.Error.Type),
                type: GetType(result.Error.Type),
                extensions: new Dictionary<string, object?>
                {
                { "errors", new[] { result.Error } }
                });
        }

        private static int GetStatusCode(ErrorType type)
        {
            return type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.Failure => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status500InternalServerError
            };
        }

        private static string GetTitle(ErrorType type)
        {
            return type switch
            {
                ErrorType.Validation => "Bad Request",
                ErrorType.NotFound => "Not Found",
                ErrorType.Conflict => "Conflict",
                ErrorType.Unauthorized => "Unauthorized",
                ErrorType.Failure => "Server Failure",
                _ => "Server Error"
            };
        }

        private static string GetType(ErrorType type)
        {
            return type switch
            {
                ErrorType.Validation => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                ErrorType.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                ErrorType.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            };
        }
    }
}