window.addEventListener('DOMContentLoaded', function() {
  const form = document.getElementById('token-control-form');
  const logList = document.getElementById('log-list');
  const pauseSwitch = document.getElementById('pause-switch');
  const metricStatus = document.getElementById('metric-status');
  const metricStatusPill = document.getElementById('metric-status-pill');
  const resetBtn = document.getElementById('reset-form');
  const networkSelect = document.getElementById('network-select');

  const sendBtn = document.getElementById('send-btn');
  const freezeBtn = document.getElementById('freeze-btn');
  const retrieveBtn = document.getElementById('retrieve-btn');
  const emergencyFreezeAllBtn = document.getElementById('emergency-freeze-all');
  const emergencyClearBtn = document.getElementById('emergency-clear');

  const sendToHost = (type, payload) => {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
      window.chrome.webview.postMessage({ type, payload });
    }
  };

  const logToHost = (message, level = 'info') => {
    sendToHost('log', { message, level });
  };

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
    pauseSwitch.setAttribute('data-on', next ? 'true' : 'false');
    if (next) {
      metricStatus.textContent = 'Paused';
      metricStatusPill.textContent = 'Transfers halted';
      addLog('Pause', 'System pause toggled ON.');
    } else {
      metricStatus.textContent = 'Active';
      metricStatusPill.textContent = 'Transfers enabled';
      addLog('Pause', 'System pause toggled OFF.');
    }
  });

  resetBtn.addEventListener('click', () => {
    form.reset();
    pauseSwitch.setAttribute('data-on', 'false');
    metricStatus.textContent = 'Active';
    metricStatusPill.textContent = 'Transfers enabled';
    addLog('System', 'Local form state reset.');
  });

  networkSelect.addEventListener('change', () => {
    const selectedNetwork = networkSelect.value;
    addLog('Network', `Network switched to ${selectedNetwork}`);
    sendToHost('network-changed', { network: selectedNetwork });
  });

  sendBtn.addEventListener('click', () => {
    const to = (document.getElementById('send-to').value || '0xƒ?İ').trim();
    const amount = document.getElementById('send-amount').value || '?';
    addLog('Send', `Queued send of ${amount} tokens to ${to}.`);
  });

  freezeBtn.addEventListener('click', () => {
    const addr = (document.getElementById('freeze-address').value || '0xƒ?İ').trim();
    const action = document.getElementById('freeze-action').value === 'freeze' ? 'Freeze' : 'Unfreeze';
    addLog('Freeze', `${action} rule requested for ${addr}.`);
  });

  retrieveBtn.addEventListener('click', () => {
    const from = (document.getElementById('retrieve-from').value || '0xƒ?İ').trim();
    const amount = document.getElementById('retrieve-amount').value || 'full';
    addLog('Retrieve', `Requested retrieval of ${amount} tokens from ${from} to treasury.`);
  });

  emergencyFreezeAllBtn.addEventListener('click', () => {
    addLog('Emergency', 'Emergency freeze-all requested.');
  });

  emergencyClearBtn.addEventListener('click', () => {
    addLog('Emergency', 'Clear emergency state requested.');
  });

  sendToHost('bridge-ready', { ready: true });
  logToHost('Bridge initialized');
});
