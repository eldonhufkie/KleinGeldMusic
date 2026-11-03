using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using KleinGeld.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Three configurations:
// - run mode (dotnet run, the API tests): Azurite emulator and MailPit, managed by Aspire;
// - publish Production: real Azure Blob and SMTP, from secrets.env;
// - publish Test: Azurite and MailPit as plain containers in the compose file.
// `aspire publish` picks the copy with Deploy:Copy and the TrueNAS pool with Deploy:Pool, see Deployment.cs.
var deployment = builder.ExecutionContext.IsPublishMode ? Deployment.FromConfiguration(builder.Configuration) : null;

// Pinned: the bind mount below relies on PostgreSQL 18's layout.
var postgres = builder.AddPostgres("postgres").WithImageTag("18.3");
var database = postgres.AddDatabase("kleingeld");

var adminEmail = builder.AddParameter("admin-email");
var adminPassword = builder.AddParameter("admin-password", secret: true);

// Applies migrations and seeds the one Admin on its first run.
var migrator = builder.AddProject<Projects.KleinGeld_Migrator>("migrator")
    .WithReference(database)
    .WithEnvironment("Admin__Email", adminEmail)
    .WithEnvironment("Admin__Password", adminPassword)
    .WaitFor(database);

var web = builder.AddProject<Projects.KleinGeld_Web>("web")
    .WithReference(database)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

if (deployment is null)
{
    // Keeps dev data between runs. The API tests turn it off so every test run starts from an empty database.
    if (!bool.TryParse(builder.Configuration["Postgres:DataVolume"], out var useDataVolume) || useDataVolume)
    {
        postgres.WithDataVolume();
    }

    var blobs = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("blobs");
    web.WithReference(blobs).WaitFor(blobs);
    SendEmailToMailPit(AddMailPit());
}
else
{
    builder.AddDockerComposeEnvironment("compose").WithDashboard(false);

    postgres.PublishAsDockerComposeService((_, service) =>
    {
        // PostgreSQL 18 images keep their data (in a versioned subfolder) under /var/lib/postgresql.
        deployment.LongRunning(service, "postgres", "/var/lib/postgresql");
        service.Healthcheck = new Healthcheck
        {
            Test = ["CMD-SHELL", "pg_isready -U postgres -h localhost"],
            Interval = "5s",
            Timeout = "5s",
            Retries = 10,
            StartPeriod = "10s",
        };
    });

    migrator.PublishAsDockerComposeService((_, service) =>
    {
        service.Image = deployment.Image("migrator");
        // A one-shot job: it migrates, seeds the Admin and exits; the web host waits for it to complete.
        service.Restart = "no";
        // Aspire's WaitFor only waits for the container to start; PostgreSQL must accept connections first.
        service.DependsOn["postgres"] = new ServiceDependency { Condition = "service_healthy" };
    });

    web.PublishAsDockerComposeService((_, service) =>
    {
        service.Image = deployment.Image("web");
        // Members stay logged in across deploys because the key ring lives on a dataset, not in the container.
        deployment.LongRunning(service, "keys", "/keys");
        service.Environment["DataProtection__KeysPath"] = "/keys";
        service.Environment["HTTP_PORTS"] = "8080";
        Deployment.PublishLocally(service, deployment.WebHostPort, 8080);
    });

    if (deployment.Copy == Copy.Production)
    {
        // The real Azure audio account and real SMTP (Gmail or any other), both from secrets.env.
        // Switching mail provider is a change to the smtp connection string and email-from only.
        web.WithReference(builder.AddConnectionString("blobs"))
            .WithReference(builder.AddConnectionString("smtp"))
            .WithEnvironment("Email__From", builder.AddParameter("email-from"));
    }
    else
    {
        // The Azure Storage emulator integration publishes Bicep for real Azure, so Test runs Azurite as a plain container.
        var azurite = builder.AddContainer("azurite", "mcr.microsoft.com/azure-storage/azurite")
            .WithArgs("azurite-blob", "--blobHost", "0.0.0.0", "--location", "/data", "--skipApiVersionCheck")
            .WithEndpoint(targetPort: 10000, name: "blob", scheme: "http")
            .PublishAsDockerComposeService((_, service) =>
            {
                deployment.LongRunning(service, "azurite", "/data");
                // Browsers play and upload audio straight from Azurite, through tailscale serve.
                Deployment.PublishLocally(service, 10000, 10000);
            });
        var azuriteBlob = azurite.GetEndpoint("blob");
        web.WithEnvironment(
                "ConnectionStrings__blobs",
                ReferenceExpression.Create($"{Deployment.AzuriteAccount}BlobEndpoint={azuriteBlob.Property(EndpointProperty.Url)}/devstoreaccount1;"))
            // Where browsers reach Azurite (the tailscale serve address), and the site's origin it must accept.
            .WithEnvironment("Blobs__PublicEndpoint", builder.AddParameter("blobs-public-endpoint"))
            .WithEnvironment("Blobs__CorsOrigin", builder.AddParameter("site-origin"))
            .WaitFor(azurite);

        var mailpit = AddMailPit().PublishAsDockerComposeService((_, service) =>
        {
            deployment.LongRunning(service);
            // The Test copy's inbox, through tailscale serve.
            Deployment.PublishLocally(service, 8025, 8025);
        });
        SendEmailToMailPit(mailpit);
    }
}

builder.Build().Run();

IResourceBuilder<ContainerResource> AddMailPit() =>
    builder.AddContainer("mailpit", "axllent/mailpit")
        .WithHttpEndpoint(targetPort: 8025, name: "http")
        .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");

void SendEmailToMailPit(IResourceBuilder<ContainerResource> mailpit)
{
    var smtp = mailpit.GetEndpoint("smtp");
    web.WithEnvironment(
            "ConnectionStrings__smtp",
            ReferenceExpression.Create($"Endpoint=smtp://{smtp.Property(EndpointProperty.HostAndPort)}"))
        .WaitFor(mailpit);
}
