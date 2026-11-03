namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class DataProtectionTests(KleinGeldApp app)
{
    // Members stay logged in across deploys only if the keys that sign their cookies outlive the container.
    [Fact]
    public async Task Logging_in_keeps_the_signing_keys_in_the_configured_folder()
    {
        using var client = await app.LoginAsAdminAsync();

        Assert.NotEmpty(Directory.GetFiles(app.DataProtectionKeysPath, "key-*.xml"));
    }
}
