var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");

// Keeps dev data between runs. The API tests turn it off so every test run starts from an empty database.
var useDataVolume = !bool.TryParse(builder.Configuration["Postgres:DataVolume"], out var configured) || configured;
if (useDataVolume)
{
    postgres.WithDataVolume();
}

var database = postgres.AddDatabase("kleingeld");

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs");

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");
var mailpitSmtp = mailpit.GetEndpoint("smtp");

// The one Admin, created by the migrator on its first run.
var adminEmail = builder.AddParameter("admin-email");
var adminPassword = builder.AddParameter("admin-password", secret: true);

var migrator = builder.AddProject<Projects.KleinGeld_Migrator>("migrator")
    .WithReference(database)
    .WithEnvironment("Admin__Email", adminEmail)
    .WithEnvironment("Admin__Password", adminPassword)
    .WaitFor(database);

builder.AddProject<Projects.KleinGeld_Web>("web")
    .WithReference(database)
    .WithReference(blobs)
    // Every email goes to MailPit in development and tests.
    .WithEnvironment(
        "ConnectionStrings__smtp",
        ReferenceExpression.Create($"Endpoint=smtp://{mailpitSmtp.Property(EndpointProperty.HostAndPort)}"))
    .WaitFor(blobs)
    .WaitFor(mailpit)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
