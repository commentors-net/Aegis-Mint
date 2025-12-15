window.addEventListener('DOMContentLoaded', function() {
  const form = document.getElementById('token-control-form');
  const logList = document.getElementById('log-list');
  const pauseSwitch = document.getElementById('pause-switch');
  const metricStatus = document.getElementById('metric-status');
  const metricStatusPill = document.getElementById('metric-status-pill');
  const resetBtn = document.getElementById('reset-form');
  const networkSelect = document.getElementById('network-select');
  const contractPill = document.getElementById('contract-pill');
  const contractPillValue = document.getElementById('contract-pill-value');
  const contractPillDot = document.getElementById('contract-pill-dot');

  function ensureProgressStyles() {
    if (document.getElementById("progress-style")) return;
    const style = document.createElement("style");
    style.id = "progress-style";
    style.textContent = `
      @keyframes token-progress-stripe {
        0% { background-position: 0 0; }
        100% { background-position: 200% 0; }
      }
    `;
    document.head.appendChild(style);
  }

  function showProgress(text = "Processing...") {
    ensureProgressStyles();
    let container = document.getElementById("token-progress");
    if (!container) {
      container = document.createElement("div");
      container.id = "token-progress";
      container.style.position = "fixed";
      container.style.bottom = "18px";
      container.style.left = "50%";
      container.style.transform = "translateX(-50%)";
      container.style.minWidth = "260px";
      container.style.maxWidth = "520px";
      container.style.background = "rgba(255, 255, 255, 0.95)";
      container.style.border = "1px solid rgba(215, 38, 56, 0.6)";
      container.style.borderRadius = "14px";
      container.style.boxShadow = "0 12px 30px rgba(215, 38, 56, 0.25)";
      container.style.padding = "12px 14px 16px";
      container.style.zIndex = "1200";

      const label = document.createElement("div");
      label.id = "token-progress-label";
      label.style.color = "#0f172a";
      label.style.fontSize = "13px";
      label.style.fontWeight = "600";
      label.style.marginBottom = "8px";
      container.appendChild(label);

      const bar = document.createElement("div");
      bar.style.width = "100%";
      bar.style.height = "8px";
      bar.style.borderRadius = "999px";
      bar.style.overflow = "hidden";
      bar.style.background = "rgba(215, 38, 56, 0.18)";

      const inner = document.createElement("div");
      inner.id = "token-progress-bar";
      inner.style.width = "60%";
      inner.style.height = "100%";
      inner.style.borderRadius = "999px";
      inner.style.background = "linear-gradient(90deg, rgba(215,38,56,0.9), rgba(239,68,68,0.9), rgba(215,38,56,0.9))";
      inner.style.backgroundSize = "200% 100%";
      inner.style.animation = "token-progress-stripe 1.2s linear infinite";

      bar.appendChild(inner);
      container.appendChild(bar);

      document.body.appendChild(container);
    }

    const labelEl = document.getElementById("token-progress-label");
    if (labelEl) {
      labelEl.textContent = text;
    }
  }

  function hideProgress() {
    const container = document.getElementById("token-progress");
    if (container) {
      container.remove();
    }
  }

  const sendBtn = document.getElementById('send-btn');
  const freezeBtn = document.getElementById('freeze-btn');
  const retrieveBtn = document.getElementById('retrieve-btn');
  const emergencyFreezeAllBtn = document.getElementById('emergency-freeze-all');
  const emergencyClearBtn = document.getElementById('emergency-clear');
  const sendFromInput = document.getElementById('send-from');
  const retrieveToInput = document.getElementById('retrieve-to');

  const sendToHost = (type, payload) => {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
      window.chrome.webview.postMessage({ type, payload });
    }
  };

  const logToHost = (message, level = 'info') => {
    sendToHost('log', { message, level });
  };

  function setContractAddress(addr) {
    if (!addr || !contractPillValue) return;
    const short = addr.length > 16 ? `${addr.substring(0, 8)}...${addr.substring(addr.length - 6)}` : addr;
    contractPillValue.textContent = short;
    contractPillValue.title = addr;
    if (contractPill) {
      contractPill.title = `Copy contract address (${addr})`;
    }
    window.contractAddress = addr;
  }

  async function copyContractAddress() {
    if (!window.contractAddress) return;
    try {
      await navigator.clipboard.writeText(window.contractAddress);
      addLog('Copy', 'Contract address copied to clipboard.');
    } catch (err) {
      addLog('Error', 'Failed to copy contract address.');
    }
  }

  if (contractPill) {
    contractPill.addEventListener('click', () => copyContractAddress());
  }

  function setPauseUI(paused, options = {}) {
    if (pauseSwitch) {
      pauseSwitch.setAttribute('data-on', paused ? 'true' : 'false');
    }
    if (metricStatus) {
      metricStatus.textContent = paused ? 'Paused' : 'Active';
    }
    if (metricStatusPill) {
      metricStatusPill.textContent = paused ? 'Transfers halted' : 'Transfers enabled';
    }
    if (contractPillDot) {
      if (paused) {
        contractPillDot.classList.add('paused');
      } else {
        contractPillDot.classList.remove('paused');
      }
    }
    if (options.logChange) {
      addLog('Pause', `System pause ${paused ? 'ON' : 'OFF'}${options.reason ? ` (${options.reason})` : ''}`);
    }
  }

  function addLog(tag, text, txHash = null, address = null) {
    const item = document.createElement('div');
    item.className = 'log-item';
    const meta = document.createElement('div');
    meta.className = 'log-meta';
    const tagSpan = document.createElement('span');
    tagSpan.className = 'log-tag';
    tagSpan.textContent = tag;
    const timeSpan = document.createElement('span');
    timeSpan.textContent = 'Now';
    meta.appendChild(tagSpan);
    meta.appendChild(timeSpan);
    const textDiv = document.createElement('div');
    textDiv.textContent = text;
    textDiv.style.wordBreak = 'break-all';
    textDiv.style.userSelect = 'text';
    textDiv.style.cursor = 'text';
    item.appendChild(meta);
    item.appendChild(textDiv);
    
    // Add transaction hash if provided
    if (txHash) {
      const txDiv = document.createElement('div');
      txDiv.style.marginTop = '4px';
      txDiv.style.padding = '4px 6px';
      txDiv.style.background = 'rgba(215, 38, 56, 0.08)';
      txDiv.style.borderRadius = '6px';
      txDiv.style.fontSize = '10px';
      txDiv.style.fontFamily = 'monospace';
      txDiv.style.wordBreak = 'break-all';
      txDiv.style.userSelect = 'text';
      txDiv.style.cursor = 'pointer';
      txDiv.title = 'Click to copy transaction hash';
      txDiv.textContent = `TX: ${txHash}`;
      txDiv.addEventListener('click', async () => {
        try {
          await navigator.clipboard.writeText(txHash);
          const original = txDiv.textContent;
          txDiv.textContent = '✓ Copied!';
          setTimeout(() => { txDiv.textContent = original; }, 1500);
        } catch (err) {
          console.error('Failed to copy:', err);
        }
      });
      item.appendChild(txDiv);
    }
    
    // Add address if provided
    if (address) {
      const addrDiv = document.createElement('div');
      addrDiv.style.marginTop = '4px';
      addrDiv.style.padding = '4px 6px';
      addrDiv.style.background = 'rgba(52, 211, 153, 0.12)';
      addrDiv.style.borderRadius = '6px';
      addrDiv.style.fontSize = '10px';
      addrDiv.style.fontFamily = 'monospace';
      addrDiv.style.wordBreak = 'break-all';
      addrDiv.style.userSelect = 'text';
      addrDiv.style.cursor = 'pointer';
      addrDiv.title = 'Click to copy address';
      addrDiv.textContent = `Address: ${address}`;
      addrDiv.addEventListener('click', async () => {
        try {
          await navigator.clipboard.writeText(address);
          const original = addrDiv.textContent;
          addrDiv.textContent = '✓ Copied!';
          setTimeout(() => { addrDiv.textContent = original; }, 1500);
        } catch (err) {
          console.error('Failed to copy:', err);
        }
      });
      item.appendChild(addrDiv);
    }
    
    logList.insertBefore(item, logList.firstChild);
    logToHost(text, tag.toLowerCase() === 'emergency' ? 'warn' : 'info');
  }

  pauseSwitch.addEventListener('click', () => {
    const isOn = pauseSwitch.getAttribute('data-on') === 'true';
    const next = !isOn;
    setPauseUI(next);
    
    showProgress(`${next ? 'Pausing' : 'Unpausing'} token contract...`);
    sendToHost('set-paused', { paused: next });
    
    addLog('Pause', `System pause toggled ${next ? 'ON' : 'OFF'}.`);
  });

  resetBtn.addEventListener('click', () => {
    form.reset();
    setPauseUI(false);
    addLog('System', 'Local form state reset.');
  });

  networkSelect.addEventListener('change', () => {
    const selectedNetwork = networkSelect.value;
    addLog('Network', `Network switched to ${selectedNetwork}`);
    sendToHost('network-changed', { network: selectedNetwork });
  });

  sendBtn.addEventListener('click', () => {
    const to = (document.getElementById('send-to').value || '').trim();
    const amount = document.getElementById('send-amount').value || '';
    const memo = document.getElementById('send-memo').value || '';
    
    if (!to || !amount) {
      addLog('Error', 'Please provide recipient address and amount.');
      return;
    }
    
    showProgress(`Sending ${amount} tokens...`);
    addLog('Send', `Initiating transfer: ${amount} tokens`, null, to);
    sendToHost('send-tokens', { to, amount, memo });
  });

  freezeBtn.addEventListener('click', () => {
    const addr = (document.getElementById('freeze-address').value || '').trim();
    const action = document.getElementById('freeze-action').value;
    const reason = document.getElementById('freeze-reason').value || '';
    
    if (!addr) {
      addLog('Error', 'Please provide an address to freeze/unfreeze.');
      return;
    }
    
    const isFreezing = action === 'freeze';
    showProgress(`${isFreezing ? 'Freezing' : 'Unfreezing'} address...`);
    addLog('Freeze', `${isFreezing ? 'Freezing' : 'Unfreezing'} address`, null, addr);
    sendToHost('freeze-address', { address: addr, freeze: isFreezing, reason });
  });

  retrieveBtn.addEventListener('click', () => {
    const from = (document.getElementById('retrieve-from').value || '').trim();
    const amount = document.getElementById('retrieve-amount').value || '';
    const reason = document.getElementById('retrieve-memo').value || '';
    
    if (!from) {
      addLog('Error', 'Please provide a source address.');
      return;
    }
    
    showProgress(`Retrieving tokens from frozen address...`);
    addLog('Retrieve', `Retrieving ${amount || 'full balance'} from address`, null, from);
    sendToHost('retrieve-tokens', { from, amount, reason });
  });

  emergencyFreezeAllBtn.addEventListener('click', () => {
    addLog('Emergency', 'Emergency freeze-all requested.');
  });

  emergencyClearBtn.addEventListener('click', () => {
    addLog('Emergency', 'Clear emergency state requested.');
  });

  function applyNetworkFromHost(network) {
    if (!networkSelect || !network) return;
    const option = Array.from(networkSelect.options).find(opt => opt.value === network);
    if (option) {
      networkSelect.value = network;
    }
  }

  function applyVaultStatus(payload) {
    if (!payload) return;
    if (payload.currentNetwork) {
      applyNetworkFromHost(payload.currentNetwork);
    }
    if (payload.treasuryAddress) {
      if (sendFromInput) sendFromInput.value = payload.treasuryAddress;
      if (retrieveToInput) retrieveToInput.value = payload.treasuryAddress;
    }
    if (payload.contractAddress) {
      setContractAddress(payload.contractAddress);
      addLog('Contract', `Active contract: ${payload.contractAddress.substring(0, 16)}...`);
    }
  }

  function applyBalanceStats(payload) {
    if (!payload) return;
    
    const statTokenBalance = document.getElementById('stat-token-balance');
    const statEthBalance = document.getElementById('stat-eth-balance');
    const statContractAddress = document.getElementById('stat-contract-address');
    const statTotalSupply = document.getElementById('stat-total-supply');
    
    if (statTokenBalance && payload.tokenBalance !== undefined) {
      statTokenBalance.textContent = `${payload.tokenBalance} TOK`;
    }
    if (statEthBalance && payload.ethBalance !== undefined) {
      statEthBalance.textContent = `${payload.ethBalance} ETH`;
    }
    if (statContractAddress && payload.contractAddress) {
      const addr = payload.contractAddress;
      const short = addr.length > 16 ? `${addr.substring(0, 8)}...${addr.substring(addr.length - 6)}` : addr;
      statContractAddress.textContent = short;
      statContractAddress.title = addr;
      setContractAddress(addr);
    }
    if (statTotalSupply && payload.totalSupply !== undefined) {
      statTotalSupply.textContent = `${payload.totalSupply} TOK`;
    }
  }

  function applyPauseStatus(payload) {
    if (!payload) return;
    const paused = !!payload.paused;
    const current = pauseSwitch?.getAttribute('data-on') === 'true';
    setPauseUI(paused);
    if (current !== paused) {
      addLog('Pause', `System pause ${paused ? 'ON' : 'OFF'} (synced from network)`);
    }
  }

  window.receiveHostMessage = function(message) {
    const { type, payload } = message || {};
    switch (type) {
      case 'host-info':
        applyNetworkFromHost(payload?.network);
        logToHost('Host connected');
        break;
      case 'vault-status':
        applyVaultStatus(payload);
        break;
      case 'balance-stats':
        applyBalanceStats(payload);
        break;
      case 'pause-status':
        applyPauseStatus(payload);
        break;
      case 'operation-result':
        handleOperationResult(payload);
        break;
      case 'host-error':
        hideProgress();
        addLog('Error', payload?.message || 'Host error occurred');
        break;
      case 'operation-progress':
        if (payload?.message) {
          showProgress(payload.message);
        }
        break;
      default:
        break;
    }
  };

  function handleOperationResult(payload) {
    hideProgress();
    
    if (!payload) return;
    
    const { operation, success, transactionHash, errorMessage } = payload;
    
    if (success) {
      addLog(operation || 'Operation', `✓ ${operation} completed successfully`, transactionHash);
    } else {
      addLog('Error', `${operation || 'Operation'} failed: ${errorMessage || 'Unknown error'}`, transactionHash);
    }
  }

  sendToHost('bridge-ready', { ready: true });
  logToHost('Bridge initialized');
});
