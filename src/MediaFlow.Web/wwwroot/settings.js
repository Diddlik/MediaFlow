const settingsById = (id) => document.getElementById(id);
let currentRuntimeSettings = null;

async function settingsRequest(url, options = {}) {
  const response = await fetch(url, {
    headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
    ...options
  });

  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const body = await response.json();
      message = body.error || body.title || body.detail || message;
    } catch {}
    throw new Error(message);
  }

  return response.status === 204 ? null : response.json();
}

function applySettingsToForm(settings) {
  currentRuntimeSettings = settings;
  settingsById('settingsDryRun').checked = settings.dryRun;
  settingsById('settingsAutomationEnabled').checked = settings.automationEnabled;
  settingsById('settingsInterval').value = settings.reconciliationIntervalSeconds;
  settingsById('settingsMaxFiles').value = settings.maxFilesPerSharePerCycle;
  settingsById('settingsTimestampFallback').checked = settings.allowFilesystemTimestampFallback;
  updateModeBadge(settings);

  if (typeof appInfo !== 'undefined') {
    appInfo = { ...appInfo, ...settings };
  }
}

function updateModeBadge(settings) {
  const badge = settingsById('status');
  if (!badge) return;
  if (settings.dryRun) {
    badge.textContent = settings.automationEnabled ? 'Dry run enabled' : 'Automation disabled';
  } else {
    badge.textContent = settings.automationEnabled ? 'LIVE MODE' : 'Live mode · automation disabled';
  }
}

async function loadRuntimeSettings() {
  try {
    const settings = await settingsRequest('/api/v1/settings');
    applySettingsToForm(settings);
    settingsById('settingsMessage').textContent = '';
  } catch (error) {
    settingsById('settingsMessage').textContent = `Settings failed: ${error.message}`;
  }
}

async function loadAutomationStatus() {
  const target = settingsById('automationStatus');
  try {
    const [result, storage] = await Promise.all([
      settingsRequest('/api/v1/status'),
      settingsRequest('/api/v1/storage')
    ]);
    const automation = result.automation;
    const mode = result.mode === 'live' ? 'LIVE' : 'Dry Run';
    const enabled = result.automationEnabled ? 'automation enabled' : 'automation disabled';
    const storageText = formatStorageStatus(storage);

    if (!automation.lastCycleStartedAt) {
      target.textContent = `${mode} · ${enabled} · no automation cycle recorded yet · ${storageText}`;
      return;
    }

    const completed = automation.lastCycleCompletedAt
      ? new Date(automation.lastCycleCompletedAt).toLocaleString()
      : 'running';
    const error = automation.lastError ? ` · last error: ${automation.lastError}` : '';
    target.textContent = `${mode} · ${enabled} · last cycle ${completed} · sources ${automation.lastSourceShares} · matched ${automation.lastMatched} · executed ${automation.lastExecuted} · skipped ${automation.lastSkipped} · errors ${automation.lastErrors}${error} · ${storageText}`;
  } catch (error) {
    target.textContent = `Status failed: ${error.message}`;
  }
}

function formatStorageStatus(storage) {
  if (!storage?.items?.length) return 'no destination storage configured';
  const reserve = formatBytes(storage.minimumFreeSpaceReserveBytes);
  const items = storage.items.map(item => {
    if (!item.exists) return `${item.name}: path missing`;
    if (item.availableFreeSpaceBytes == null) return `${item.name}: free space unknown`;
    return `${item.name}: ${formatBytes(item.availableFreeSpaceBytes)} free${item.belowReserve ? ' LOW' : ''}`;
  });
  return `storage ${items.join(', ')} · reserve ${reserve}`;
}

function formatBytes(value) {
  if (value == null || Number.isNaN(Number(value))) return 'unknown';
  const bytes = Number(value);
  const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB'];
  let index = 0;
  let number = bytes;
  while (number >= 1024 && index < units.length - 1) {
    number /= 1024;
    index++;
  }
  return `${number.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
}

settingsById('settingsForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const dryRun = settingsById('settingsDryRun').checked;
  let liveModeConfirmation = null;

  if (currentRuntimeSettings?.dryRun && !dryRun) {
    liveModeConfirmation = prompt(
      'You are enabling LIVE transfers. Safe Move may delete source files after verified destination commit. Type ENABLE_LIVE_TRANSFERS to continue.'
    );
    if (liveModeConfirmation !== 'ENABLE_LIVE_TRANSFERS') {
      settingsById('settingsDryRun').checked = true;
      settingsById('settingsMessage').textContent = 'Live mode was not enabled.';
      return;
    }
  }

  const body = {
    dryRun,
    automationEnabled: settingsById('settingsAutomationEnabled').checked,
    reconciliationIntervalSeconds: Number(settingsById('settingsInterval').value),
    maxFilesPerSharePerCycle: Number(settingsById('settingsMaxFiles').value),
    allowFilesystemTimestampFallback: settingsById('settingsTimestampFallback').checked,
    liveModeConfirmation
  };

  try {
    const updated = await settingsRequest('/api/v1/settings', {
      method: 'PUT',
      body: JSON.stringify(body)
    });
    applySettingsToForm(updated);
    settingsById('settingsMessage').textContent = updated.dryRun
      ? 'Saved. Media operations remain non-destructive.'
      : 'Saved. LIVE transfers are enabled.';
    await loadAutomationStatus();
  } catch (error) {
    settingsById('settingsMessage').textContent = error.message;
    await loadRuntimeSettings();
  }
});

settingsById('resetSettings').addEventListener('click', async () => {
  if (!confirm('Reset runtime settings to the Docker/application defaults?')) return;
  try {
    const settings = await settingsRequest('/api/v1/settings', { method: 'DELETE' });
    applySettingsToForm(settings);
    settingsById('settingsMessage').textContent = 'Runtime settings reset to defaults.';
    await loadAutomationStatus();
  } catch (error) {
    settingsById('settingsMessage').textContent = error.message;
  }
});

settingsById('refreshAutomationStatus').addEventListener('click', loadAutomationStatus);

loadRuntimeSettings();
loadAutomationStatus();
setInterval(loadAutomationStatus, 15000);
