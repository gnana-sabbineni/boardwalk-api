using BoardWalk.Monopoly.Abstractions;

namespace BoardWalk.Api.Services.Implementations
{
    /// <summary>Tells the game whether the current request comes from the trusted bot service (see BotKeyAuthenticationHandler).</summary>
    public class GameActorContext : IGameActorContext
    {
        private readonly IHttpContextAccessor _http;

        public GameActorContext(IHttpContextAccessor http) => _http = http;

        public bool IsBotService => _http.HttpContext?.User.HasClaim(BotAuth.ActorClaim, BotAuth.BotActor) == true;
    }
}
