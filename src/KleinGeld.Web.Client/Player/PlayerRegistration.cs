using Microsoft.Extensions.DependencyInjection;

namespace KleinGeld.Web.Client.Player;

public static class PlayerRegistration
{
    /// <summary>
    /// The player, one per tab. MainLayout also renders on the server for Identity's pages, so both hosts call this,
    /// each with its own <see cref="IPlayLinkSource"/>.
    /// </summary>
    public static void AddPlayer(this IServiceCollection services)
    {
        services.AddScoped<JsAudioElement>();
        services.AddScoped<IAudioElement>(provider => provider.GetRequiredService<JsAudioElement>());
        services.AddScoped<IPlayerStorage, JsPlayerStorage>();
        services.AddScoped<PlayerService>();
    }
}
