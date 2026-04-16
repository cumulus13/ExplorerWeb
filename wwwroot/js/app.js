'use strict';

// ── API ────────────────────────────────────────────────────────────────────────
const api = {
  async list(path, sort, dir, hidden) {
    const u = new URLSearchParams({path, sort, dir, hidden});
    const r = await fetch(`/api/explorer/list?${u}`);
    return r.json();
  },
  async search(path, q) {
    const u = new URLSearchParams({path, q});
    const r = await fetch(`/api/explorer/search?${u}`);
    return r.json();
  },
  async properties(path) {
    const r = await fetch(`/api/explorer/properties?path=${encodeURIComponent(path)}`);
    return r.json();
  },
  async newFolder(parentPath, name) {
    const r = await fetch('/api/explorer/newfolder', post({parentPath, name}));
    return r.json();
  },
  async rename(fullPath, newName) {
    const r = await fetch('/api/explorer/rename', post({fullPath, newName}));
    return r.json();
  },
  async delete(paths, permanent) {
    const r = await fetch('/api/explorer/delete', post({paths, permanent}));
    return r.json();
  },
  async copy(sourcePaths, destDir) {
    const r = await fetch('/api/explorer/copy', post({sourcePaths, destDir}));
    return r.json();
  },
  async move(sourcePaths, destDir) {
    const r = await fetch('/api/explorer/move', post({sourcePaths, destDir}));
    return r.json();
  },
  previewUrl(path) { return `/api/explorer/preview?path=${encodeURIComponent(path)}`; },
  downloadUrl(path){ return `/api/explorer/download?path=${encodeURIComponent(path)}`; },
};
const post = body => ({method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(body)});

// ── State ──────────────────────────────────────────────────────────────────────
let currentPath  = '';
let listing      = null;
let selected     = new Set();       // fullPaths
let clipboard    = null;            // {paths:[], op:'copy'|'cut'}
let viewMode     = 'list';
let sortField    = 'name';
let sortDir      = 'asc';
let showHidden   = true;
let previewOpen  = false;
let searchMode   = false;
let searchResults= [];

// History for back/forward
const history    = { stack: [''], index: 0 };

// Special folder paths (resolved server-side via environment)
const SPECIAL = {
  desktop:   () => `${getEnvPath('USERPROFILE')}\\Desktop`,
  documents: () => `${getEnvPath('USERPROFILE')}\\Documents`,
  downloads: () => `${getEnvPath('USERPROFILE')}\\Downloads`,
  pictures:  () => `${getEnvPath('USERPROFILE')}\\Pictures`,
  music:     () => `${getEnvPath('USERPROFILE')}\\Music`,
  videos:    () => `${getEnvPath('USERPROFILE')}\\Videos`,
};
let userProfile = '';

// ── Toast ──────────────────────────────────────────────────────────────────────
function toast(msg, type='info', ms=3000) {
  const icons = {success:'✅',error:'❌',warn:'⚠️',info:'ℹ️'};
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.innerHTML = `<span style="font-size:15px;flex-shrink:0">${icons[type]||'ℹ'}</span><span>${msg}</span>`;
  document.getElementById('toasts').appendChild(el);
  setTimeout(() => { el.classList.add('out'); setTimeout(() => el.remove(), 300); }, ms);
}

function setStatus(msg, right='') {
  document.getElementById('status-text').textContent  = msg;
  document.getElementById('status-right').textContent = right;
}

function esc(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function fmtDate(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, {year:'numeric',month:'short',day:'numeric'})
       + ' ' + d.toLocaleTimeString(undefined, {hour:'2-digit',minute:'2-digit'});
}

