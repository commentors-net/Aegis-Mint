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
  const refreshEthBtn = document.getElementById("refresh-treasury-eth");
  const faucetBtn = document.getElementById("sepolia-faucet");
  const lockBanner = document.getElementById("lock-banner");
  const lockBannerText = document.getElementById("lock-banner-text");
  
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
  let pageEnabled = false;
  let ethPollTimer = null;
  const alwaysEnabledControls = new Set([
    networkSelect?.id,
    refreshEthBtn?.id,
    faucetBtn?.id,
    "gen-treasury"
  ].filter(Boolean));

  const sendToHost = (type, payload) => {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
      window.chrome.webview.postMessage({ type, payload });
    }
  };

  const logToHost = (message, level = "info") => {
    sendToHost("log", { message, level });
  };

  function attachCopyButtons() {
    const targets = document.querySelectorAll('input.input, textarea.input, textarea.textarea');
    targets.forEach((el) => {
      if (!el) return;
      let wrapper = el.closest(".input-copy-wrapper");
      if (!wrapper) {
        wrapper = document.createElement("div");
        wrapper.className = "input-copy-wrapper";
        el.parentNode.insertBefore(wrapper, el);
        wrapper.appendChild(el);
      } else if (wrapper.querySelector(".copy-btn")) {
        return;
      }

      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "copy-btn";
      btn.textContent = "⧉";
      btn.addEventListener("click", async () => {
        const value = (el.value || "").toString().trim();
        if (!value) {
          //showToast("Nothing to copy", true, 3000);
          return;
        }
        try {
          await navigator.clipboard.writeText(value);
          //showToast("Copied to clipboard", false, 3000);
        } catch (err) {
          console.error("Copy failed", err);
          //showToast("Copy failed", true, 4000);
        }
      });
      wrapper.appendChild(btn);
    });
  }

  function setPageEnabled(enabled, reason) {
    pageEnabled = enabled;
    const controls = document.querySelectorAll("input, select, button, textarea");
    controls.forEach((el) => {
      if (!el) return;
      if (el.classList.contains("copy-btn")) return;
      if (alwaysEnabledControls.has(el.id)) return;
      const forceDisabled = el.dataset.forceDisabled === "true";
      if (!enabled) {
        el.disabled = true;
        el.classList.add("locked-control");
      } else {
        el.disabled = forceDisabled;
        el.classList.toggle("locked-control", forceDisabled);
      }
    });

    if (lockBanner && lockBannerText) {
      if (enabled) {
        lockBanner.style.display = "none";
      } else {
        lockBanner.style.display = "block";
        lockBannerText.textContent =
          reason || "Generate a treasury key and fund it with ETH to continue.";
      }
    }
  }

  function startEthPolling() {
    if (ethPollTimer || !treasuryAddressInput.value) return;
    sendToHost("refresh-treasury-eth", {
      address: treasuryAddressInput.value,
      network: selectedNetwork
    });
    ethPollTimer = setInterval(() => {
      if (!treasuryAddressInput.value) return;
      sendToHost("refresh-treasury-eth", {
        address: treasuryAddressInput.value,
        network: selectedNetwork
      });
    }, 12000);
  }

  function stopEthPolling() {
    if (ethPollTimer) {
      clearInterval(ethPollTimer);
      ethPollTimer = null;
    }
  }

  function evaluateLockState() {
    const eth = parseFloat(treasuryEthInput.value || "0");
    const hasEth = treasuryGenerated && !Number.isNaN(eth) && eth > 0;

    if (contractDeploymentLocked) {
      stopEthPolling();
      setPageEnabled(false, "Contract already deployed. Minting disabled.");
      updateGenerateState();
      return;
    }

    if (!treasuryGenerated) {
      setPageEnabled(false, "Generate the treasury before continuing.");
      stopEthPolling();
      updateGenerateState();
      return;
    }

    if (!hasEth) {
      setPageEnabled(false, "Treasury has 0 ETH. Fund it, refresh, or use faucet.");
      startEthPolling();
      treasuryEthInput.classList.add("eth-alert");
      treasuryStatus.textContent = "Waiting for ETH on treasury...";
      updateGenerateState();
      return;
    } else {
      stopEthPolling();
      setPageEnabled(true);
      treasuryEthInput.classList.remove("eth-alert");
      treasuryStatus.textContent = "Treasury ready";
    }

    updateGenerateState();
    updateMintState();
    updateResetState();
  }

  function updateFaucetVisibility() {
    if (!faucetBtn) return;
    faucetBtn.style.display = selectedNetwork === "sepolia" ? "inline-flex" : "none";
  }

  function resetForNetworkChange() {
    contractDeploymentLocked = false;
    stopEthPolling();

    tokenNameInput.value = "";
    tokenSupplyInput.value = "";
    tokenDecimalsInput.value = "";
    sharesInput.value = "";
    thresholdInput.value = "";
    contractAddressInput.value = "";
    treasuryEthInput.value = "";
    treasuryTokensInput.value = "";
    treasuryEthInput.classList.remove("eth-alert");
    treasuryStatus.textContent = treasuryGenerated
      ? "Checking treasury on selected network..."
      : "Fill Steps 1 & 2, then generate Treasury.";

    if (!treasuryGenerated) {
      treasuryAddressInput.value = "";
      treasuryGenerated = false;
      genTreasuryBtn.disabled = false;
    }
    clearPermanentToast();

    setPageEnabled(false, "Switching network...");
    updateHeaderTreasury();
    evaluateLockState();
  }

  function ensureProgressStyles() {
    if (document.getElementById("progress-style")) return;
    const style = document.createElement("style");
    style.id = "progress-style";
    style.textContent = `
      @keyframes mint-progress-stripe {
        0% { background-position: 0 0; }
        100% { background-position: 200% 0; }
      }
    `;
    document.head.appendChild(style);
  }

  function showProgress(text = "Processing...") {
    ensureProgressStyles();
    let container = document.getElementById("mint-progress");
    if (!container) {
      container = document.createElement("div");
      container.id = "mint-progress";
      container.style.position = "fixed";
      container.style.bottom = "18px";
      container.style.left = "50%";
      container.style.transform = "translateX(-50%)";
      container.style.minWidth = "260px";
      container.style.maxWidth = "520px";
      container.style.background = "rgba(15, 23, 42, 0.9)";
      container.style.border = "1px solid rgba(56, 189, 248, 0.6)";
      container.style.borderRadius = "14px";
      container.style.boxShadow = "0 12px 30px rgba(0,0,0,0.45)";
      container.style.padding = "12px 14px 16px";
      container.style.zIndex = "1200";

      const label = document.createElement("div");
      label.id = "mint-progress-label";
      label.style.color = "#e5e7eb";
      label.style.fontSize = "13px";
      label.style.fontWeight = "600";
      label.style.marginBottom = "8px";
      container.appendChild(label);

      const bar = document.createElement("div");
      bar.style.width = "100%";
      bar.style.height = "8px";
      bar.style.borderRadius = "999px";
      bar.style.overflow = "hidden";
      bar.style.background = "rgba(56, 189, 248, 0.18)";

      const inner = document.createElement("div");
      inner.id = "mint-progress-bar";
      inner.style.width = "60%";
      inner.style.height = "100%";
      inner.style.borderRadius = "999px";
      inner.style.background = "linear-gradient(90deg, rgba(56,189,248,0.9), rgba(14,165,233,0.9), rgba(56,189,248,0.9))";
      inner.style.backgroundSize = "200% 100%";
      inner.style.animation = "mint-progress-stripe 1.2s linear infinite";

      bar.appendChild(inner);
      container.appendChild(bar);

      document.body.appendChild(container);
    }

    const labelEl = document.getElementById("mint-progress-label");
    if (labelEl) {
      labelEl.textContent = text;
    }
  }

  function hideProgress() {
    const container = document.getElementById("mint-progress");
    if (container) {
      container.remove();
    }
  }

  window.receiveHostMessage = function (message) {
    const { type, payload } = message || {};
    switch (type) {
      case "host-info":
        logToHost("Connected to host");
        if (payload?.network) {
          applyNetworkFromHost(payload.network);
        }
        break;
      case "treasury-generated":
        applyTreasury(payload);
        break;
      case "vault-status":
        applyVault(payload);
        break;
      case "mint-received":
        showToast("Mint request received by host");
        hideProgress();
        break;
      case "treasury-eth-updated":
        if (payload?.eth !== undefined) {
          const bal = Number(payload.eth);
          treasuryEthInput.value = Number.isNaN(bal) ? payload.eth : bal.toFixed(4);
          treasuryEthInput.classList.toggle("eth-alert", Number.isNaN(bal) ? false : bal <= 0);
        }
        if (payload?.tokens !== undefined && payload.tokens !== null && payload.tokens !== "") {
          treasuryTokensInput.value = payload.tokens;
        }
        updateHeaderTreasury();
        evaluateLockState();
        hideProgress();
        break;
      case "contract-deployed":
        applyPrefill(payload?.prefill || payload);
        lockUiForDeployedContract(payload);
        hideProgress();
        break;
      case "host-error":
        showToast(payload?.message || "Host error", true);
        hideProgress();
        break;
      case "validation-result":
        showToast(payload?.message || "Validation completed", payload?.ok === false);
        if (payload?.ok === false) {
          hideProgress();
        }
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

  function showToast(text, isError = false, durationMs = 7000) {
    let container = document.getElementById("toast-container");
    if (!container) {
      container = document.createElement("div");
      container.id = "toast-container";
      container.style.position = "fixed";
      container.style.bottom = "18px";
      container.style.left = "18px";
      container.style.display = "flex";
      container.style.flexDirection = "column";
      container.style.alignItems = "flex-start";
      container.style.gap = "8px";
      container.style.zIndex = "1000";
      document.body.appendChild(container);
    }

    const toast = document.createElement("div");
    toast.textContent = text;
    toast.style.padding = "10px 14px";
    toast.style.borderRadius = "12px";
    toast.style.background = isError ? "rgba(249, 115, 115, 0.9)" : "rgba(56, 189, 248, 0.9)";
    toast.style.color = "#020617";
    toast.style.boxShadow = "0 10px 24px rgba(0,0,0,0.35)";
    toast.style.maxWidth = "320px";
    toast.style.wordBreak = "break-word";
    container.prepend(toast);

    setTimeout(() => {
      toast.remove();
      if (container.childElementCount === 0) {
        container.remove();
      }
    }, durationMs);
  }

  function clearPermanentToast() {
    const existing = document.getElementById("permanent-toast");
    if (existing) {
      existing.remove();
    }
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

  function unlockUi() {
    contractDeploymentLocked = false;
    clearPermanentToast();
    evaluateLockState();
  }

  function applyPrefill(prefill) {
    if (!prefill) return;
    if (prefill.tokenName !== undefined) tokenNameInput.value = prefill.tokenName;
    if (prefill.tokenSupply !== undefined) tokenSupplyInput.value = prefill.tokenSupply;
    if (prefill.tokenDecimals !== undefined) tokenDecimalsInput.value = prefill.tokenDecimals;
    if (prefill.govShares !== undefined) sharesInput.value = prefill.govShares;
    if (prefill.govThreshold !== undefined) thresholdInput.value = prefill.govThreshold;
    if (prefill.contractAddress !== undefined) contractAddressInput.value = prefill.contractAddress;
    const ethToUse = prefill.liveTreasuryEth ?? prefill.treasuryEth;
    if (ethToUse !== undefined) {
      const num = parseFloat(ethToUse);
      treasuryEthInput.value = Number.isFinite(num) ? num.toFixed(4) : ethToUse;
    }
    const tokensToUse = prefill.liveTreasuryTokens ?? prefill.treasuryTokens ?? prefill.tokenSupply;
    if (tokensToUse !== undefined && tokensToUse !== null && tokensToUse !== "") {
      treasuryTokensInput.value = tokensToUse;
    }
    if (prefill.treasuryAddress !== undefined) {
      treasuryAddressInput.value = prefill.treasuryAddress;
      headerTreasuryAddress.textContent = prefill.treasuryAddress;
      treasuryGenerated = true;
      treasuryStatus.textContent = "Treasury loaded from vault";
    }
    updateHeaderTreasury();
    evaluateLockState();
  }

  function applyNetworkFromHost(network) {
    if (!networkSelect) return;
    resetForNetworkChange();
    const option = Array.from(networkSelect.options).find(opt => opt.value === network);
    if (option) {
      networkSelect.value = network;
      selectedNetwork = network;
      logToHost(`Network set by host: ${network}`);
    }
    updateFaucetVisibility();
  }

  function lockUiForDeployedContract(data) {
    const address = data?.contractAddress || data?.address || data;

    setPageEnabled(false, "Contract already deployed. Deployment disabled.");

    if (contractAddressInput && address) {
      contractAddressInput.value = address;
    }

    contractDeploymentLocked = true;
    treasuryStatus.textContent = "Contract already deployed. Deployment disabled.";
    showPermanentToast("Contract is already deployed");
    updateMintState();
    updateGenerateState();
    updateResetState();
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
    const shouldDisable = !pageEnabled || contractDeploymentLocked || !(treasuryGenerated && hasEth);
    
    logToHost(`updateMintState: locked=${contractDeploymentLocked}, treasuryGen=${treasuryGenerated}, eth=${eth}, hasEth=${hasEth}, disable=${shouldDisable}`);
    
    mintBtn.disabled = shouldDisable;
  }

  function updateResetState() {
    resetBtn.disabled = !pageEnabled || contractDeploymentLocked;
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
      headerTreasuryAddress.textContent = addr;
      genTreasuryBtn.disabled = true;
    }
    updateHeaderTreasury();
    evaluateLockState();
  }

  function applyVault(payload) {
    if (payload?.currentNetwork) {
      applyNetworkFromHost(payload.currentNetwork);
    }

    if (payload?.hasTreasury && payload?.treasuryAddress) {
      treasuryAddressInput.value = payload.treasuryAddress;
      treasuryGenerated = true;
      treasuryStatus.textContent = "Treasury loaded from vault";
      headerTreasuryAddress.textContent = payload.treasuryAddress;
      genTreasuryBtn.disabled = true;
    } else {
      treasuryGenerated = false;
      genTreasuryBtn.disabled = false;
      treasuryAddressInput.value = "";
      headerTreasuryAddress.textContent = "Not set";
      treasuryStatus.textContent = "Fill Steps 1 & 2, then generate Treasury.";
    }
    if (payload?.balance !== undefined && payload.balance !== null) {
      const bal = Number(payload.balance);
      if (!Number.isNaN(bal)) {
        treasuryEthInput.value = bal.toFixed(4);
        treasuryEthInput.classList.toggle("eth-alert", bal <= 0);
      }
    }
    if (payload?.liveTreasuryEth !== undefined) {
      const bal = Number(payload.liveTreasuryEth);
      if (!Number.isNaN(bal)) {
        treasuryEthInput.value = bal.toFixed(4);
        treasuryEthInput.classList.toggle("eth-alert", bal <= 0);
      }
    }
    if (payload?.liveTreasuryTokens !== undefined && payload.liveTreasuryTokens !== null && payload.liveTreasuryTokens !== "") {
      treasuryTokensInput.value = payload.liveTreasuryTokens;
    }

    // Only lock UI if contract is deployed on the CURRENT network
    if (payload?.contractDeployed) {
      contractDeploymentLocked = true;
      applyPrefill(payload.prefill);
      lockUiForDeployedContract(payload);
    } else {
      // Contract not deployed on current network - unlock UI
      contractDeploymentLocked = false;
      clearPermanentToast();
      treasuryTokensInput.value = "";
      contractAddressInput.value = "";
    }
    updateHeaderTreasury();
    evaluateLockState();
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
    evaluateLockState();
  });

  treasuryAddressInput.addEventListener("input", updateHeaderTreasury);

  resetBtn.addEventListener("click", () => {
    const preserveTreasury = treasuryGenerated
      ? {
          addr: treasuryAddressInput.value,
          eth: treasuryEthInput.value,
          tokens: treasuryTokensInput.value
        }
      : null;

    form.reset();
    thresholdError.style.display = "none";

    if (preserveTreasury) {
      treasuryGenerated = true;
      treasuryAddressInput.value = preserveTreasury.addr;
      treasuryEthInput.value = preserveTreasury.eth;
      treasuryTokensInput.value = preserveTreasury.tokens || "0";
      treasuryStatus.textContent = "Treasury loaded from vault";
    } else {
      treasuryTokensInput.value = "0";
      treasuryEthInput.classList.remove("eth-alert");
      treasuryGenerated = false;
      treasuryStatus.textContent = "Fill Steps 1 & 2, then generate Treasury.";
      treasuryAddressInput.value = "";
      genTreasuryBtn.disabled = false;
    }

    updateHeaderTreasury();
    evaluateLockState();
    sendToHost("reset", {});
  });

  networkSelect.addEventListener("change", () => {
    selectedNetwork = networkSelect.value;
    resetForNetworkChange();
    updateFaucetVisibility();
    sendToHost("network-changed", { network: selectedNetwork });
    logToHost(`Network changed to: ${selectedNetwork}`);
  });

  refreshEthBtn?.addEventListener("click", () => {
    if (!treasuryAddressInput.value) {
      showToast("Generate the treasury first.", true, 4000);
      return;
    }
    showProgress("Checking ETH balance...");
    sendToHost("refresh-treasury-eth", { address: treasuryAddressInput.value, network: selectedNetwork });
  });

  faucetBtn?.addEventListener("click", () => {
    if (selectedNetwork !== "sepolia") {
      showToast("Faucet available only on Sepolia.", true, 4000);
      return;
    }
    sendToHost("open-faucet", { network: selectedNetwork });
    showToast("Opening Sepolia faucet...", false, 4000);
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
    showProgress("Deploying token contract...");
  });

  // Initialize network selection
  selectedNetwork = networkSelect.value;
  updateFaucetVisibility();
  attachCopyButtons();
  
  sendToHost("bridge-ready", { ready: true });
  logToHost("Bridge initialized");
  updateHeaderTreasury();
  evaluateLockState();
});
