namespace Harmony.ProjectGeneration
{
    public class ProjectGenerationService
    {
        private readonly ProjectFileGenerationService _projectFileGenerationService;
        private readonly PackageFileParser _packageFileParser;

        public ProjectGenerationService(ProjectFileGenerationService projectFileGenerationService, PackageFileParser packageFileParser)
        {
            _projectFileGenerationService = projectFileGenerationService;
            _packageFileParser = packageFileParser;
        }

        public async Task GenerateProject(string projectName, string projectPackageLockFilePath)
        {
            Console.WriteLine($"Generating project {projectName}");

            string fullPath = Path.GetFullPath(projectPackageLockFilePath);

            // Parse the package lock file
            PackageDependencyFile packageDependencyFile = await _packageFileParser.Parse(fullPath);

            // Get the dependencies that need to be compiled
            IEnumerable<PackageDependency> dependencies = packageDependencyFile.Dependencies
                .Where(package => package.Source != "builtin");

            // Generate the project file for every dependency

        }
    }
}
