const $ = (id) => document.getElementById(id);
let appInfo = { dryRun: true };
let presets = [];
let shares = [];
let groups = [];
let events = [];
let operations = [];

async function request(url, options = {}) {
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

async function load() {
  try {
    const [info, presetData, shareData, groupData, eventData, operationData] = await Promise.all([
      request('/api/v1/info'),
      request('/api/v1/share-presets'),
      request('/api/v1/shares'),
      request('/api/v1/source-groups/'),
      request('/api/v1/events/'),
      request('/api/v1/operations?limit=50')
    ]);
    appInfo = info;
    presets = presetData;
    shares = shareData;
    groups = groupData;
    events = eventData;
    operations = operationData;
    $('status').textContent = appInfo.dryRun ? 'Dry run enabled' : 'Live mode';
    renderAll();
  } catch (error) {
    $('status').textContent = 'Offline';
    $('formMessage').textContent = error.message;
  }
}

function renderAll() {
  renderPresets();
  renderShares();
  renderGroupChoices();
  renderGroups();
  renderEventSelectors();
  renderEvents();
  renderRoutingSources();
  renderOperations();
}

function renderPresets() {
  $('preset').innerHTML = '<option value="">Custom / none</option>' + presets
    .map(p => `<option value="${escapeHtml(p.id)}">${escapeHtml(p.displayName)}</option>`)
    .join('');
}

function renderShares() {
  const list = $('shareList');
  if (!shares.length) {
    list.innerHTML = '<div class="empty">No shares configured yet.</div>';
    return;
  }

  list.innerHTML = shares.map(share => `
    <article class="share-card">
      <div>
        <div class="share-heading">
          <strong>${escapeHtml(share.name)}</strong>
          <span class="role">${escapeHtml(share.role)}</span>
        </div>
        <code>${escapeHtml(share.path)}</code>
        <div class="meta">
          ${share.owner ? `Owner: ${escapeHtml(share.owner)} · ` : ''}
          Stability: ${share.stabilitySeconds}s · ${share.recursive ? 'recursive' : 'top-level'}
          ${share.preset ? ` · preset: ${escapeHtml(share.preset)}` : ''}
        </div>
        <div id="state-${share.id}" class="meta"></div>
      </div>
      <div class="card-actions">
        <button type="button" onclick="probeShare('${share.id}')">Test</button>
        ${share.role !== 'Destination' ? `<button type="button" onclick="scanShare('${share.id}')">Scan</button>` : ''}
        ${share.role !== 'Destination' ? `<button type="button" onclick="metadataPreview('${share.id}')">Metadata</button>` : ''}
        <button type="button" onclick="editShare('${share.id}')">Edit</button>
        <button type="button" class="danger" onclick="deleteShare('${share.id}')">Delete</button>
      </div>
    </article>
  `).join('');
}

function renderGroupChoices(selected = []) {
  const sourceShares = shares.filter(x => x.enabled && x.role !== 'Destination');
  $('groupShareChoices').innerHTML = sourceShares.length
    ? sourceShares.map(share => `
        <label><input type="checkbox" name="groupShare" value="${share.id}" ${selected.includes(share.id) ? 'checked' : ''} /> ${escapeHtml(share.name)}</label>
      `).join('')
    : '<span class="subtle">Create at least one source share first.</span>';
}

function renderGroups() {
  const list = $('groupList');
  if (!groups.length) {
    list.innerHTML = '<div class="empty">No source groups configured yet.</div>';
    return;
  }

  list.innerHTML = groups.map(group => {
    const names = group.shareIds.map(id => shares.find(x => x.id === id)?.name || id).join(', ');
    return `
      <article class="item-card">
        <div>
          <div class="item-heading"><strong>${escapeHtml(group.name)}</strong></div>
          <div class="meta">${escapeHtml(names)}</div>
        </div>
        <div class="card-actions">
          <button type="button" onclick="editGroup('${group.id}')">Edit</button>
          <button type="button" class="danger" onclick="deleteGroup('${group.id}')">Delete</button>
        </div>
      </article>`;
  }).join('');
}

function renderEventSelectors() {
  $('eventSourceGroup').innerHTML = groups.length
    ? groups.map(group => `<option value="${group.id}">${escapeHtml(group.name)}</option>`).join('')
    : '<option value="">Create a source group first</option>';

  const destinations = shares.filter(x => x.enabled && x.role !== 'Source');
  $('eventDestination').innerHTML = destinations.length
    ? destinations.map(share => `<option value="${share.id}">${escapeHtml(share.name)}</option>`).join('')
    : '<option value="">Create a destination share first</option>';
}

function renderEvents() {
  const list = $('eventList');
  if (!events.length) {
    list.innerHTML = '<div class="empty">No events yet. Create a vacation, trip or other media collection window.</div>';
    return;
  }

  list.innerHTML = events.map(event => {
    const groupName = groups.find(x => x.id === event.sourceGroupId)?.name || event.sourceGroupId;
    const destinationName = shares.find(x => x.id === event.destinationShareId)?.name || event.destinationShareId;
    const range = `${formatDate(event.startAt)} → ${event.endAt ? formatDate(event.endAt) : 'open'}`;
    return `
      <article class="item-card">
        <div>
          <div class="item-heading">
            <strong>${escapeHtml(event.name)}</strong>
            <span class="state ${escapeHtml(event.status)}">${escapeHtml(event.status)}</span>
            <span class="role">${escapeHtml(event.operationMode)}</span>
          </div>
          <div class="meta">${range}</div>
          <div class="meta">${escapeHtml(groupName)} → ${escapeHtml(destinationName)}/${escapeHtml(event.destinationFolderTemplate)}</div>
        </div>
        <div class="card-actions">
          ${event.status === 'Active'
            ? `<button type="button" class="good" onclick="stopEvent('${event.id}')">Stop</button>`
            : (event.status !== 'Archived' && event.status !== 'Cancelled'
              ? `<button type="button" class="good" onclick="startEvent('${event.id}')">Start now</button>` : '')}
          <button type="button" onclick="editEvent('${event.id}')">Edit</button>
          <button type="button" class="danger" onclick="deleteEvent('${event.id}')">Delete</button>
        </div>
      </article>`;
  }).join('');
}

function renderRoutingSources() {
  const sourceShares = shares.filter(x => x.enabled && x.role !== 'Destination');
  $('routingSource').innerHTML = sourceShares.length
    ? sourceShares.map(share => `<option value="${share.id}">${escapeHtml(share.name)}</option>`).join('')
    : '<option value="">No source shares</option>';
}

function renderOperations() {
  const list = $('operationList');
  if (!operations.length) {
    list.innerHTML = '<div class="empty">No operations recorded yet.</div>';
    return;
  }

  list.innerHTML = operations.map(operation => `
    <article class="item-card">
      <div>
        <div class="item-heading">
          <strong>${escapeHtml(operation.state)}</strong>
          <span class="state ${escapeHtml(operation.state)}">${escapeHtml(operation.state)}</span>
        </div>
        <code>${escapeHtml(operation.sourcePath)}</code>
        ${operation.destinationPath ? `<code class="route-path">→ ${escapeHtml(operation.destinationPath)}</code>` : ''}
        <div class="meta">${operation.lastError ? escapeHtml(operation.lastError) : `Started ${formatDate(operation.startedAt)}`}</div>
      </div>
    </article>
  `).join('');
}

window.probeShare = async function(id) {
  const state = $(`state-${id}`);
  state.textContent = 'Testing path…';
  try {
    const result = await request(`/api/v1/shares/${id}/probe`);
    state.textContent = result.exists && result.readable
      ? 'Path OK · readable'
      : `Path problem · ${result.error || (result.exists ? 'not readable' : 'not found')}`;
  } catch (error) {
    state.textContent = `Test failed · ${error.message}`;
  }
};

window.scanShare = async function(id) {
  const state = $(`state-${id}`);
  state.textContent = 'Scanning…';
  try {
    const result = await request(`/api/v1/shares/${id}/scan?limit=500`);
    state.textContent = `${result.total} media files · ${result.stable} stable · ${result.waitingStable} waiting`;
  } catch (error) {
    state.textContent = `Scan failed · ${error.message}`;
  }
};

window.metadataPreview = async function(id) {
  const state = $(`state-${id}`);
  state.textContent = 'Reading metadata…';
  try {
    const result = await request(`/api/v1/shares/${id}/metadata-preview?limit=5`);
    if (!result.items.length) {
      state.textContent = 'No stable media yet. Scan again after the stability interval.';
      return;
    }
    const first = result.items[0];
    const captured = first.metadata.capturedAt || 'no capture time';
    const camera = [first.metadata.cameraMake, first.metadata.cameraModel].filter(Boolean).join(' ');
    const error = first.metadata.error ? ` · ${first.metadata.error}` : '';
    state.textContent = `${result.total} metadata samples · ${captured}${camera ? ` · ${camera}` : ''}${error}`;
  } catch (error) {
    state.textContent = `Metadata failed · ${error.message}`;
  }
};

window.editShare = function(id) {
  const share = shares.find(x => x.id === id);
  if (!share) return;
  $('shareId').value = share.id;
  $('name').value = share.name;
  $('path').value = share.path;
  $('role').value = share.role;
  $('preset').value = share.preset || '';
  $('owner').value = share.owner || '';
  $('timezone').value = share.defaultTimeZone || '';
  $('stability').value = share.stabilitySeconds;
  $('ignore').value = (share.ignorePatterns || []).join('\n');
  $('enabled').checked = share.enabled;
  $('recursive').checked = share.recursive;
  $('images').checked = (share.allowedMediaTypes || []).includes('Image');
  $('videos').checked = (share.allowedMediaTypes || []).includes('Video');
  $('formTitle').textContent = `Edit ${share.name}`;
  showPanel('shareFormPanel');
};

window.deleteShare = async function(id) {
  const share = shares.find(x => x.id === id);
  if (!share || !confirm(`Delete share “${share.name}”? No media files are deleted.`)) return;
  try {
    await request(`/api/v1/shares/${id}`, { method: 'DELETE' });
    await reloadConfiguration();
  } catch (error) {
    alert(error.message);
  }
};

window.editGroup = function(id) {
  const group = groups.find(x => x.id === id);
  if (!group) return;
  $('groupId').value = group.id;
  $('groupName').value = group.name;
  $('groupFormTitle').textContent = `Edit ${group.name}`;
  renderGroupChoices(group.shareIds);
  showPanel('groupFormPanel');
};

window.deleteGroup = async function(id) {
  const group = groups.find(x => x.id === id);
  if (!group || !confirm(`Delete source group “${group.name}”?`)) return;
  try {
    await request(`/api/v1/source-groups/${id}`, { method: 'DELETE' });
    await reloadConfiguration();
  } catch (error) {
    alert(error.message);
  }
};

window.editEvent = function(id) {
  const event = events.find(x => x.id === id);
  if (!event) return;
  $('eventId').value = event.id;
  $('eventName').value = event.name;
  $('eventType').value = event.type || '';
  $('eventSourceGroup').value = event.sourceGroupId;
  $('eventDestination').value = event.destinationShareId;
  $('eventStart').value = toLocalInput(event.startAt);
  $('eventEnd').value = event.endAt ? toLocalInput(event.endAt) : '';
  $('eventStatus').value = event.status;
  $('eventOperation').value = event.operationMode;
  $('eventConflict').value = event.conflictStrategy;
  $('eventDuplicate').value = event.duplicateStrategy;
  $('eventTemplate').value = event.destinationFolderTemplate;
  $('eventFormTitle').textContent = `Edit ${event.name}`;
  showPanel('eventFormPanel');
};

window.startEvent = async function(id) {
  try {
    await request(`/api/v1/events/${id}/start`, { method: 'POST' });
    await reloadEvents();
  } catch (error) {
    alert(error.message);
  }
};

window.stopEvent = async function(id) {
  try {
    await request(`/api/v1/events/${id}/stop`, { method: 'POST' });
    await reloadEvents();
  } catch (error) {
    alert(error.message);
  }
};

window.deleteEvent = async function(id) {
  const event = events.find(x => x.id === id);
  if (!event || !confirm(`Delete event “${event.name}”?`)) return;
  try {
    await request(`/api/v1/events/${id}`, { method: 'DELETE' });
    await reloadEvents();
  } catch (error) {
    alert(error.message);
  }
};

window.executeTransfer = async function(mediaFileId, eventId) {
  const event = events.find(x => x.id === eventId);
  const action = event?.operationMode === 'Copy'
    ? 'copy this media file to the verified destination'
    : 'safe-move this media file; the source will only be deleted after destination SHA-256 verification';
  if (!confirm(`MediaFlow will ${action}. Continue?`)) return;

  try {
    const result = await request('/api/v1/transfers', {
      method: 'POST',
      body: JSON.stringify({ mediaFileId, eventId })
    });
    alert(result.message || `Transfer finished: ${result.operation.state}`);
    await refreshOperations();
    await previewRouting();
  } catch (error) {
    alert(error.message);
  }
};

async function previewRouting() {
  const id = $('routingSource').value;
  if (!id) return;
  $('routingSummary').textContent = 'Scanning stable files and evaluating events…';
  $('routingList').innerHTML = '';
  try {
    const result = await request(`/api/v1/shares/${id}/routing-preview?limit=50`);
    $('routingSummary').textContent = `${result.total} indexed · ${result.matched} matched · ${result.unmatched} unmatched · ${result.ambiguous} ambiguous${appInfo.dryRun ? ' · Dry Run: no files can be changed' : ''}`;
    $('routingList').innerHTML = result.items.length ? result.items.map(item => {
      const event = item.event;
      return `
        <article class="item-card">
          <div>
            <div class="item-heading">
              <strong>${escapeHtml(item.mediaFile.originalName)}</strong>
              <span class="state ${escapeHtml(item.state)}">${escapeHtml(item.state)}</span>
              ${event ? `<span class="role">${escapeHtml(event.name)}</span>` : ''}
            </div>
            <div class="meta">Captured: ${formatDate(item.mediaFile.capturedAt)} · via ${escapeHtml(item.mediaFile.timestampSource || 'unknown')}</div>
            ${item.destinationPath ? `<code class="route-path">→ ${escapeHtml(item.destinationPath)}</code>` : ''}
            ${item.message ? `<div class="meta">${escapeHtml(item.message)}</div>` : ''}
          </div>
          <div class="card-actions">
            ${item.state === 'Matched' && event
              ? `<button type="button" class="good" ${appInfo.dryRun ? 'disabled title="Dry Run is enabled"' : ''} onclick="executeTransfer('${item.mediaFile.id}','${event.id}')">${appInfo.dryRun ? 'Dry Run' : 'Execute'}</button>`
              : ''}
          </div>
        </article>`;
    }).join('') : '<div class="empty">No stable media files found yet.</div>';
  } catch (error) {
    $('routingSummary').textContent = `Routing preview failed · ${error.message}`;
  }
}

async function refreshOperations() {
  operations = await request('/api/v1/operations?limit=50');
  renderOperations();
}

async function reloadConfiguration() {
  [shares, groups, events] = await Promise.all([
    request('/api/v1/shares'),
    request('/api/v1/source-groups/'),
    request('/api/v1/events/')
  ]);
  renderShares();
  renderGroupChoices();
  renderGroups();
  renderEventSelectors();
  renderEvents();
  renderRoutingSources();
}

async function reloadEvents() {
  events = await request('/api/v1/events/');
  renderEvents();
}

function applyPreset() {
  const preset = presets.find(p => p.id === $('preset').value);
  if (!preset) return;
  $('stability').value = preset.stabilitySeconds;
  $('ignore').value = (preset.ignorePatterns || []).join('\n');
}

function resetShareForm() {
  $('shareForm').reset();
  $('shareId').value = '';
  $('formTitle').textContent = 'Add share';
  $('stability').value = 30;
  $('enabled').checked = true;
  $('recursive').checked = true;
  $('images').checked = true;
  $('videos').checked = true;
  $('formMessage').textContent = '';
}

function resetGroupForm() {
  $('groupForm').reset();
  $('groupId').value = '';
  $('groupFormTitle').textContent = 'Add source group';
  $('groupMessage').textContent = '';
  renderGroupChoices();
}

function resetEventForm() {
  $('eventForm').reset();
  $('eventId').value = '';
  $('eventFormTitle').textContent = 'Add event';
  $('eventType').value = 'Vacation';
  $('eventStart').value = toLocalInput(new Date().toISOString());
  $('eventStatus').value = 'Planned';
  $('eventOperation').value = 'SafeMove';
  $('eventConflict').value = 'AppendSourceName';
  $('eventDuplicate').value = 'SafeMoveToExisting';
  $('eventTemplate').value = '{event.name}';
  $('eventMessage').textContent = '';
  renderEventSelectors();
}

$('shareForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const mediaTypes = [];
  if ($('images').checked) mediaTypes.push('Image');
  if ($('videos').checked) mediaTypes.push('Video');

  const body = {
    name: $('name').value,
    path: $('path').value,
    role: $('role').value,
    enabled: $('enabled').checked,
    owner: $('owner').value || null,
    group: null,
    preset: $('preset').value || null,
    stabilitySeconds: Number($('stability').value),
    recursive: $('recursive').checked,
    defaultTimeZone: $('timezone').value || null,
    ignorePatterns: $('ignore').value.split('\n').map(x => x.trim()).filter(Boolean),
    allowedMediaTypes: mediaTypes
  };

  const id = $('shareId').value;
  try {
    await request(id ? `/api/v1/shares/${id}` : '/api/v1/shares', {
      method: id ? 'PUT' : 'POST',
      body: JSON.stringify(body)
    });
    resetShareForm();
    hidePanel('shareFormPanel');
    await reloadConfiguration();
  } catch (error) {
    $('formMessage').textContent = error.message;
  }
});

