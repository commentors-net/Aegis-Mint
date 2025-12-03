(function () {
  const form = document.getElementById("aegis-mint-form");
  const sharesInput = document.getElementById("gov-shares");
  const thresholdInput = document.getElementById("gov-threshold");
  const thresholdError = document.getElementById("threshold-error");
  const resetBtn = document.getElementById("reset-form");
  const validateBtn = document.getElementById("validate-config");
  const demoBtn = document.getElementById("load-demo");
  const networkSelect = document.getElementById("network-select");

  const genEngineBtn = document.getElementById("gen-engine");
  const engineStatus = document.getElementById("engine-status");
  const engineAddressInput = document.getElementById("engine-address");

  const genTreasuryBtn = document.getElementById("gen-treasury");
  const treasuryStatus = document.getElementById("treasury-status");
  const treasuryAddressInput = document.getElementById("treasury-address");

  const mintBtn = document.querySelector(".btn-primary");

  let engineOk = false;
  let treasuryOk = false;
  mintBtn.disabled = true;

  const sendToHost = (type, payload) => {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
      window.chrome.webview.postMessage({ type, payload });
    } else {
      console.log("Host message", type, payload);
    }
  };

  const logToHost = (message, level = "info") => {
    sendToHost("log", { message, level });
  };

  window.receiveHostMessage = function (message) {
    const { type, payload } = message || {};
    console.log("Received from host:", type, payload);
    if (type === "host-info") {
      logToHost("Connected to host");
    } else if (type === "validation-result" && payload) {
      handleValidationResult(payload);
    } else if (type === "mint-received") {
      showToast("Mint request received by host");
    } else if (type === "host-error" && payload?.message) {
      showToast(`Host error: ${payload.message}`, true);
    } else if (type === "engine-generated") {
      console.log("Applying engine:", payload);
      applyEngine(payload);
    } else if (type === "treasury-generated") {
      applyTreasury(payload);
    } else if (type === "vault-status") {
      console.log("Applying vault status:", payload);
      applyVaultStatus(payload);
    }
  };

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

  function validateThreshold(showMessage) {
    const shares = parseInt(sharesInput.value, 10);
    const threshold = parseInt(thresholdInput.value, 10);
    const invalid =
      Number.isFinite(shares) &&
      Number.isFinite(threshold) &&
      threshold > shares;

    if (invalid && showMessage) {
      thresholdError.style.display = "block";
    } else {
      thresholdError.style.display = "none";
    }
    return !invalid;
  }

  function collectForm() {
    return {
      network: networkSelect?.value || "mainnet",
      tokenName: document.getElementById("token-name").value,
      tokenSupply: document.getElementById("token-supply").value,
      tokenDecimals: document.getElementById("token-decimals").value,
      govShares: sharesInput.value,
      govThreshold: thresholdInput.value,
      contractAddress: document.getElementById("contract-address").value,
      treasuryAddress: document.getElementById("treasury-address").value,
      engineAddress: document.getElementById("engine-address").value,
      engineReady: engineOk,
      treasuryReady: treasuryOk
    };
  }

  function updateMintEnabled() {
    mintBtn.disabled = !(engineOk && treasuryOk);
  }

  function applyEngine(payload) {
    const addr = payload?.address || "";
    const status = payload?.status || "Engine generated";
    if (addr) {
      engineAddressInput.value = addr;
      engineOk = true;
      genEngineBtn.disabled = true;
      engineStatus.textContent = status;
      document.getElementById("engine-pill").textContent = "Engine ready";
      updateMintEnabled();
    }
  }

  function applyTreasury(payload) {
    const addr = payload?.address || "";
    const status = payload?.status || "Treasury generated";
    if (addr) {
      treasuryAddressInput.value = addr;
      treasuryOk = true;
      genTreasuryBtn.disabled = true;
      treasuryStatus.textContent = status;
      updateMintEnabled();
    }
  }

  function applyVaultStatus(payload) {
    if (payload?.hasEngine && payload?.engineAddress) {
      engineAddressInput.value = payload.engineAddress;
      engineOk = true;
      genEngineBtn.disabled = true;
      engineStatus.textContent = "Engine loaded from vault";
      document.getElementById("engine-pill").textContent = "Engine ready";
    }
    if (payload?.hasTreasury && payload?.treasuryAddress) {
      treasuryAddressInput.value = payload.treasuryAddress;
      treasuryOk = true;
      genTreasuryBtn.disabled = true;
      treasuryStatus.textContent = "Treasury loaded from vault";
    }
    updateMintEnabled();
  }

  function handleValidationResult(payload) {
    const isValid = payload?.ok === true;
    const canMint = payload?.canMint === true;
    const message = payload?.message || "Validation completed";
    
    showToast(message, !isValid);
    
    // Update mint button state based on validation
    if (canMint && engineOk && treasuryOk) {
      mintBtn.disabled = false;
      console.log("Mint button enabled - validation passed");
    } else {
      mintBtn.disabled = true;
      console.log("Mint button disabled - validation failed or vaults not ready");
    }
  }

  thresholdInput.addEventListener("input", () => validateThreshold(false));
  sharesInput.addEventListener("input", () => validateThreshold(false));
  if (networkSelect) {
    networkSelect.addEventListener("change", () => {
      sendToHost("network-changed", { network: networkSelect.value });
      logToHost(`Network changed to ${networkSelect.value}`);
    });
  }

  resetBtn.addEventListener("click", () => {
    form.reset();
    thresholdError.style.display = "none";

    engineOk = false;
    treasuryOk = false;
    engineAddressInput.value = "";
    treasuryAddressInput.value = "";
    engineStatus.textContent = "Engine not generated";
    treasuryStatus.textContent = "Treasury not generated";
    document.getElementById("engine-pill").textContent = "Engine not generated";
    genEngineBtn.disabled = false;
    genTreasuryBtn.disabled = false;
    updateMintEnabled();
    sendToHost("reset", {});
    logToHost("Reset requested from UI");
  });

  validateBtn.addEventListener("click", () => {
    const validForm = form.reportValidity();
    const validThreshold = validateThreshold(true);
    if (validForm && validThreshold) {
      sendToHost("validate", collectForm());
      logToHost("Validate requested from UI");
    }
  });

  genEngineBtn.addEventListener("click", () => {
    if (genEngineBtn.disabled) {
      return;
    }
    engineStatus.textContent = "Requesting Engine...";
    sendToHost("generate-engine", collectForm());
    logToHost("Generate Engine requested");
  });

  genTreasuryBtn.addEventListener("click", () => {
    treasuryStatus.textContent = "Requesting Treasury...";
    sendToHost("generate-treasury", collectForm());
    logToHost("Generate Treasury requested");
  });

  demoBtn.addEventListener("click", () => {
    document.getElementById("token-name").value = "Aegis Demo Token";
    document.getElementById("token-supply").value = "1000000";
    document.getElementById("token-decimals").value = "18";
    sharesInput.value = "5";
    thresholdInput.value = "3";

    engineOk = true;
    treasuryOk = true;
    engineAddressInput.value = "0xENGINE-DEMO";
    treasuryAddressInput.value = "0xTREASURY-DEMO";
    engineStatus.textContent = "Engine generated (demo)";
    treasuryStatus.textContent = "Treasury generated (demo)";
    genEngineBtn.disabled = true;
    genTreasuryBtn.disabled = true;

    validateThreshold(false);
    updateMintEnabled();
    sendToHost("load-demo", collectForm());
    logToHost("Demo config loaded");
  });

  form.addEventListener("submit", (e) => {
    e.preventDefault();
    const validForm = form.reportValidity();
    const validThreshold = validateThreshold(true);
    if (!validForm || !validThreshold) return;
    if (!engineOk || !treasuryOk) {
      showToast("Engine and Treasury must be generated first.", true);
      return;
    }

    sendToHost("mint-submit", collectForm());
    logToHost("Mint submitted from UI");
  });

  sendToHost("bridge-ready", { ready: true });
  logToHost("Bridge initialized");
})();
