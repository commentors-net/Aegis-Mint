(function () {
  const form = document.getElementById("aegis-mint-form");
  const sharesInput = document.getElementById("gov-shares");
  const thresholdInput = document.getElementById("gov-threshold");
  const thresholdError = document.getElementById("threshold-error");
  const resetBtn = document.getElementById("reset-form");
  const validateBtn = document.getElementById("validate-config");
  const demoBtn = document.getElementById("load-demo");

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
    if (type === "host-info") {
      logToHost("Connected to host");
    } else if (type === "validation-result" && payload) {
      showToast(payload.message || "Validation completed");
    } else if (type === "mint-received") {
      showToast("Mint request received by host");
    } else if (type === "host-error" && payload?.message) {
      showToast(`Host error: ${payload.message}`, true);
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
      tokenName: document.getElementById("token-name").value,
      tokenSupply: document.getElementById("token-supply").value,
      tokenDecimals: document.getElementById("token-decimals").value,
      govShares: sharesInput.value,
      govThreshold: thresholdInput.value,
      contractAddress: document.getElementById("contract-address").value,
      treasuryAddress: document.getElementById("treasury-address").value
    };
  }

  thresholdInput.addEventListener("input", () => validateThreshold(false));
  sharesInput.addEventListener("input", () => validateThreshold(false));

  resetBtn.addEventListener("click", () => {
    form.reset();
    thresholdError.style.display = "none";
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

  demoBtn.addEventListener("click", () => {
    document.getElementById("token-name").value = "Aegis Demo Token";
    document.getElementById("token-supply").value = "1000000";
    document.getElementById("token-decimals").value = "18";
    sharesInput.value = "5";
    thresholdInput.value = "3";
    document.getElementById("treasury-address").value =
      "0x0000000000000000000000000000000000DEMO";
    document.getElementById("contract-address").value = "";
    validateThreshold(false);
    sendToHost("load-demo", collectForm());
    logToHost("Demo config loaded");
  });

  form.addEventListener("submit", (e) => {
    e.preventDefault();
    const validForm = form.reportValidity();
    const validThreshold = validateThreshold(true);
    if (!validForm || !validThreshold) return;

    sendToHost("mint-submit", collectForm());
    logToHost("Mint submitted from UI");
  });

  sendToHost("bridge-ready", { ready: true });
  logToHost("Bridge initialized");
})();
