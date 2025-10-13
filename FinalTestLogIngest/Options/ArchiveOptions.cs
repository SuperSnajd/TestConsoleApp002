using System.ComponentModel.DataAnnotations;

namespace FinalTestLogIngest.Options;

public class ArchiveOptions
{
    [Required]
    public string SuccessPath { get; set; } = string.Empty;

    [Required]
    public string ErrorPath { get; set; } = string.Empty;

    [Required]
    public string OnSuccess { get; set; } = "Move";

    [Required]
    public string OnError { get; set; } = "Move";

    [Required]
    public string ArchiveNamePatternOnConflict { get; set; } = "{name}-{yyyyMMdd_HHmmss}{ext}";

    public bool PreserveSubfolders { get; set; } = false;
}

