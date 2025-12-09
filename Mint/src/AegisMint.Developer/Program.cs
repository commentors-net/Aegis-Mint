using AegisMint.Core.Services;

Console.WriteLine("AegisMint Developer Utility");
Console.WriteLine("Import an existing treasury mnemonic and store it securely for the Mint app.");
Console.WriteLine();

var vault = new VaultManager();

if (vault.HasTreasury())
{
    Console.Write("A treasury already exists. Overwrite with the new mnemonic? (y/N): ");
    var overwrite = Console.ReadLine();
    if (!string.Equals(overwrite, "y", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Aborted. Existing treasury left untouched.");
        return;
    }
}

Console.Write("Paste the mnemonic (words separated by spaces): ");
var mnemonicInput = Console.ReadLine();

if (string.IsNullOrWhiteSpace(mnemonicInput))
{
    Console.WriteLine("No mnemonic provided. Exiting.");
    return;
}

try
{
    var address = vault.ImportTreasuryMnemonic(mnemonicInput);
    var primaryPk = vault.GetTreasuryPrivateKey();
    var secondaryAddress = vault.GetSecondaryAddress();
    var secondaryPk = vault.GetSecondaryPrivateKey();

    Console.WriteLine();
    Console.WriteLine("Mnemonic stored for current Windows user.");
    Console.WriteLine($"Treasury address (index 0):  {address}");
    if (!string.IsNullOrWhiteSpace(primaryPk))
    {
        Console.WriteLine($"Treasury private key (index 0): {primaryPk}");
    }
    if (!string.IsNullOrWhiteSpace(secondaryAddress))
    {
        Console.WriteLine($"Secondary address (index 1):   {secondaryAddress}");
    }
    if (!string.IsNullOrWhiteSpace(secondaryPk))
    {
        Console.WriteLine($"Secondary private key (index 1): {secondaryPk}");
    }
    Console.WriteLine();
    Console.WriteLine("Keep these secrets safe. Anyone with the mnemonic or private key controls the treasury.");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to import mnemonic: {ex.Message}");
}