// ── Modals ─────────────────────────────────────────────────────────────────────
function confirm(title, body, okLabel='OK', icon='⚠️') {
  return new Promise(resolve => {
    document.getElementById('confirm-title').textContent = title;
    document.getElementById('confirm-body').textContent  = body;
    document.getElementById('confirm-icon').textContent  = icon;
    document.getElementById('confirm-ok').textContent    = okLabel;
    document.getElementById('confirm-overlay').classList.remove('hidden');
    const cleanup = v => {
      document.getElementById('confirm-overlay').classList.add('hidden');
      ['confirm-ok','confirm-cancel'].forEach(id => {
        const el = document.getElementById(id);
        el.replaceWith(el.cloneNode(true));
      });
      resolve(v);
    };
    document.getElementById('confirm-ok').addEventListener('click',     () => cleanup(true));
    document.getElementById('confirm-cancel').addEventListener('click', () => cleanup(false));
  });
}

function promptDialog(overlayId, inputId, okId, cancelId, defaultVal = '') {
  return new Promise(resolve => {
    const input = document.getElementById(inputId);
    input.value = defaultVal;
    document.getElementById(overlayId).classList.remove('hidden');
    setTimeout(() => { input.focus(); input.select(); }, 50);
    const cleanup = v => {
      document.getElementById(overlayId).classList.add('hidden');
      [okId, cancelId].forEach(id => {
        const el = document.getElementById(id);
        el.replaceWith(el.cloneNode(true));
      });
      resolve(v);
    };
    document.getElementById(okId).addEventListener('click',     () => cleanup(input.value.trim()));
    document.getElementById(cancelId).addEventListener('click', () => cleanup(null));
    input.addEventListener('keydown', e => {
      if (e.key === 'Enter')  cleanup(input.value.trim());
      if (e.key === 'Escape') cleanup(null);
    });
  });
}

// ── Navigation ─────────────────────────────────────────────────────────────────
function navigate(path, addToHistory = true) {
  if (addToHistory) {
    history.stack = history.stack.slice(0, history.index + 1);
    history.stack.push(path);
    history.index = history.stack.length - 1;
  }
  currentPath = path;
  selected.clear();
  searchMode = false;
  document.getElementById('search-input').value = '';
  document.getElementById('search-clear').classList.add('hidden');
  load();
}

function goBack() {
  if (history.index > 0) {
    history.index--;
    currentPath = history.stack[history.index];
    selected.clear(); searchMode = false;
    load();
  }
}

function goForward() {
  if (history.index < history.stack.length - 1) {
    history.index++;
    currentPath = history.stack[history.index];
    selected.clear(); searchMode = false;
    load();
  }
}

function goUp() {
  if (!currentPath) return;
  const parent = listing?.parentPath;
  if (parent !== undefined && parent !== null) navigate(parent);
  else if (currentPath) navigate('');
}

function updateNavButtons() {
  document.getElementById('btn-back').disabled    = history.index <= 0;
  document.getElementById('btn-forward').disabled = history.index >= history.stack.length - 1;
  document.getElementById('btn-up').disabled      = !currentPath;
}

// ── Breadcrumbs ────────────────────────────────────────────────────────────────
function renderBreadcrumbs(crumbs) {
  const el = document.getElementById('breadcrumbs');
  el.innerHTML = crumbs.map((c, i) => {
    const sep = i > 0 ? `<span class="crumb-sep">›</span>` : '';
    return `${sep}<span class="crumb" data-path="${esc(c.path)}">${esc(c.name)}</span>`;
  }).join('');
  el.querySelectorAll('.crumb').forEach(c => {
    c.addEventListener('click', () => navigate(c.dataset.path));
  });
}

// ── Address bar ────────────────────────────────────────────────────────────────
function initAddressBar() {
  const bar   = document.getElementById('address-bar');
  const input = document.getElementById('address-input');
  const crumbs= document.getElementById('breadcrumbs');

  bar.addEventListener('click', e => {
    if (e.target.classList.contains('crumb')) return;
    crumbs.classList.add('hidden');
    input.classList.remove('hidden');
    input.value = currentPath;
    input.focus();
    input.select();
  });

  input.addEventListener('blur', () => {
    input.classList.add('hidden');
    crumbs.classList.remove('hidden');
  });

  input.addEventListener('keydown', e => {
    if (e.key === 'Enter') {
      const p = input.value.trim();
      input.blur();
      navigate(p);
    }
    if (e.key === 'Escape') input.blur();
  });
}

