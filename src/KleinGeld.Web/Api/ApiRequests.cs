namespace KleinGeld.Web.Api;

internal static class ApiRequests
{
    /// <summary>True for calls to the JSON API under <c>/api</c>, which answer with bare status codes, never pages.</summary>
    public static bool IsApiRequest(this HttpRequest request) => request.Path.StartsWithSegments("/api");
}
