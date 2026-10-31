var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .AddDatabase("kleingeld");

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs");

builder.AddContainer("mailpit", "axllent/mailpit")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");

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
    .WaitFor(blobs)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
