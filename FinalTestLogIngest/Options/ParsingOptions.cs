using System.ComponentModel.DataAnnotations;

namespace FinalTestLogIngest.Options;

public class ParsingOptions
{
    [Required]
    public string Culture { get; set; } = "sv-SE";

    [Required]
    public string Timezone { get; set; } = "Local";

    [Required]
    public string IdentityFormat { get; set; } = "{DeviceSerial}-{Date:yyyyMMdd}-{Time:HHmmss}";
}

