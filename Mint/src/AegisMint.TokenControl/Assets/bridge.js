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

  function addLog(tag, text) {
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
    item.appendChild(meta);
    item.appendChild(textDiv);
    logList.insertBefore(item, logList.firstChild);
    logToHost(text, tag.toLowerCase() === 'emergency' ? 'warn' : 'info');
  }

  pauseSwitch.addEventListener('click', () => {
    const isOn = pauseSwitch.getAttribute('data-on') === 'true';
    const next = !isOn;
    setPauseUI(next);
    
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
    
    addLog('Send', `Sending ${amount} tokens to ${to}...`);
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
    addLog('Freeze', `${isFreezing ? 'Freezing' : 'Unfreezing'} ${addr}...`);
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
    
    addLog('Retrieve', `Retrieving ${amount || 'full balance'} from ${from}...`);
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
      default:
        break;
    }
  };

  function handleOperationResult(payload) {
    if (!payload) return;
    
    const { operation, success, transactionHash, errorMessage } = payload;
    
    if (success) {
      addLog(operation || 'Operation', `Success! TX: ${transactionHash?.substring(0, 16)}...`);
    } else {
      addLog('Error', `${operation || 'Operation'} failed: ${errorMessage || 'Unknown error'}`);
    }
  }

  sendToHost('bridge-ready', { ready: true });
  logToHost('Bridge initialized');
});