$('groupForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const shareIds = [...document.querySelectorAll('input[name="groupShare"]:checked')].map(x => x.value);
  const body = { name: $('groupName').value, shareIds };
  const id = $('groupId').value;
  try {
    await request(id ? `/api/v1/source-groups/${id}` : '/api/v1/source-groups/', {
      method: id ? 'PUT' : 'POST',
      body: JSON.stringify(body)
    });
    resetGroupForm();
    hidePanel('groupFormPanel');
    await reloadConfiguration();
  } catch (error) {
    $('groupMessage').textContent = error.message;
  }
});

$('eventForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const body = {
    name: $('eventName').value,
    type: $('eventType').value || null,
    startAt: fromLocalInput($('eventStart').value),
    endAt: $('eventEnd').value ? fromLocalInput($('eventEnd').value) : null,
    status: $('eventStatus').value,
    sourceGroupId: $('eventSourceGroup').value,
    destinationShareId: $('eventDestination').value,
    destinationFolderTemplate: $('eventTemplate').value,
    operationMode: $('eventOperation').value,
    conflictStrategy: $('eventConflict').value,
    duplicateStrategy: $('eventDuplicate').value
  };
  const id = $('eventId').value;
  try {
    await request(id ? `/api/v1/events/${id}` : '/api/v1/events/', {
      method: id ? 'PUT' : 'POST',
      body: JSON.stringify(body)
    });
    resetEventForm();
    hidePanel('eventFormPanel');
    await reloadEvents();
  } catch (error) {
    $('eventMessage').textContent = error.message;
  }
});

$('preset').addEventListener('change', applyPreset);
$('newShare').addEventListener('click', () => { resetShareForm(); showPanel('shareFormPanel'); });
$('cancelEdit').addEventListener('click', () => hidePanel('shareFormPanel'));
$('newGroup').addEventListener('click', () => { resetGroupForm(); showPanel('groupFormPanel'); });
$('cancelGroup').addEventListener('click', () => hidePanel('groupFormPanel'));
$('newEvent').addEventListener('click', () => { resetEventForm(); showPanel('eventFormPanel'); });
$('cancelEvent').addEventListener('click', () => hidePanel('eventFormPanel'));
$('previewRouting').addEventListener('click', previewRouting);
$('refreshOperations').addEventListener('click', refreshOperations);

function showPanel(id) {
  $(id).classList.remove('hidden');
  $(id).scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function hidePanel(id) { $(id).classList.add('hidden'); }

function formatDate(value) {
  if (!value) return 'unknown';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? escapeHtml(value) : date.toLocaleString();
}

function toLocalInput(value) {
  const date = new Date(value);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 16);
}

function fromLocalInput(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) throw new Error('Invalid date/time.');
  return date.toISOString();
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>'"]/g, c => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
  })[c]);
}

load();
