using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using KleinGeld.Data;

namespace KleinGeld.Web;

/// <summary>
/// Identity's default token provider (set password, reset password, change email), built like
/// <see cref="DataProtectorTokenProvider{TUser}"/> but reading the time from <see cref="TimeProvider"/>, which
/// Identity's own provider ignores. Tokens last <see cref="DataProtectionTokenProviderOptions.TokenLifespan"/> and
/// die with the Member's security stamp, so a link works once.
/// </summary>
internal sealed class MemberTokenProvider(
    IDataProtectionProvider dataProtection,
    IOptions<DataProtectionTokenProviderOptions> options,
    TimeProvider clock) : IUserTwoFactorTokenProvider<Member>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector(nameof(MemberTokenProvider));
    private readonly TimeSpan _lifespan = options.Value.TokenLifespan;

    public async Task<string> GenerateAsync(string purpose, UserManager<Member> manager, Member user)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(clock.GetUtcNow().UtcTicks);
            writer.Write(await manager.GetUserIdAsync(user));
            writer.Write(purpose);
            writer.Write(await manager.GetSecurityStampAsync(user) ?? "");
        }
        return Convert.ToBase64String(_protector.Protect(stream.ToArray()));
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<Member> manager, Member user)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(_protector.Unprotect(Convert.FromBase64String(token))));
            var createdAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
            return createdAt + _lifespan >= clock.GetUtcNow()
                && reader.ReadString() == await manager.GetUserIdAsync(user)
                && reader.ReadString() == purpose
                && reader.ReadString() == (await manager.GetSecurityStampAsync(user) ?? "")
                && reader.PeekChar() == -1;
        }
        // Anything that isn't a token this provider made: tampered, truncated, or not base64 at all.
        catch (Exception exception) when (exception is FormatException or EndOfStreamException or System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<Member> manager, Member user) => Task.FromResult(false);
}
