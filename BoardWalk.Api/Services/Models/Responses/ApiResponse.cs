namespace BoardWalk.Api.Services.Models.Responses
{
    /// <summary>
    /// Standard response envelope returned by every API endpoint. Wraps whatever
    /// data an endpoint produces (if any) alongside a success flag, a human-readable
    /// message, and the HTTP status code — so the frontend can handle every response
    /// the same way regardless of which endpoint it called.
    /// </summary>
    /// <typeparam name="T">The shape of the data being returned, e.g. UserResponse, List&lt;FriendResponse&gt;.</typeparam>
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public int StatusCode { get; set; }

        // Private constructor: forces every ApiResponse to be built through the
        // Success/Fail factory methods below, rather than assembled by hand with
        // `new ApiResponse<T> { ... }` scattered everywhere — this way the rules
        // for "what does success look like" live in exactly one place.
        private ApiResponse() { }

        public static ApiResponse<T> Success(T? data, string message = "Request successful.", int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                Message = message,
                Data = data,
                StatusCode = statusCode
            };
        }

        public static ApiResponse<T> Fail(string message, int statusCode = 400)
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                Message = message,
                Data = default, // null for reference types, e.g. default(UserResponse) == null
                StatusCode = statusCode
            };
        }
    }
}