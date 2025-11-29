namespace AegisMint.Service.Options;

public class MintServiceOptions
{
    public int Port { get; set; } = 5050;
    public bool UseHttps { get; set; } = true;
    public bool AllowDevBypassUnlock { get; set; }
    public int DefaultUnlockMinutes { get; set; } = 15;
    public string LogFilePath { get; set; } = @"logs\service.log";
}