// ── Sidebar drives ─────────────────────────────────────────────────────────────
function renderSidebarDrives(drives) {
  const el = document.getElementById('sidebar-drives');
  if (!drives || !drives.length) { el.innerHTML = ''; return; }
  el.innerHTML = drives.map(d => {
    const pct = d.usedPercent;
    const barCls = pct > 90 ? 'full' : pct > 75 ? 'warn' : 'ok';
    const icon = getFileIcon(d.iconKey);
    const label = d.label ? `${d.label} (${d.name})` : `Local Disk (${d.name})`;
    return `<button class="drive-item${currentPath.startsWith(d.name) ? ' active' : ''}" data-path="${d.name}\\">
      <div class="drive-label">${icon} ${esc(label)}</div>
      <div class="drive-bar-wrap"><div class="drive-bar ${barCls}" style="width:${pct}%"></div></div>
      <div class="drive-free">${esc(d.freeDisplay)} free of ${esc(d.totalDisplay)}</div>
    </button>`;
  }).join('');
  el.querySelectorAll('.drive-item').forEach(btn => {
    btn.addEventListener('click', () => navigate(btn.dataset.path));
  });
}

// ── Render file rows ───────────────────────────────────────────────────────────
function buildRow(entry) {
  const icon   = getFileIcon(entry.iconKey);
  const isCut  = clipboard?.op === 'cut' && clipboard.paths.includes(entry.fullPath);
  const isSel  = selected.has(entry.fullPath);
  const cls    = [
    'file-row',
    isSel  ? 'selected'  : '',
    isCut  ? 'is-cut'    : '',
    entry.isHidden ? 'is-hidden' : '',
  ].filter(Boolean).join(' ');

  return `<div class="${cls}" data-path="${esc(entry.fullPath)}" data-isdir="${entry.isDirectory}">
    <div class="col-chk"><input type="checkbox" class="row-chk"${isSel?' checked':''}></div>
    <div class="col-icon">${icon}</div>
    <div class="col-name" title="${esc(entry.fullPath)}">${esc(entry.name)}</div>
    <div class="col-date">${fmtDate(entry.modified)}</div>
    <div class="col-type">${esc(entry.typeLabel)}</div>
    <div class="col-size">${entry.isDirectory ? '' : esc(entry.sizeDisplay)}</div>
  </div>`;
}

function renderFiles(entries) {
  const container = document.getElementById('file-container');
  if (!entries || !entries.length) {
    container.innerHTML = `<div class="empty-state">
      <div class="empty-icon">📂</div>
      <div class="empty-title">${searchMode ? 'No results found' : 'This folder is empty'}</div>
      <div class="empty-sub">${searchMode ? 'Try a different search term' : ''}</div>
    </div>`;
    return;
  }
  container.innerHTML = entries.map(buildRow).join('');
  bindRowEvents(container);
}

