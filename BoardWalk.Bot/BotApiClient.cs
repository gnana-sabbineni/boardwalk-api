using System.Net.Http.Json;
using System.Text.Json;
using BoardWalk.Monopoly.Contracts;

namespace BoardWalk.Bot
{
    /// <summary>The API's standard response wrapper.</summary>
    public sealed class ApiEnvelope<T>
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public int StatusCode { get; set; }
    }

    public sealed record ApiResult<T>(bool Ok, T? Data, string Message);

    /// <summary>
    /// The bot's only way of touching the game: the same public HTTP API a browser uses. It identifies itself with the
    /// shared bot key and names the seat it plays for in X-Act-As.
    /// </summary>
    public sealed class BotApiClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public BotApiClient(HttpClient http) => _http = http;

        public async Task<List<BotTurnResponse>> GetTurnsAsync(CancellationToken ct)
        {
            var envelope = await _http.GetFromJsonAsync<ApiEnvelope<List<BotTurnResponse>>>("api/bot/turns", Json, ct);
            return envelope?.Data ?? new List<BotTurnResponse>();
        }

        public async Task<BoardResponse> GetBoardAsync(CancellationToken ct)
        {
            var envelope = await _http.GetFromJsonAsync<ApiEnvelope<BoardResponse>>("api/games/board", Json, ct);
            return envelope?.Data ?? throw new InvalidOperationException("The API returned no board.");
        }

        /// <summary>POSTs to /api/games/{path} as the given seat. Rule violations come back as Ok = false with the reason.</summary>
        public async Task<ApiResult<JsonElement>> PostAsync(Guid seatUserId, string path, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/games/{path}");
            request.Headers.Add("X-Act-As", seatUserId.ToString());

            using var response = await _http.SendAsync(request, ct);
            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>(Json, ct);

            return new ApiResult<JsonElement>(
                response.IsSuccessStatusCode && envelope?.IsSuccess == true,
                envelope?.Data ?? default,
                envelope?.Message ?? response.ReasonPhrase ?? "No response.");
        }
    }
}
