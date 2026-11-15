using BoardWalk.Monopoly.Abstractions;
using BoardWalk.Monopoly.Data;
using BoardWalk.Monopoly.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BoardWalk.Monopoly
{
    public static class MonopolyServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Monopoly game module. The host must separately provide: IGameRealtimeNotifier,
        /// IGameCompletionHandler and IGameActorContext (see Abstractions/), and call
        /// modelBuilder.ApplyMonopolyModel() in its DbContext. A host IGameLock (e.g. Redis) registered BEFORE this call wins;
        /// otherwise the in-memory lock is used (single server only).
        /// </summary>
        /// <typeparam name="TContext">The host's DbContext that contains the Monopoly tables.</typeparam>
        public static IServiceCollection AddMonopolyGame<TContext>(this IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext
        {
            services.Configure<GameOptions>(configuration.GetSection("Monopoly"));

            services.AddScoped(sp => new GameStore(sp.GetRequiredService<TContext>()));
            services.AddScoped<IGameService, GameService>();

            services.TryAddSingleton<IDiceRoller, SeededDiceRoller>();
            services.TryAddSingleton<IGameLock, InMemoryGameLock>();

            services.AddHostedService<TurnTimerService>();
            return services;
        }
    }
}