function bindRowEvents(container) {
  container.querySelectorAll('.file-row').forEach(row => {
    const path  = row.dataset.path;
    const isDir = row.dataset.isdir === 'true';

    // Checkbox
    row.querySelector('.row-chk')?.addEventListener('change', e => {
      e.stopPropagation();
      e.target.checked ? selected.add(path) : selected.delete(path);
      row.classList.toggle('selected', e.target.checked);
      updateSelectionTools();
    });

    // Click → select
    row.addEventListener('click', e => {
      if (e.target.classList.contains('row-chk') || e.target.classList.contains('col-name')) return;
      if (!e.ctrlKey && !e.shiftKey) {
        selected.clear();
        container.querySelectorAll('.file-row.selected').forEach(r => {
          r.classList.remove('selected');
          r.querySelector('.row-chk').checked = false;
        });
      }
      selected.add(path);
      row.classList.add('selected');
      row.querySelector('.row-chk').checked = true;
      updateSelectionTools();
    });

    // Name click → navigate or preview
    row.querySelector('.col-name')?.addEventListener('click', e => {
      e.stopPropagation();
      if (isDir) {
        navigate(path);
      } else {
        if (previewOpen) loadPreview(path, row.querySelector('.col-icon').textContent, row.querySelector('.col-name').textContent);
      }
    });

    // Double-click → open
    row.addEventListener('dblclick', e => {
      if (e.target.classList.contains('row-chk')) return;
      if (isDir) navigate(path);
      else loadPreview(path, row.querySelector('.col-icon').textContent, row.querySelector('.col-name').textContent);
    });
  });
}

// ── Selection tools ────────────────────────────────────────────────────────────
function updateSelectionTools() {
  const selTools = document.getElementById('selection-tools');
  const genTools = document.getElementById('general-tools');
  const cnt      = document.getElementById('sel-count');
  const chkAll   = document.getElementById('chk-all');
  const entries  = listing?.entries || searchResults;

  if (selected.size > 0) {
    selTools.classList.remove('hidden');
    cnt.textContent = `${selected.size} selected`;
  } else {
    selTools.classList.add('hidden');
  }

  const allPaths = entries.map(e => e.fullPath);
  chkAll.checked       = allPaths.length > 0 && allPaths.every(p => selected.has(p));
  chkAll.indeterminate = selected.size > 0 && !chkAll.checked;

  // Rename only enabled for single selection
  document.getElementById('btn-rename-sel').disabled = selected.size !== 1;

  // Preview: update if open
  if (previewOpen && selected.size === 1) {
    const path = [...selected][0];
    const entry = entries.find(e => e.fullPath === path);
    if (entry && !entry.isDirectory)
      loadPreview(path, getFileIcon(entry.iconKey), entry.name);
  }
}

// ── Main load ──────────────────────────────────────────────────────────────────
async function load() {
  document.getElementById('file-container').innerHTML =
    `<div class="loading-state"><div class="spinner-large"></div><p>Loading…</p></div>`;
  setStatus('Loading…');

  try {
    const [sortF, sortD] = sortField === 'name' ? ['name', sortDir]
      : sortField === 'modified' ? ['modified', sortDir]
      : sortField === 'size' ? ['size', sortDir]
      : ['type', sortDir];

    listing = await api.list(currentPath, sortF, sortD, showHidden);
    userProfile = listing.drives?.[0]?.name ? '' : userProfile;

    // Sidebar drives (always refresh)
    renderSidebarDrives(listing.drives);

    // Breadcrumbs
    renderBreadcrumbs(listing.breadcrumbs);

    // Sidebar active state
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
    if (!currentPath) document.getElementById('nav-thispc')?.classList.add('active');

    // Stats
    document.getElementById('stat-files').textContent   = listing.fileCount;
    document.getElementById('stat-folders').textContent = listing.folderCount;

    renderFiles(listing.entries);
    updateNavButtons();
    updateSelectionTools();

    const total = listing.fileCount + listing.folderCount;
    setStatus(
      `${total} item${total!==1?'s':''}${selected.size ? ` · ${selected.size} selected` : ''}`,
      listing.totalSize > 0 ? `${formatSizeClient(listing.totalSize)}` : ''
    );
  } catch (e) {
    document.getElementById('file-container').innerHTML =
      `<div class="error-state"><div class="empty-icon">⚠️</div><div class="empty-title">Cannot load folder</div><div class="empty-sub">${e.message}</div></div>`;
    setStatus('Error loading folder');
  }
}

function formatSizeClient(bytes) {
  if (bytes < 1024)          return `${bytes} B`;
  if (bytes < 1024*1024)     return `${(bytes/1024).toFixed(1)} KB`;
  if (bytes < 1024**3)       return `${(bytes/1024**2).toFixed(1)} MB`;
  return `${(bytes/1024**3).toFixed(2)} GB`;
}

