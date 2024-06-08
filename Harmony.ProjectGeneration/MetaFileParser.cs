using System.Text.Json;
using System.Text.RegularExpressions;
using Harmony.Domain;

namespace Harmony.ProjectGeneration;

public class MetaFileParser
{
    public async Task<MetaFile> Parse(string path)
    {
        path = Path.GetFullPath(path);
        using FileStream? stream = File.OpenRead(path);
        using TextReader reader = new StreamReader(stream);
        string? text = await reader.ReadToEndAsync();

        Regex regex = new Regex(@"guid: \b(\w+)\b");
        string? guid = regex.Match(text).Groups[1].Value;

        if (guid is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return new MetaFile()
        {
            Id = Guid.Parse(guid)
        };
    }
}
