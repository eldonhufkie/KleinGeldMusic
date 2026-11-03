using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using KleinGeld.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Run mode (dotnet run, the API tests) uses emulators and MailPit. `aspire publish` writes the compose file
// for one copy of the app on TrueNAS, chosen by Deploy:Copy (Production or Test), see Deployment.cs.
var deployment = builder.ExecutionContext.IsPublishMode ? Deployment.FromConfiguration(builder.Configuration) : null;
if (deployment is not null)
{
    builder.AddDockerComposeEnvironment("compose").WithDashboard(false);
}

var postgres = builder.AddPostgres("postgres");

// Keeps dev data between runs. The API tests turn it off so every test run starts from an empty database.
var useDataVolume = !bool.TryParse(builder.Configuration["Postgres:DataVolume"], out var configured) || configured;
if (deployment is not null)
{
    postgres.PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = Deployment.RestartAlways;
        service.Volumes.Add(deployment.BindMount("postgres", "/var/lib/postgresql"));
        service.Healthcheck = new Healthcheck
        {
            Test = ["CMD-SHELL", "pg_isready -U postgres -h localhost"],
            Interval = "5s",
            Timeout = "5s",
            Retries = 10,
            StartPeriod = "10s",
        };
    });
}
else if (useDataVolume)
{
    postgres.WithDataVolume();
}

var database = postgres.AddDatabase("kleingeld");

// The one Admin, created by the migrator on its first run.
var adminEmail = builder.AddParameter("admin-email");
var adminPassword = builder.AddParameter("admin-password", secret: true);

var migrator = builder.AddProject<Projects.KleinGeld_Migrator>("migrator")
    .WithReference(database)
    .WithEnvironment("Admin__Email", adminEmail)
    .WithEnvironment("Admin__Password", adminPassword)
    .WaitFor(database)
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Image = deployment!.Image("migrator");
        // A one-shot job: it migrates, seeds the Admin and exits; the web host waits for it to complete.
        service.Restart = "no";
        // Aspire's WaitFor only waits for the container to start; PostgreSQL must accept connections first.
        service.DependsOn["postgres"] = new ServiceDependency { Condition = "service_healthy" };
    });

var web = builder.AddProject<Projects.KleinGeld_Web>("web")
    .WithReference(database)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

if (deployment is { IsProduction: true })
{
    // Production: the real Azure audio account and real SMTP (Gmail or any other), both from secrets.env.
    // Switching mail provider is a change to the smtp connection string and email-from only.
    web.WithReference(builder.AddConnectionString("blobs"))
        .WithReference(builder.AddConnectionString("smtp"))
        .WithEnvironment("Email__From", builder.AddParameter("email-from"));
}
else
{
    // Development, the API tests and the Test copy: blobs go to Azurite and every email to MailPit.
    var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
        .WithHttpEndpoint(targetPort: 8025, name: "http")
        .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp")
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Restart = Deployment.RestartAlways;
            // The Test copy's inbox, reachable on the tailnet.
            service.Ports.Add("8025:8025");
        });
    var mailpitSmtp = mailpit.GetEndpoint("smtp");

    web.WithEnvironment(
            "ConnectionStrings__smtp",
            ReferenceExpression.Create($"Endpoint=smtp://{mailpitSmtp.Property(EndpointProperty.HostAndPort)}"))
        .WaitFor(mailpit);

    if (deployment is null)
    {
        var blobs = builder.AddAzureStorage("storage")
            .RunAsEmulator()
            .AddBlobs("blobs");
        web.WithReference(blobs).WaitFor(blobs);
    }
    else
    {
        // The Azure Storage emulator integration publishes Bicep for real Azure, so Test runs Azurite as a plain container.
        var azurite = builder.AddContainer("azurite", "mcr.microsoft.com/azure-storage/azurite")
            .WithArgs("azurite-blob", "--blobHost", "0.0.0.0", "--location", "/data", "--skipApiVersionCheck")
            .WithEndpoint(targetPort: 10000, name: "blob", scheme: "http")
            .PublishAsDockerComposeService((_, service) =>
            {
                service.Restart = Deployment.RestartAlways;
                service.Volumes.Add(deployment.BindMount("azurite", "/data"));
            });
        var azuriteBlob = azurite.GetEndpoint("blob");
        web.WithEnvironment(
                "ConnectionStrings__blobs",
                ReferenceExpression.Create($"{Deployment.AzuriteAccount}BlobEndpoint={azuriteBlob.Property(EndpointProperty.Url)}/devstoreaccount1;"))
            .WaitFor(azurite);
    }
}

web.PublishAsDockerComposeService((_, service) =>
{
    service.Image = deployment!.Image("web");
    service.Restart = Deployment.RestartAlways;
    // A fixed port per copy, so Production and Test can run side by side behind Tailscale.
    service.Environment["HTTP_PORTS"] = "8080";
    service.Ports.Clear();
    service.Ports.Add($"{deployment.WebHostPort}:8080");
    // Members stay logged in across deploys because the key ring lives on a dataset, not in the container.
    service.Environment["DataProtection__KeysPath"] = "/keys";
    service.Volumes.Add(deployment.BindMount("keys", "/keys"));
});

builder.Build().Run();