// ── Search ─────────────────────────────────────────────────────────────────────
let searchDebounce = null;
async function doSearch(q) {
  if (!q) { searchMode = false; load(); return; }
  searchMode = true;
  document.getElementById('file-container').innerHTML =
    `<div class="loading-state"><div class="spinner-large"></div><p>Searching…</p></div>`;
  try {
    searchResults = await api.search(currentPath, q);
    renderFiles(searchResults);
    setStatus(`${searchResults.length} result${searchResults.length!==1?'s':''} for "${q}"`);
  } catch (e) { setStatus('Search failed: ' + e.message); }
}

// ── File operations ────────────────────────────────────────────────────────────
async function doDelete(permanent = false) {
  const paths = [...selected];
  if (!paths.length) return;
  const label = permanent ? 'Permanently delete' : 'Move to Recycle Bin';
  const ok = await confirm(
    `${label} ${paths.length} item${paths.length!==1?'s':''}?`,
    permanent
      ? 'This cannot be undone.'
      : 'Items will be moved to the Recycle Bin.',
    permanent ? '🗑 Delete' : '🗑 Move to Bin',
    permanent ? '💀' : '🗑'
  );
  if (!ok) return;
  setStatus('Deleting…');
  const res = await api.delete(paths, permanent);
  toast(res.warning || `Deleted ${paths.length} item${paths.length!==1?'s':''}`, res.warning ? 'warn' : 'warn');
  selected.clear();
  load();
}

async function doRename() {
  if (selected.size !== 1) return;
  const path    = [...selected][0];
  const oldName = path.split('\\').pop() || path.split('/').pop();
  const newName = await promptDialog('rename-overlay','rename-input','rename-ok','rename-cancel', oldName);
  if (!newName || newName === oldName) return;
  const res = await api.rename(path, newName);
  if (res.error) { toast(res.error, 'error'); return; }
  toast(`Renamed to "${newName}"`, 'success');
  selected.clear();
  load();
}

async function doNewFolder() {
  const name = await promptDialog('newfolder-overlay','newfolder-input','newfolder-ok','newfolder-cancel', 'New Folder');
  if (!name) return;
  const res = await api.newFolder(currentPath, name);
  if (res.error) { toast(res.error, 'error'); return; }
  toast(res.message, 'success');
  load();
}

async function doCopy() {
  if (!selected.size) return;
  clipboard = { paths: [...selected], op: 'copy' };
  toast(`Copied ${clipboard.paths.length} item${clipboard.paths.length!==1?'s':''}`, 'info');
  reapplyCutStyle();
}

async function doCut() {
  if (!selected.size) return;
  clipboard = { paths: [...selected], op: 'cut' };
  toast(`Cut ${clipboard.paths.length} item${clipboard.paths.length!==1?'s':''}`, 'info');
  reapplyCutStyle();
}

function reapplyCutStyle() {
  document.querySelectorAll('.file-row').forEach(row => {
    row.classList.toggle('is-cut', clipboard?.op === 'cut' && clipboard.paths.includes(row.dataset.path));
  });
}

async function doPaste() {
  if (!clipboard || !currentPath) return;
  setStatus(`${clipboard.op === 'copy' ? 'Copying' : 'Moving'} ${clipboard.paths.length} item${clipboard.paths.length!==1?'s':''}…`);
  let res;
  if (clipboard.op === 'copy')
    res = await api.copy(clipboard.paths, currentPath);
  else
    res = await api.move(clipboard.paths, currentPath);

  if (clipboard.op === 'cut') clipboard = null;
  const count = res.copied ?? res.moved ?? 0;
  toast(`${clipboard?.op === 'copy' ? 'Copied' : 'Moved'} ${count} item${count!==1?'s':''}${res.failed ? ` (${res.failed} failed)` : ''}`,
    res.failed ? 'warn' : 'success');
  load();
}

