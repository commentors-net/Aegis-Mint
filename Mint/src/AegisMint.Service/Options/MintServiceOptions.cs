namespace AegisMint.Service.Options;

public class MintServiceOptions
{
    public string PipeName { get; set; } = "AegisMint_Service";
    public bool AllowDevBypassUnlock { get; set; } = false;
    public int DefaultUnlockMinutes { get; set; } = 15;
    public string LogFilePath { get; set; } = "logs/service.log";
}
