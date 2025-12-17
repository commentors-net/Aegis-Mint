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
  const lockBanner = document.getElementById('lock-banner');
  const lockBannerText = document.getElementById('lock-banner-text');
  const exportLogsBtn = document.getElementById('export-logs');
  const refreshBalancesBtn = document.getElementById('refresh-balances');
  const recoverBtn = document.getElementById('recover-btn');
  const tabButtons = document.querySelectorAll('.tab-btn');
  const frozenList = document.getElementById('frozen-list');
  const unfrozenList = document.getElementById('unfrozen-list');

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

  let pageEnabled = false;
  let ethPollTimer = null;
  let hasTreasuryKey = false;
  let treasuryAddressKnown = false;
  let lastEthBalance = 0;
  let lastFreezeContext = null;
  const logHistory = [];
  const frozenAddresses = new Set();
  const unfrozenAddresses = new Set();
  const frozenEntries = [];
  const unfrozenEntries = [];
  const alwaysEnabledControls = new Set([
    networkSelect?.id,
    refreshBalancesBtn?.id,
    exportLogsBtn?.id,
    recoverBtn?.id
  ].filter(Boolean));

  function attachCopyButtons() {
    const targets = document.querySelectorAll('input.input, textarea.input, textarea.textarea');
    targets.forEach((el) => {
      if (!el) return;
      let wrapper = el.closest('.input-copy-wrapper');
      if (!wrapper) {
        wrapper = document.createElement('div');
        wrapper.className = 'input-copy-wrapper';
        el.parentNode.insertBefore(wrapper, el);
        wrapper.appendChild(el);
      } else if (wrapper.querySelector('.copy-btn')) {
        return;
      }

      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'copy-btn';
      btn.textContent = '⧉';
      btn.addEventListener('click', async () => {
        const value = (el.value || '').toString().trim();
        if (!value) {
          showToast('Nothing to copy', true, 3000);
          return;
        }
        try {
          await navigator.clipboard.writeText(value);
          showToast('Copied', false, 3000);
        } catch (err) {
          console.error('Copy failed', err);
          showToast('Copy failed', true, 4000);
        }
      });
      wrapper.appendChild(btn);
    });
  }

  function showToast(text, isError = false, durationMs = 7000) {
    let container = document.getElementById('toast-container');
    if (!container) {
      container = document.createElement('div');
      container.id = 'toast-container';
      container.style.position = 'fixed';
      container.style.bottom = '18px';
      container.style.left = '18px';
      container.style.display = 'flex';
      container.style.flexDirection = 'column';
      container.style.alignItems = 'flex-start';
      container.style.gap = '8px';
      container.style.zIndex = '1000';
      document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.textContent = text;
    toast.style.padding = '10px 14px';
    toast.style.borderRadius = '12px';
    toast.style.background = isError ? 'rgba(215, 38, 56, 0.9)' : 'rgba(16, 185, 129, 0.9)';
    toast.style.color = '#0f172a';
    toast.style.boxShadow = '0 10px 24px rgba(0,0,0,0.2)';
    toast.style.maxWidth = '320px';
    toast.style.wordBreak = 'break-word';
    container.prepend(toast);

    setTimeout(() => {
      toast.remove();
      if (container.childElementCount === 0) {
        container.remove();
      }
    }, durationMs);
  }

  function setPageEnabled(enabled, reason) {
    pageEnabled = enabled;
    const controls = document.querySelectorAll('input, select, textarea, button');
    controls.forEach((el) => {
      if (!el) return;
      if (el.classList.contains('copy-btn')) return;
      if (alwaysEnabledControls.has(el.id)) return;
      if (!enabled) {
        el.disabled = true;
        el.classList.add('locked-control');
      } else {
        el.disabled = false;
        el.classList.remove('locked-control');
      }
    });

    if (lockBanner && lockBannerText) {
      if (enabled) {
        lockBanner.style.display = 'none';
      } else {
        lockBanner.style.display = 'block';
        lockBannerText.textContent = reason || 'Load or import the treasury key and fund it with ETH to continue.';
      }
    }
  }

  function startEthPolling() {
    if (ethPollTimer || !treasuryAddressKnown) return;
    sendToHost('refresh-balances', {});
    ethPollTimer = setInterval(() => {
      sendToHost('refresh-balances', {});
    }, 12000);
  }

  function stopEthPolling() {
    if (ethPollTimer) {
      clearInterval(ethPollTimer);
      ethPollTimer = null;
    }
  }

  function evaluateLockState() {
    const hasEth = treasuryAddressKnown && lastEthBalance > 0;

    if (!treasuryAddressKnown) {
      stopEthPolling();
      setPageEnabled(false, 'Add or import a treasury address to continue.');
      return;
    }

    if (!hasTreasuryKey) {
      startEthPolling();
      setPageEnabled(false, 'Treasury key not found. Install Mint or import the key.');
      return;
    }

    if (!hasEth) {
      setPageEnabled(false, 'Treasury has 0 ETH. Fund then refresh.');
      startEthPolling();
      return;
    }

    stopEthPolling();
    setPageEnabled(true);
  }

  function activateTab(targetId) {
    if (!tabButtons || !tabButtons.length) return;
    tabButtons.forEach((btn) => {
      const isActive = btn.dataset.target === targetId;
      btn.classList.toggle('active', isActive);
    });

    if (logList) logList.style.display = targetId === 'log-list' ? 'flex' : 'none';
    if (frozenList) frozenList.style.display = targetId === 'frozen-list' ? 'flex' : 'none';
    if (unfrozenList) unfrozenList.style.display = targetId === 'unfrozen-list' ? 'flex' : 'none';
  }

  function formatTimestamp(dateLike) {
    const date = dateLike instanceof Date ? dateLike : new Date(dateLike);
    return date.toLocaleString();
  }

  function recordFreezeChange(address, frozen) {
    if (!address) return;
    if (frozen) {
      frozenAddresses.add(address);
      unfrozenAddresses.delete(address);
      frozenEntries.unshift({ address, at: new Date() });
    } else {
      unfrozenAddresses.add(address);
      frozenAddresses.delete(address);
      unfrozenEntries.unshift({ address, at: new Date() });
    }
    renderAddressLists();
  }

  function renderAddressLists() {
    if (frozenList) {
      frozenList.innerHTML = '';
      frozenEntries.slice(0, 50).forEach((entry) => {
        const item = document.createElement('div');
        item.className = 'log-item';
        item.innerHTML = `<div class="log-meta"><span class="log-tag">Frozen</span><span>${formatTimestamp(entry.at)}</span></div><div>${entry.address}</div>`;
        frozenList.appendChild(item);
      });
      if (frozenEntries.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'log-item';
        empty.textContent = 'No frozen addresses yet.';
        frozenList.appendChild(empty);
      }
    }

    if (unfrozenList) {
      unfrozenList.innerHTML = '';
      unfrozenEntries.slice(0, 50).forEach((entry) => {
        const item = document.createElement('div');
        item.className = 'log-item';
        item.innerHTML = `<div class="log-meta"><span class="log-tag">Unfrozen</span><span>${formatTimestamp(entry.at)}</span></div><div>${entry.address}</div>`;
        unfrozenList.appendChild(item);
      });
      if (unfrozenEntries.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'log-item';
        empty.textContent = 'No unfrozen addresses yet.';
        unfrozenList.appendChild(empty);
      }
    }
  }

  function exportLogs() {
    if (!logHistory.length) {
      showToast('No logs to export yet.', true, 4000);
      return;
    }

    const header = ['Timestamp', 'Tag', 'Message', 'TransactionHash', 'Address'];
    const lines = [header.join(',')];
    logHistory.forEach((entry) => {
      const safe = (value) => `"${(value || '').toString().replace(/\"/g, '""')}"`;
      lines.push([
        safe(entry.timestamp),
        safe(entry.tag),
        safe(entry.text),
        safe(entry.txHash || ''),
        safe(entry.address || '')
      ].join(','));
    });

    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `aegis-token-control-logs-${Date.now()}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
    showToast('Logs exported', false, 4000);
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

      function addLog(tag, text, txHash = null, address = null, timestamp = new Date()) {
    const ts = timestamp instanceof Date ? timestamp : new Date(timestamp);
    const item = document.createElement('div');
    item.className = 'log-item';
    const meta = document.createElement('div');
    meta.className = 'log-meta';
    const tagSpan = document.createElement('span');
    tagSpan.className = 'log-tag';
    tagSpan.textContent = tag;
    const timeSpan = document.createElement('span');
    timeSpan.textContent = formatTimestamp(ts);
    meta.appendChild(tagSpan);
    meta.appendChild(timeSpan);
    const textDiv = document.createElement('div');
    textDiv.textContent = text;
    textDiv.style.wordBreak = 'break-all';
    textDiv.style.userSelect = 'text';
    textDiv.style.cursor = 'text';
    item.appendChild(meta);
    item.appendChild(textDiv);
    
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
          txDiv.textContent = 'Copied!';
          setTimeout(() => { txDiv.textContent = original; }, 1500);
        } catch (err) {
          console.error('Failed to copy:', err);
        }
      });
      item.appendChild(txDiv);
    }
    
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
          addrDiv.textContent = 'Copied!';
          setTimeout(() => { addrDiv.textContent = original; }, 1500);
        } catch (err) {
          console.error('Failed to copy:', err);
        }
      });
      item.appendChild(addrDiv);
    }
    
    if (logList) {
      logList.insertBefore(item, logList.firstChild);
    }

    logHistory.unshift({
      tag,
      text,
      txHash,
      address,
      timestamp: ts.toISOString()
    });
    if (logHistory.length > 300) {
      logHistory.pop();
    }

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
    lastEthBalance = 0;
    evaluateLockState();
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
    lastFreezeContext = { address: addr, freeze: isFreezing };
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

  refreshBalancesBtn?.addEventListener('click', () => {
    showProgress('Refreshing balances...');
    sendToHost('refresh-balances', {});
  });

  recoverBtn?.addEventListener('click', () => {
    showProgress('Recovering treasury from shares...');
    sendToHost('recover-from-shares', {});
  });

  exportLogsBtn?.addEventListener('click', exportLogs);

  if (tabButtons && tabButtons.length) {
    tabButtons.forEach((btn) => {
      btn.addEventListener('click', () => activateTab(btn.dataset.target));
    });
  }

  if (logList) {
    logList.innerHTML = '';
  }
  activateTab('log-list');
  renderAddressLists();
  attachCopyButtons();
  addLog('System', 'Token Control console opened.', null, null, new Date());
  evaluateLockState();

  function applyNetworkFromHost(network) {
    if (!networkSelect || !network) return;
    const option = Array.from(networkSelect.options).find(opt => opt.value === network);
    if (option) {
      networkSelect.value = network;
    }
  }

  function applyVaultStatus(payload) {
    if (!payload) return;
    hasTreasuryKey = payload.hasTreasuryKey !== undefined ? !!payload.hasTreasuryKey : !!payload.hasTreasury;
    treasuryAddressKnown = !!payload.treasuryAddress;
    if (recoverBtn) {
      if (hasTreasuryKey) {
        recoverBtn.style.display = 'none';
        recoverBtn.disabled = true;
      } else {
        recoverBtn.style.display = 'inline-flex';
        recoverBtn.disabled = false;
      }
    }
    if (payload.currentNetwork) {
      applyNetworkFromHost(payload.currentNetwork);
    }
    if (payload.treasuryAddress) {
      if (sendFromInput) sendFromInput.value = payload.treasuryAddress;
      if (retrieveToInput) retrieveToInput.value = payload.treasuryAddress;
    } else {
      treasuryAddressKnown = false;
      if (sendFromInput) sendFromInput.value = '';
      if (retrieveToInput) retrieveToInput.value = '';
    }
    if (payload.contractAddress) {
      setContractAddress(payload.contractAddress);
      addLog('Contract', `Active contract: ${payload.contractAddress.substring(0, 16)}...`);
    }
    evaluateLockState();
  }

  function applyBalanceStats(payload) {
    if (!payload) return;
    hideProgress();
    
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
    if (payload.ethBalance !== undefined) {
      const raw = payload.ethBalance.toString().replace(/,/g, '').replace(/ ETH/i, '');
      const ethVal = Number(raw);
      lastEthBalance = Number.isFinite(ethVal) ? ethVal : 0;
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

    evaluateLockState();
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
      case 'treasury-eth-updated':
        if (payload?.eth !== undefined) {
          const value = Number(payload.eth);
          const statEthBalance = document.getElementById('stat-eth-balance');
          if (statEthBalance) {
            statEthBalance.textContent = `${Number.isNaN(value) ? payload.eth : value.toFixed(6)} ETH`;
          }
          lastEthBalance = Number.isFinite(value) ? value : lastEthBalance;
          evaluateLockState();
        }
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
      case 'recovery-result':
        if (payload?.message) {
          showToast(payload.message, false, 6000);
        }
        if (recoverBtn) {
          recoverBtn.style.display = 'none';
          recoverBtn.disabled = true;
        }
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
    
    const { operation, success, transactionHash, errorMessage, address, timestamp } = payload;
    const opName = operation || 'Operation';
    const ts = timestamp || new Date().toISOString();
    const addressForLog = address || (opName === 'Freeze' && lastFreezeContext?.address ? lastFreezeContext.address : null);

    if (success) {
      addLog(opName, `${opName} completed successfully`, transactionHash, addressForLog, ts);
      showToast(`${opName} completed`, false, 5000);
      if (opName === 'Freeze' && lastFreezeContext?.address) {
        recordFreezeChange(lastFreezeContext.address, !!lastFreezeContext.freeze);
      }
      sendToHost('refresh-balances', {});
    } else {
      addLog('Error', `${opName} failed: ${errorMessage || 'Unknown error'}`, transactionHash, addressForLog, ts);
      showToast(errorMessage || `${opName} failed`, true, 7000);
    }

    if (opName === 'Freeze') {
      lastFreezeContext = null;
    }
  }

  sendToHost('bridge-ready', { ready: true });
  logToHost('Bridge initialized');
});