async function doProperties() {
  const path = [...selected][0];
  if (!path) return;
  const props = await api.properties(path);
  if (!props) { toast('Could not load properties', 'error'); return; }

  const entry = (listing?.entries || searchResults).find(e => e.fullPath === path);
  document.getElementById('props-icon').textContent = entry ? getFileIcon(entry.iconKey) : '📄';
  document.getElementById('props-name').textContent = props.name;

  const rows = [
    ['Type',     props.typeLabel],
    ['Location', props.fullPath.replace(props.name, '').replace(/\\$/, '') || props.fullPath],
    ['Size',     `${props.sizeDisplay} (${props.sizeBytes.toLocaleString()} bytes)`],
    props.itemCount !== undefined && props.itemCount !== null ? ['Contains', `${props.itemCount} file${props.itemCount!==1?'s':''}` ] : null,
    ['Created',  fmtDate(props.created)],
    ['Modified', fmtDate(props.modified)],
    ['Accessed', fmtDate(props.accessed)],
    props.attributes ? ['Attributes', props.attributes] : null,
  ].filter(Boolean);

  document.getElementById('props-grid').innerHTML = rows.map(([k,v]) =>
    `<div class="pk">${esc(k)}</div><div class="pv">${esc(v)}</div>`
  ).join('');
  document.getElementById('props-overlay').classList.remove('hidden');
}

// ── Preview panel ──────────────────────────────────────────────────────────────
async function loadPreview(path, icon, name) {
  const content = document.getElementById('preview-content');
  document.getElementById('preview-title').textContent = 'Preview';
  content.innerHTML = `<div class="loading-state" style="min-height:120px"><div class="spinner-large"></div></div>`;

  try {
    const r   = await fetch(api.previewUrl(path));
    const res = await r.json();

    if (res.type === 'image') {
      content.innerHTML = `
        <img src="${res.content}" alt="${esc(name)}" style="display:block;margin:0 auto"/>
        <div class="preview-fname" style="margin-top:8px">${esc(name)}</div>`;
    } else if (res.type === 'text') {
      const highlighted = esc(res.content);
      content.innerHTML = `
        <div class="preview-fname">${esc(name)}</div>
        <pre style="margin-top:8px">${highlighted}</pre>`;
    } else {
      content.innerHTML = `
        <span class="preview-icon">${icon}</span>
        <div class="preview-fname">${esc(name)}</div>
        <div class="preview-fmeta">No preview available</div>
        <div style="margin-top:14px;text-align:center">
          <a href="${api.downloadUrl(path)}" download="${esc(name)}" class="btn btn-sm btn-primary">⬇ Download</a>
        </div>`;
    }
  } catch {
    content.innerHTML = `<span class="preview-icon">${icon}</span><div class="preview-fname">${esc(name)}</div><div class="preview-fmeta">Preview failed</div>`;
  }
}

// ── Select All ────────────────────────────────────────────────────────────────
document.getElementById('chk-all').addEventListener('change', e => {
  const entries = listing?.entries || searchResults;
  entries.forEach(entry => {
    const row = document.querySelector(`[data-path="${CSS.escape(entry.fullPath)}"]`);
    if (e.target.checked) {
      selected.add(entry.fullPath);
      row?.classList.add('selected');
      const chk = row?.querySelector('.row-chk');
      if (chk) chk.checked = true;
    } else {
      selected.delete(entry.fullPath);
      row?.classList.remove('selected');
      const chk = row?.querySelector('.row-chk');
      if (chk) chk.checked = false;
    }
  });
  updateSelectionTools();
});

// ── View mode ─────────────────────────────────────────────────────────────────
function setView(mode) {
  viewMode = mode;
  const container = document.getElementById('file-container');
  container.className = `file-container ${mode}-view`;
  document.getElementById('list-header').style.display = mode === 'list' ? '' : 'none';
  ['list','grid','compact'].forEach(m => {
    document.getElementById(`view-${m}`).classList.toggle('active', m === mode);
  });
  if (listing) renderFiles(searchMode ? searchResults : listing.entries);
}

