using System.ComponentModel.DataAnnotations;

namespace FinalTestLogIngest.Options;

public class ProcessingOptions
{
    [Required]
    [Range(100, 60000)]
    public int StableWaitMs { get; set; } = 1500;

    [Required]
    [Range(1, 32)]
    public int MaxConcurrency { get; set; } = 2;
}

