using Zakira.Imprint.Sample.WithCode;

// This is a sample consumer project demonstrating Imprint package consumption.
// After running 'dotnet restore', check .github/skills/ for installed AI skills.

Console.WriteLine("Imprint Consumer Project");
Console.WriteLine("========================");
Console.WriteLine();
Console.WriteLine("This project demonstrates how to consume AI skills via NuGet packages.");
Console.WriteLine();
Console.WriteLine("Check the .github/skills/ folder for installed skills!");
Console.WriteLine();

// --- Demonstrate Zakira.Imprint.Sample.WithCode library usage ---
Console.WriteLine("--- String Utility Demo (Zakira.Imprint.Sample.WithCode) ---");
Console.WriteLine();

Console.WriteLine($"Slugify:        {"Hello World! This is C#".Slugify()}");
Console.WriteLine($"Truncate:       {"Hello World".Truncate(8)}");
Console.WriteLine($"Mask:           {"1234567890".Mask(2, 2)}");
Console.WriteLine($"TitleCase:      {"hello world example".ToTitleCase()}");
Console.WriteLine($"RemoveDiacrit:  {"café résumé".RemoveDiacritics()}");
Console.WriteLine($"CamelCase:      {"hello world example".ToCamelCase()}");
Console.WriteLine($"SnakeCase:      {"camelCaseExample".ToSnakeCase()}");
Console.WriteLine($"Reverse:        {"Hello".Reverse()}");
Console.WriteLine($"WordCount:      {"Hello world, this is a test".WordCount()}");
Console.WriteLine();
Console.WriteLine($"MaskEmail:      {StringHelper.MaskEmail("john.doe@example.com")}");
Console.WriteLine($"MaskCard:       {StringHelper.MaskCreditCard("4111-1111-1111-1111")}");
Console.WriteLine($"ShortHash:      {StringHelper.ShortHash("hello world")}");
Console.WriteLine($"IsValidEmail:   {StringHelper.IsValidEmail("user@example.com")}");
Console.WriteLine($"GetInitials:    {StringHelper.GetInitials("John Michael Doe")}");
Console.WriteLine();

// Check if skills are installed
var skillsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".github", "skills");
var projectSkillsPath = Path.Combine(Directory.GetCurrentDirectory(), ".github", "skills");

if (Directory.Exists(projectSkillsPath))
{
    Console.WriteLine($"Skills folder found at: {projectSkillsPath}");
    var skillFiles = Directory.GetFiles(projectSkillsPath, "*.md", SearchOption.AllDirectories);
    Console.WriteLine($"Found {skillFiles.Length} skill file(s):");
    foreach (var file in skillFiles)
    {
        Console.WriteLine($"  - {Path.GetRelativePath(projectSkillsPath, file)}");
    }
}
else
{
    Console.WriteLine("Skills folder not found. Run 'dotnet restore' to install skills.");
}