// ── Sort ──────────────────────────────────────────────────────────────────────
document.getElementById('sort-select').addEventListener('change', e => {
  const [f, d] = e.target.value.split('-');
  sortField = f; sortDir = d;
  if (!searchMode) load();
});

document.querySelectorAll('.sortable').forEach(el => {
  el.addEventListener('click', () => {
    const col = el.dataset.col;
    if (sortField === col) sortDir = sortDir === 'asc' ? 'desc' : 'asc';
    else { sortField = col; sortDir = 'asc'; }
    const sortMap = {name:'name', modified:'modified', type:'type', size:'size'};
    const sel = document.getElementById('sort-select');
    sel.value = `${sortMap[col] || 'name'}-${sortDir}`;
    if (!searchMode) load();
  });
});

// ── Keyboard shortcuts ────────────────────────────────────────────────────────
document.addEventListener('keydown', e => {
  // Don't intercept when typing in inputs
  if (['INPUT','TEXTAREA'].includes(e.target.tagName)) return;

  if (e.key === 'F2') { e.preventDefault(); doRename(); }
  if (e.key === 'Delete' && !e.shiftKey) { e.preventDefault(); doDelete(false); }
  if (e.key === 'Delete' && e.shiftKey)  { e.preventDefault(); doDelete(true); }
  if (e.key === 'F5')  { e.preventDefault(); load(); }
  if (e.altKey && e.key === 'ArrowLeft')  { e.preventDefault(); goBack(); }
  if (e.altKey && e.key === 'ArrowRight') { e.preventDefault(); goForward(); }
  if (e.altKey && e.key === 'ArrowUp')    { e.preventDefault(); goUp(); }
  if (e.altKey && e.key === 'Enter')      { e.preventDefault(); doProperties(); }
  if (e.ctrlKey && e.key === 'c') { e.preventDefault(); doCopy(); }
  if (e.ctrlKey && e.key === 'x') { e.preventDefault(); doCut(); }
  if (e.ctrlKey && e.key === 'v') { e.preventDefault(); doPaste(); }
  if (e.ctrlKey && e.key === 'a') {
    e.preventDefault();
    const entries = listing?.entries || searchResults;
    entries.forEach(entry => {
      selected.add(entry.fullPath);
      const row = document.querySelector(`[data-path="${CSS.escape(entry.fullPath)}"]`);
      row?.classList.add('selected');
      const chk = row?.querySelector('.row-chk');
      if (chk) chk.checked = true;
    });
    updateSelectionTools();
  }
  if (e.key === 'Escape') {
    ['confirm-overlay','rename-overlay','newfolder-overlay','props-overlay'].forEach(id => {
      document.getElementById(id).classList.add('hidden');
    });
    selected.clear();
    document.querySelectorAll('.file-row.selected').forEach(r => {
      r.classList.remove('selected');
      const c = r.querySelector('.row-chk');
      if (c) c.checked = false;
    });
    updateSelectionTools();
  }
});

// ── Wire up all buttons ────────────────────────────────────────────────────────
document.getElementById('btn-back').addEventListener('click',    goBack);
document.getElementById('btn-forward').addEventListener('click', goForward);
document.getElementById('btn-up').addEventListener('click',      goUp);
document.getElementById('btn-refresh').addEventListener('click', () => load());

document.getElementById('btn-copy-sel').addEventListener('click',   doCopy);
document.getElementById('btn-cut-sel').addEventListener('click',    doCut);
document.getElementById('btn-paste').addEventListener('click',      doPaste);
document.getElementById('btn-rename-sel').addEventListener('click', doRename);
document.getElementById('btn-delete-sel').addEventListener('click', () => doDelete(false));
document.getElementById('btn-props-sel').addEventListener('click',  doProperties);
document.getElementById('btn-deselect').addEventListener('click', () => {
  selected.clear();
  document.querySelectorAll('.file-row.selected').forEach(r => {
    r.classList.remove('selected');
    const c = r.querySelector('.row-chk');
    if (c) c.checked = false;
  });
  updateSelectionTools();
});

