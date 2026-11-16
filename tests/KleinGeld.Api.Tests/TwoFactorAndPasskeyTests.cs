using System.Buffers.Binary;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

/// <summary>The optional extras on the Manage pages, used by the Admin, who is required to use neither.</summary>
[Collection(KleinGeldAppCollection.Name)]
public sealed partial class TwoFactorAndPasskeyTests(KleinGeldApp app)
{
    private DateTimeOffset _now;

    [Fact]
    public async Task Two_factor_can_be_turned_on_then_asks_for_a_code_at_login_and_can_be_turned_off()
    {
        // The code depends on the time, so the app's clock has to run close to the one this test computes it from.
        _now = DateTimeOffset.UtcNow;
        await app.SetClockAsync(_now);
        using var admin = await app.LoginAsAdminAsync();

        var setupPage = await admin.GetStringAsync("/Account/Manage/EnableAuthenticator");
        var key = SharedKey().Match(setupPage).Groups["key"].Value.Replace(" ", "");
        await AssertStillLoggedInAsync(admin);
        Assert.Contains("otpauth://totp/KleinGeld%20Music:", WebUtility.HtmlDecode(setupPage));
        var form = await IdentityForms.ReadAsync(admin, "/Account/Manage/EnableAuthenticator");
        form["Input.Code"] = CurrentCode(key);
        using var enabled = await admin.PostAsync("/Account/Manage/EnableAuthenticator", new FormUrlEncodedContent(form));
        Assert.Contains("Put these codes in a safe place", await enabled.Content.ReadAsStringAsync());
        await AssertStillLoggedInAsync(admin);

        try
        {
            using var browser = app.CreateWebClient();
            var login = await IdentityForms.ReadAsync(browser, "/Account/Login");
            login["Input.Email"] = KleinGeldApp.AdminEmail;
            login["Input.Password"] = KleinGeldApp.AdminPassword;
            using var passwordAccepted = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(login));
            var codePage = passwordAccepted.RequestMessage!.RequestUri!.PathAndQuery;
            Assert.StartsWith("/Account/LoginWith2fa", codePage);
            using var notYet = await browser.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.Unauthorized, notYet.StatusCode);

            var code = await IdentityForms.ReadAsync(browser, codePage);
            code["Input.TwoFactorCode"] = CurrentCode(key);
            using var codeAccepted = await browser.PostAsync(codePage, new FormUrlEncodedContent(code));
            codeAccepted.EnsureSuccessStatusCode();

            Assert.Equal(new MeResponse(KleinGeldApp.AdminEmail, IsAdmin: true), await browser.GetFromJsonAsync<MeResponse>("/api/me"));
        }
        finally
        {
            var disable = await IdentityForms.ReadAsync(admin, "/Account/Manage/Disable2fa");
            using var disabled = await admin.PostAsync("/Account/Manage/Disable2fa", new FormUrlEncodedContent(disable));
            disabled.EnsureSuccessStatusCode();
            await AssertStillLoggedInAsync(admin);
        }

        using var again = await app.LoginAsAdminAsync();
        Assert.Equal(new MeResponse(KleinGeldApp.AdminEmail, IsAdmin: true), await again.GetFromJsonAsync<MeResponse>("/api/me"));
    }

    [Fact]
    public async Task The_passkeys_page_opens_and_offers_the_browser_a_passkey_for_this_site()
    {
        using var admin = await app.LoginAsAdminAsync();

        var page = await admin.GetStringAsync("/Account/Manage/Passkeys");
        var form = await IdentityForms.ReadAsync(admin, "/Account/Manage/Passkeys");
        using var response = await admin.PostAsync("/Account/PasskeyCreationOptions", new FormUrlEncodedContent(form));

        Assert.Contains("No passkeys are registered", page);
        response.EnsureSuccessStatusCode();
        using var options = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(KleinGeldApp.AdminEmail, options.RootElement.GetProperty("user").GetProperty("name").GetString());
        Assert.NotEmpty(options.RootElement.GetProperty("challenge").GetString()!);
    }

    /// <summary>
    /// Lets a second pass, so Identity re-checks the session's security stamp as it does on any real request, and
    /// checks the session survived: these pages change the stamp, and a Member shouldn't be logged out for using them.
    /// </summary>
    private async Task AssertStillLoggedInAsync(HttpClient member)
    {
        _now = _now.AddSeconds(1);
        await app.SetClockAsync(_now);
        using var response = await member.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The six-digit code an authenticator app shows now (RFC 6238 with SHA-1 and 30-second steps).</summary>
    private static string CurrentCode(string base32Key)
    {
        Span<byte> step = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(step, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var hash = HMACSHA1.HashData(DecodeBase32(base32Key), step);
        var offset = hash[^1] & 0x0F;
        var code = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7FFFFFFF;
        return (code % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string key)
    {
        var bits = string.Concat(key.ToUpperInvariant().TrimEnd('=').Select(character =>
            Convert.ToString("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(character), 2).PadLeft(5, '0')));
        return Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray();
    }

    [GeneratedRegex("<kbd>(?<key>[^<]+)</kbd>")]
    private static partial Regex SharedKey();
}
