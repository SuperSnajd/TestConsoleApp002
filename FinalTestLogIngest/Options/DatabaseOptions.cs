using System.ComponentModel.DataAnnotations;

namespace FinalTestLogIngest.Options;

public class DatabaseOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string Schema { get; set; } = "gen4finaltest_testlogs";

    [Required]
    public string AutoCreate { get; set; } = "All";
}

