using System.ComponentModel.DataAnnotations;

namespace FinalTestLogIngest.Options;

public class WatcherOptions
{
    [Required]
    public string Path { get; set; } = string.Empty;

    [Required]
    public string Filter { get; set; } = "*.log";

    public bool IncludeSubdirectories { get; set; } = false;

    public string[] NotifyFilters { get; set; } = ["FileName", "LastWrite"];

    public bool InitialScan { get; set; } = true;
}