document.getElementById('btn-newfolder').addEventListener('click', doNewFolder);

document.getElementById('chk-hidden').addEventListener('change', e => {
  showHidden = e.target.checked;
  load();
});

// Preview toggle
document.getElementById('btn-preview-toggle').addEventListener('click', () => {
  previewOpen = !previewOpen;
  const panel = document.getElementById('preview-panel');
  panel.classList.toggle('hidden', !previewOpen);
  document.getElementById('btn-preview-toggle').style.color = previewOpen ? 'var(--accent)' : '';
});
document.getElementById('preview-close').addEventListener('click', () => {
  previewOpen = false;
  document.getElementById('preview-panel').classList.add('hidden');
  document.getElementById('btn-preview-toggle').style.color = '';
});

// View buttons
document.getElementById('view-list').addEventListener('click',    () => setView('list'));
document.getElementById('view-grid').addEventListener('click',    () => setView('grid'));
document.getElementById('view-compact').addEventListener('click', () => setView('compact'));

// Properties modal close
document.getElementById('props-close').addEventListener('click', () => {
  document.getElementById('props-overlay').classList.add('hidden');
});

// Search
const si = document.getElementById('search-input');
const sc = document.getElementById('search-clear');
si.addEventListener('input', () => {
  const q = si.value.trim();
  sc.classList.toggle('hidden', !q);
  clearTimeout(searchDebounce);
  searchDebounce = setTimeout(() => doSearch(q), 350);
});
sc.addEventListener('click', () => {
  si.value = ''; sc.classList.add('hidden');
  si.focus(); doSearch('');
});

// Sidebar quick access
document.querySelectorAll('[data-path-special]').forEach(btn => {
  btn.addEventListener('click', async () => {
    // Ask server for special path via listing a known location
    const sp = btn.dataset.pathSpecial;
    const specialPaths = {
      desktop:   'DESKTOP',
      documents: 'DOCUMENTS',
      downloads: 'DOWNLOADS',
      pictures:  'PICTURES',
      music:     'MUSIC',
      videos:    'VIDEOS',
    };
    // Use environment variable path via a list call with a special sentinel
    // We'll just navigate to a common path and let the server handle it
    const homeDrive = (listing?.drives?.[0]?.name || 'C:') + '\\';
    const userPaths = {
      desktop:   `${homeDrive}Users`,
      documents: `${homeDrive}Users`,
      downloads: `${homeDrive}Users`,
      pictures:  `${homeDrive}Users`,
      music:     `${homeDrive}Users`,
      videos:    `${homeDrive}Users`,
    };
    // Try %USERPROFILE% paths via a dedicated endpoint
    try {
      const r = await fetch(`/api/explorer/list?path=special:${sp}&sort=name&dir=asc&hidden=true`);
      const data = await r.json();
      if (data.path && data.path !== `special:${sp}`) {
        navigate(data.path);
        return;
      }
    } catch {}
    // Fallback: navigate to common location
    navigate(userPaths[sp] || '');
  });
});

// ── Drag-over for paste ────────────────────────────────────────────────────────
const fileContainer = document.getElementById('file-container');
fileContainer.addEventListener('dragover',  e => { e.preventDefault(); fileContainer.classList.add('drag-over'); });
fileContainer.addEventListener('dragleave', () => fileContainer.classList.remove('drag-over'));
fileContainer.addEventListener('drop',      e => { e.preventDefault(); fileContainer.classList.remove('drag-over'); });

// ── Init ──────────────────────────────────────────────────────────────────────
initAddressBar();
navigate('', false);
