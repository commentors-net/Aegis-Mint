# Aegis Mint – User Guide

This guide walks you through installing and using Aegis Mint to create and manage your token across Ethereum networks.

## Install
- Download the latest installer (v1.0.6): https://github.com/commentors-net/Releases/releases/download/AegisMint-1.0.6/AegisMint-Mint-Setup-1.0.6.exe
- Run the installer and follow the prompts.
- Launch “Aegis Mint” from the Start Menu (no admin needed; the app stores its data under your user profile).

## Choose a Network
- **Localhost 7545**: For local testing with a tool like Ganache (no real funds).
- **Ethereum Testnet (Sepolia)**: Test network; needs free TESTETH from a faucet (e.g., https://cloud.google.com/application/web3/faucet/ethereum/sepolia).
- **Ethereum Mainnet**: Real ETH required; incurs real costs.
- The app remembers the last network you used and reloads its data on startup.

## Screen Layout
1) **Token**
   - Token Name
   - Number of Tokens
   - Number of Decimal Places
2) **Governance**
   - Number of Shares
   - Number Needed to Generate Key (threshold)
3) **Token Details**
   - Contract Address (read‑only; filled after deployment)
   - Treasury / Owner Address (read‑only; must exist before deploying)
   - Treasury ETH (current ETH balance of the treasury address)
   - Treasury Tokens (token balance of the treasury address; starts equal to total supply right after deployment)

## Buttons
- **Reset**: Clears all editable Token and Governance fields. Disabled if a contract already exists on the selected network.
- **Generate Treasury**: Creates the treasury (owner) address. This address is shared across all networks.
- **Mint Token**: Validates inputs, creates recovery shares, and deploys the token. Progress is shown, and short status toasts appear for feedback. After success, Token Details populate and the form locks for that network.

## Typical Workflow
1. Select a network. The app remembers and reloads your last choice.
2. **Generate Treasury** if the treasury address is empty. This address is reused across all networks—fund it with ETH on the selected network before minting.
3. Enter **Token** details:
   - Token Name
   - Number of Tokens
   - Number of Decimal Places
4. Enter **Governance** details:
   - Number of Shares (total recovery shares to create)
   - Number Needed to Generate Key (threshold required to recover the genesis key)
5. Confirm **Treasury ETH** shows enough balance for gas (enter up to 4 decimals). **Treasury Tokens** starts equal to the total supply right after deployment.
6. Click **Mint Token**. Wait for completion; Token Details fill in and the form locks for that network. A notice appears if a contract already exists.
7. When asked, pick a folder to save recovery shares. The app writes one JSON file per share (e.g., 5 shares = 5 files) so you can hand them to different people.

## Notes
- Each network keeps its own deployed contract info; switching networks clears the fields, then refills if a deployment exists there.
- Treasury ETH is shown to 4 decimals for input validation.
- If a contract already exists on the selected network, the app locks the form, prefills details, and shows a notice that it’s already deployed (no redeploy on that network). Reset is disabled in this state.
- Status toasts stack and auto-hide after a few seconds; a progress bar appears during minting.
