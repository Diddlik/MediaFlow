const $ = (id) => document.getElementById(id);
let presets = [];
let shares = [];

async function request(url, options = {}) {
  const response = await fetch(url, {
    headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
    ...options
  });
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const body = await response.json();
      message = body.error || body.title || message;
    } catch {}
    throw new Error(message);
  }
  return response.status === 204 ? null : response.json();
}

async function load() {
  try {
    const [info, presetData, shareData] = await Promise.all([
      request('/api/v1/info'),
      request('/api/v1/share-presets'),
      request('/api/v1/shares')
    ]);
    presets = presetData;
    shares = shareData;
    $('status').textContent = info.dryRun ? 'Dry run enabled' : 'Live mode';
    renderPresets();
    renderShares();
  } catch (error) {
    $('status').textContent = 'Offline';
    $('formMessage').textContent = error.message;
  }
}

function renderPresets() {
  const select = $('preset');
  select.innerHTML = '<option value="">Custom / none</option>' + presets
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
        </div>
      </div>
      <div class="card-actions">
        <button type="button" onclick="editShare('${share.id}')">Edit</button>
        <button type="button" class="danger" onclick="deleteShare('${share.id}')">Delete</button>
      </div>
    </article>
  `).join('');
}

function applyPreset() {
  const preset = presets.find(p => p.id === $('preset').value);
  if (!preset) return;
  $('stability').value = preset.stabilitySeconds;
  $('ignore').value = (preset.ignorePatterns || []).join('\n');
}

function resetForm() {
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
  $('shareForm').scrollIntoView({ behavior: 'smooth', block: 'start' });
};

window.deleteShare = async function(id) {
  const share = shares.find(x => x.id === id);
  if (!share || !confirm(`Delete share “${share.name}”? No media files are deleted.`)) return;
  try {
    await request(`/api/v1/shares/${id}`, { method: 'DELETE' });
    shares = await request('/api/v1/shares');
    renderShares();
    if ($('shareId').value === id) resetForm();
  } catch (error) {
    alert(error.message);
  }
};

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
    shares = await request('/api/v1/shares');
    renderShares();
    resetForm();
    $('formMessage').textContent = 'Saved.';
  } catch (error) {
    $('formMessage').textContent = error.message;
  }
});

$('preset').addEventListener('change', applyPreset);
$('newShare').addEventListener('click', () => {
  resetForm();
  $('shareForm').scrollIntoView({ behavior: 'smooth', block: 'start' });
});
$('cancelEdit').addEventListener('click', resetForm);

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>'"]/g, c => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
  })[c]);
}

load();
