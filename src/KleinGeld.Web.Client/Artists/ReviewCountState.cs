using System.Net.Http.Json;
using KleinGeld.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace KleinGeld.Web.Client.Artists;

/// <summary>
/// How much waits on the Review page, for the badge on the Admin link. Zero for anyone but the Admin. The sidebar also
/// renders on the server for Identity's static pages, so the host has its own implementation.
/// </summary>
public interface IReviewCountState
{
    int Count { get; }

    event Action? Changed;

    Task LoadAsync();
}

/// <summary>Asks the API, and tells the sidebar when the answer may have changed.</summary>
public sealed class ReviewCountState(HttpClient http, AuthenticationStateProvider authentication) : IReviewCountState
{
    public int Count { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync()
    {
        var state = await authentication.GetAuthenticationStateAsync();
        if (!state.User.IsInRole(Roles.Admin))
        {
            return;
        }

        Count = await http.GetFromJsonAsync<int>("api/admin/review/count");
        Changed?.Invoke();
    }
}
