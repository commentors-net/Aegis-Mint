window.addEventListener('DOMContentLoaded', function() {
  const form = document.getElementById("aegis-mint-form");
  const tokenNameInput = document.getElementById("token-name");
  const tokenSupplyInput = document.getElementById("token-supply");
  const tokenDecimalsInput = document.getElementById("token-decimals");
  const sharesInput = document.getElementById("gov-shares");
  const thresholdInput = document.getElementById("gov-threshold");
  const thresholdError = document.getElementById("threshold-error");

  const contractAddressInput = document.getElementById("contract-address");
  const treasuryAddressInput = document.getElementById("treasury-address");
  const treasuryEthInput = document.getElementById("treasury-eth");
  const treasuryTokensInput = document.getElementById("treasury-tokens");
  const treasuryStatus = document.getElementById("treasury-status");
  const headerTreasuryAddress = document.getElementById("header-treasury-address");
  const headerTreasurySummary = document.getElementById("header-treasury-summary");

  const resetBtn = document.getElementById("reset-form");
  const genTreasuryBtn = document.getElementById("gen-treasury");
  const mintBtn = document.getElementById("mint-button");
  const networkSelect = document.getElementById("network-select");
  
  if (!genTreasuryBtn) {
    console.error("Generate Treasury button not found!");
    return;
  }
  if (!mintBtn) {
    console.error("Mint button not found!");
    return;
  }

  let treasuryGenerated = false;
  let selectedNetwork = "sepolia"; // default
  let contractDeploymentLocked = false;

  const sendToHost = (type, payload) => {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
      window.chrome.webview.postMessage({ type, payload });
    }
  };

  const logToHost = (message, level = "info") => {
    sendToHost("log", { message, level });
  };

  window.receiveHostMessage = function (message) {
    const { type, payload } = message || {};
    switch (type) {
      case "host-info":
        logToHost("Connected to host");
        break;
      case "treasury-generated":
        applyTreasury(payload);
        break;
      case "vault-status":
        applyVault(payload);
        break;
      case "mint-received":
        showToast("Mint request received by host");
        break;
      case "contract-deployed":
        lockUiForDeployedContract(payload?.address);
        break;
      case "host-error":
        showToast(payload?.message || "Host error", true);
        break;
      case "validation-result":
        showToast(payload?.message || "Validation completed", payload?.ok === false);
        break;
    }
  };

  function parseNumeric(value) {
    if (value === null || value === undefined) return NaN;
    const cleaned = value.toString().replace(/,/g, "").trim();
    if (!cleaned) return NaN;
    return parseFloat(cleaned);
  }

  function formatWithCommas(value) {
    const str = value.toString();
    const parts = str.split(".");
    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ",");
    return parts.join(".");
  }

  function updateHeaderTreasury() {
    const addr = treasuryAddressInput.value.trim();
    const ethRaw = treasuryEthInput.value || "0";
    const eth = parseFloat(ethRaw);
    const tokens = treasuryTokensInput.value || "0";

    headerTreasuryAddress.textContent = addr || "Not set";

    const safeEth = Number.isNaN(eth) ? 0 : eth;
    headerTreasurySummary.textContent = `ETH: ${safeEth} Tokens: ${tokens}`;
  }

  function showToast(text, isError = false) {
    const toast = document.createElement("div");
    toast.textContent = text;
    toast.style.position = "fixed";
    toast.style.bottom = "18px";
    toast.style.right = "18px";
    toast.style.padding = "10px 14px";
    toast.style.borderRadius = "12px";
    toast.style.background = isError ? "rgba(249, 115, 115, 0.9)" : "rgba(56, 189, 248, 0.9)";
    toast.style.color = "#020617";
    toast.style.boxShadow = "0 10px 24px rgba(0,0,0,0.35)";
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 2200);
  }

  function showPermanentToast(text, isError = false) {
    const existing = document.getElementById("permanent-toast");
    if (existing) {
      existing.textContent = text;
      existing.style.background = isError ? "rgba(249, 115, 115, 0.12)" : "rgba(56, 189, 248, 0.14)";
      existing.style.borderColor = isError ? "rgba(249, 115, 115, 0.6)" : "rgba(56, 189, 248, 0.6)";
      return;
    }

    const toast = document.createElement("div");
    toast.id = "permanent-toast";
    toast.textContent = text;
    toast.style.position = "fixed";
    toast.style.bottom = "18px";
    toast.style.right = "18px";
    toast.style.padding = "12px 16px";
    toast.style.borderRadius = "14px";
    toast.style.background = isError ? "rgba(249, 115, 115, 0.12)" : "rgba(56, 189, 248, 0.14)";
    toast.style.color = "#e5e7eb";
    toast.style.border = `1px solid ${isError ? "rgba(249, 115, 115, 0.6)" : "rgba(56, 189, 248, 0.6)"}`;
    toast.style.boxShadow = "0 14px 30px rgba(0,0,0,0.35)";
    toast.style.backdropFilter = "blur(8px)";
    toast.style.zIndex = "999";
    document.body.appendChild(toast);
  }

  function lockUiForDeployedContract(address) {
    if (contractDeploymentLocked) return;
    contractDeploymentLocked = true;

    const controls = document.querySelectorAll("input, select, button, textarea");
    controls.forEach((el) => {
      el.disabled = true;
      el.classList.add("locked-control");
    });

    if (contractAddressInput && address) {
      contractAddressInput.value = address;
    }

    treasuryStatus.textContent = "Contract already deployed. Deployment disabled.";
    showPermanentToast("Contract is already deployed");
    updateMintState();
    updateGenerateState();
  }

  function validateThreshold(showMessage) {
    const shares = parseInt(sharesInput.value, 10);
    const threshold = parseInt(thresholdInput.value, 10);
    const invalid = Number.isFinite(shares) && Number.isFinite(threshold) && threshold > shares;
    thresholdError.style.display = invalid && showMessage ? "block" : "none";
    return !invalid;
  }

  function updateGenerateState() {
    // Only gate when a treasury already exists; otherwise leave it clickable.
    genTreasuryBtn.disabled = contractDeploymentLocked || treasuryGenerated;
  }

  function updateMintState() {
    const eth = parseFloat(treasuryEthInput.value || "0");
    const hasEth = !Number.isNaN(eth) && eth > 0;
    mintBtn.disabled = contractDeploymentLocked || !(treasuryGenerated && hasEth);
  }

  function collectForm() {
    const supplyRaw = parseNumeric(tokenSupplyInput.value || "0");
    const decimals = parseInt(tokenDecimalsInput.value || "0", 10);
    return {
      tokenName: tokenNameInput.value,
      tokenSupply: Number.isNaN(supplyRaw) ? tokenSupplyInput.value : supplyRaw,
      tokenDecimals: decimals,
      govShares: sharesInput.value,
      govThreshold: thresholdInput.value,
      treasuryEth: treasuryEthInput.value,
      treasuryAddress: treasuryAddressInput.value,
      network: selectedNetwork
    };
  }

  function applyTreasury(payload) {
    const addr = payload?.address || "";
    const status = payload?.status || "Treasury generated";
    if (addr) {
      treasuryAddressInput.value = addr;
      treasuryGenerated = true;
      treasuryStatus.textContent = status;
      treasuryEthInput.disabled = false;
      treasuryEthInput.classList.add("eth-alert");
      headerTreasuryAddress.textContent = addr;
      genTreasuryBtn.disabled = true;
    }
    updateHeaderTreasury();
    updateMintState();
  }

  function applyVault(payload) {
    if (payload?.hasTreasury && payload?.treasuryAddress) {
      treasuryAddressInput.value = payload.treasuryAddress;
      treasuryGenerated = true;
      treasuryStatus.textContent = "Treasury loaded from vault";
      treasuryEthInput.disabled = false;
      headerTreasuryAddress.textContent = payload.treasuryAddress;
      genTreasuryBtn.disabled = true;
    }
    if (payload?.balance !== undefined && payload.balance !== null) {
      const bal = Number(payload.balance);
      if (!Number.isNaN(bal)) {
        treasuryEthInput.value = bal.toFixed(6);
        treasuryEthInput.classList.toggle("eth-alert", bal <= 0);
      }
    }

    if (payload?.contractDeployed) {
      if (payload.contractAddress && contractAddressInput) {
        contractAddressInput.value = payload.contractAddress;
      }
      lockUiForDeployedContract(payload.contractAddress);
    }
    updateHeaderTreasury();
    updateMintState();
  }

  thresholdInput.addEventListener("input", () => { validateThreshold(false); updateGenerateState(); });
  sharesInput.addEventListener("input", () => { validateThreshold(false); updateGenerateState(); });
  tokenNameInput.addEventListener("input", updateGenerateState);
  tokenSupplyInput.addEventListener("input", updateGenerateState);
  tokenDecimalsInput.addEventListener("input", updateGenerateState);

  // format supply on blur
  tokenSupplyInput.addEventListener("blur", () => {
    const n = parseNumeric(tokenSupplyInput.value);
    if (!Number.isNaN(n) && n > 0) {
      tokenSupplyInput.value = formatWithCommas(Math.floor(n));
    }
  });

  treasuryEthInput.addEventListener("input", () => {
    const eth = parseFloat(treasuryEthInput.value || "0");
    const hasEth = !Number.isNaN(eth) && eth > 0;
    if (treasuryGenerated) {
      treasuryEthInput.classList.toggle("eth-alert", !hasEth);
    }
    updateHeaderTreasury();
    updateMintState();
  });

  treasuryAddressInput.addEventListener("input", updateHeaderTreasury);

  resetBtn.addEventListener("click", () => {
    form.reset();
    thresholdError.style.display = "none";
    treasuryTokensInput.value = "0";
    treasuryEthInput.classList.remove("eth-alert");
    treasuryEthInput.disabled = true;
    treasuryGenerated = false;
    treasuryStatus.textContent = "Fill Steps 1 & 2, then generate Treasury.";
    treasuryAddressInput.value = "";
    genTreasuryBtn.disabled = false;
    updateHeaderTreasury();
    updateMintState();
    updateGenerateState();
    sendToHost("reset", {});
  });

  networkSelect.addEventListener("change", () => {
    selectedNetwork = networkSelect.value;
    sendToHost("network-changed", { network: selectedNetwork });
    logToHost(`Network changed to: ${selectedNetwork}`);
  });

  genTreasuryBtn.addEventListener("click", () => {
    sendToHost("generate-treasury", collectForm());
    logToHost("Generate Treasury requested");
  });

  form.addEventListener("submit", (e) => {
    e.preventDefault();
    const validForm = form.reportValidity();
    const validThreshold = validateThreshold(true);
    if (!validForm || !validThreshold) return;

    if (!treasuryGenerated) {
      showToast("Generate the treasury before minting.", true);
      return;
    }

    const eth = parseFloat(treasuryEthInput.value || "0");
    if (Number.isNaN(eth) || eth <= 0) {
      showToast("Treasury must have ETH to mint.", true);
      return;
    }

    sendToHost("mint-submit", collectForm());
    logToHost("Mint submitted from UI");
  });

  // Initialize network selection
  selectedNetwork = networkSelect.value;
  
  sendToHost("bridge-ready", { ready: true });
  logToHost("Bridge initialized");
  updateHeaderTreasury();
  updateMintState();
  updateGenerateState();
});
