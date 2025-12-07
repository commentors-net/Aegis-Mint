using System.Windows;
using AegisMint.Core.Services;

namespace AegisMint.Mint;

public partial class NetworkSelectionWindow : Window
{
    public string SelectedNetwork { get; private set; } = "sepolia";
    public string RpcUrl { get; private set; } = "https://ethereum-sepolia-rpc.publicnode.com";

    public NetworkSelectionWindow()
    {
        InitializeComponent();
    }

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        if (LocalhostRadio.IsChecked == true)
        {
            SelectedNetwork = "localhost";
            RpcUrl = "http://127.0.0.1:8545";
        }
        else if (SepoliaRadio.IsChecked == true)
        {
            SelectedNetwork = "sepolia";
            RpcUrl = "https://ethereum-sepolia-rpc.publicnode.com";
        }
        else if (MainnetRadio.IsChecked == true)
        {
            // Show additional confirmation for mainnet
            var result = System.Windows.MessageBox.Show(
                "You are about to connect to Ethereum Mainnet.\n\n" +
                "This network uses REAL ETH and transactions will cost real money.\n\n" +
                "Are you sure you want to continue?",
                "Mainnet Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return;
            }

            SelectedNetwork = "mainnet";
            RpcUrl = "https://eth.llamarpc.com";
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
