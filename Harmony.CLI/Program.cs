using System.Text.Json;
using Harmony.ProjectGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Setup configuration
IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", true, true)
    .Build();

// Setup DI
IServiceCollection services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(console => { console.IncludeScopes = true; }));

services.AddSingleton<ProjectFileGenerationService>(_ =>
{
    string projectRoot = @"C:\Users\jdekk\source\repos\aplib.net-demo\aplib.net-demo";
    string editorPath = @"C:\Program Files\Unity\Hub\Editor\2022.3.19f1";

    return new ProjectFileGenerationService(projectRoot, editorPath);
});
services.AddSingleton<PackageFileParser>();
services.AddSingleton<ProjectGenerationService>();
services.AddSingleton<AssemblyDefinitionParser>();
services.AddSingleton<MetaFileParser>();

ServiceProvider serviceProvider = services.BuildServiceProvider();
using IServiceScope scope = serviceProvider.CreateScope();
ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

// Generate the project
ProjectGenerationService projectGenerationService = serviceProvider.GetRequiredService<ProjectGenerationService>();
await projectGenerationService.GenerateProject("MyProject");
