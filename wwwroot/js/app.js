'use strict';
// ── Type icons & colors ────────────────────────────────────────────────────────
const TYPE_ICON = {
  folder:  '📁', image:  '🖼', video: '🎬', audio:  '🎵',
  pdf:     '📕', word:   '📘', excel: '📗', ppt:    '📙',
  archive: '📦', code:   '💻', binary:'⚙',  text:   '📄', file: '📄',
};
const TYPE_COLOR = {
  folder:'--c-folder', image:'--c-image', video:'--c-video', audio:'--c-audio',
  pdf:'--c-pdf', word:'--c-word', excel:'--c-excel', ppt:'--c-ppt',
  archive:'--c-archive', code:'--c-code', binary:'--c-binary',
  text:'--c-text', file:'--c-file',
};
// Map sidebar filter keys → typeIcon values
const FILTER_MAP = {
  all:'', folder:'folder', image:'image', video:'video', audio:'audio',
  document:['pdf','word','excel','ppt','text'],
  code:'code', archive:'archive',
};

// ── State ─────────────────────────────────────────────────────────────────────
let allItems   = [];
let viewMode   = 'list';  // 'list' | 'grid'
let sortVal    = 'date-desc';
let filterType = 'all';
let filterDrive= '';
let searchQ    = '';
let selected   = new Set();
let stats      = null;

// ── API ───────────────────────────────────────────────────────────────────────
const api = {
  async getItems()          { const r = await fetch('/api/recyclebin'); return r.json(); },
  async getStats()          { const r = await fetch('/api/recyclebin/stats'); return r.json(); },
  async restore(id)         { const r = await fetch(`/api/recyclebin/${id}/restore`, {method:'POST'}); return r.json(); },
  async deletePerm(id)      { const r = await fetch(`/api/recyclebin/${id}`, {method:'DELETE'}); return r.json(); },
  async bulkRestore(ids)    { const r = await fetch('/api/recyclebin/bulk-restore', {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({ids})}); return r.json(); },
  async bulkDelete(ids)     { const r = await fetch('/api/recyclebin/bulk-delete',  {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({ids})}); return r.json(); },
  async emptyAll()          { const r = await fetch('/api/recyclebin', {method:'DELETE'}); return r.json(); },
};

// ── Toast ─────────────────────────────────────────────────────────────────────
function toast(msg, type='success', duration=3000) {
  const icons = {success:'✅', error:'❌', warn:'⚠️', info:'ℹ️'};
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.innerHTML = `<span class="toast-icon">${icons[type]||'ℹ️'}</span><span class="toast-msg">${msg}</span>`;
  document.getElementById('toasts').appendChild(el);
  setTimeout(() => {
    el.classList.add('out');
    setTimeout(() => el.remove(), 320);
  }, duration);
}

// ── Confirm dialog ─────────────────────────────────────────────────────────────
function confirm(title, body, okLabel='Confirm', icon='⚠️') {
  return new Promise(resolve => {
    document.getElementById('confirm-title').textContent = title;
    document.getElementById('confirm-body').textContent  = body;
    document.getElementById('confirm-icon').textContent  = icon;
    document.getElementById('confirm-ok').textContent    = okLabel;
    document.getElementById('confirm-overlay').classList.remove('hidden');
    const ok  = document.getElementById('confirm-ok');
    const can = document.getElementById('confirm-cancel');
    const cleanup = (v) => {
      document.getElementById('confirm-overlay').classList.add('hidden');
      ok.replaceWith(ok.cloneNode(true));
      can.replaceWith(can.cloneNode(true));
      resolve(v);
    };
    document.getElementById('confirm-ok').addEventListener('click',     () => cleanup(true));
    document.getElementById('confirm-cancel').addEventListener('click', () => cleanup(false));
  });
}

// ── Status ─────────────────────────────────────────────────────────────────────
function setStatus(msg, right='') {
  document.getElementById('status-text').textContent  = msg;
  document.getElementById('status-right').textContent = right;
}

// ── Stats sidebar ──────────────────────────────────────────────────────────────
function updateStats(s) {
  stats = s;
  document.getElementById('stat-items').textContent = s.totalItems.toLocaleString();
  document.getElementById('stat-size').textContent  = s.totalDisplay;
  // Fill bar: represent relative to e.g. 1GB
  const pct = Math.min((s.totalBytes / (1024**3)) * 100, 100);
  document.getElementById('stat-fill').style.width = pct + '%';

  // Drive buttons
  const df = document.getElementById('drive-filter');
  const existing = new Set([...df.querySelectorAll('.drive-btn:not([data-drive=""])')].map(b=>b.dataset.drive));
  const fresh    = new Set(s.byDrive.map(d=>d.drive));

  // Add new drives
  s.byDrive.forEach(d => {
    if (!existing.has(d.drive)) {
      const btn = document.createElement('button');
      btn.className = `drive-btn${filterDrive===d.drive?' active':''}`;
      btn.dataset.drive = d.drive;
      btn.textContent = d.drive;
      btn.title = `${d.items} items · ${d.display}`;
      btn.addEventListener('click', () => {
        filterDrive = filterDrive === d.drive ? '' : d.drive;
        df.querySelectorAll('.drive-btn').forEach(b => b.classList.toggle('active', b.dataset.drive === filterDrive));
        renderList();
      });
      df.appendChild(btn);
    }
  });
  // Remove gone drives
  existing.forEach(drv => { if (!fresh.has(drv)) df.querySelector(`[data-drive="${drv}"]`)?.remove(); });
}

// ── Date format ────────────────────────────────────────────────────────────────
function fmtDate(iso) {
  const d = new Date(iso);
  const now = new Date();
  const diff = (now - d) / 1000;
  if (diff < 60)    return 'Just now';
  if (diff < 3600)  return `${Math.floor(diff/60)}m ago`;
  if (diff < 86400) return `${Math.floor(diff/3600)}h ago`;
  if (diff < 86400*7) return `${Math.floor(diff/86400)}d ago`;
  return d.toLocaleDateString(undefined, {month:'short', day:'numeric', year:'numeric'});
}

// ── Filter & sort items ────────────────────────────────────────────────────────
function getVisible() {
  let items = allItems.slice();

  // type filter
  const ft = FILTER_MAP[filterType] || '';
  if (ft) {
    const types = Array.isArray(ft) ? ft : [ft];
    items = items.filter(i => types.includes(i.typeIcon));
  }
  // drive filter
  if (filterDrive) items = items.filter(i => i.drive === filterDrive);
  // search
  if (searchQ) {
    const ql = searchQ.toLowerCase();
    items = items.filter(i =>
      i.name.toLowerCase().includes(ql) || i.originalPath.toLowerCase().includes(ql));
  }
  return items;
}

// ── Render list ────────────────────────────────────────────────────────────────
function renderList() {
  const container = document.getElementById('item-container');
  const items     = getVisible();

  // Update header visibility
  document.getElementById('list-header').style.display =
    viewMode === 'list' ? '' : 'none';

  if (!items.length) {
    container.innerHTML = `
      <div class="empty-state">
        <div class="empty-icon">🗑</div>
        <div class="empty-title">${allItems.length ? 'No matching items' : 'Recycle Bin is empty'}</div>
        <div class="empty-sub">${allItems.length ? 'Try a different filter or search term' : 'Deleted files will appear here'}</div>
      </div>`;
    return;
  }

  const rows = items.map(item => buildRow(item)).join('');
  container.innerHTML = rows;

  // Bind row events
  container.querySelectorAll('.item-row').forEach(row => {
    const id = row.dataset.id;

    // Checkbox
    row.querySelector('.row-chk')?.addEventListener('change', e => {
      e.target.checked ? selected.add(id) : selected.delete(id);
      row.classList.toggle('selected', e.target.checked);
      updateBulkBar();
    });

    // Row click → select
    row.addEventListener('click', e => {
      if (e.target.closest('.action-btn, .row-chk')) return;
      const chk = row.querySelector('.row-chk');
      const checked = !chk.checked;
      chk.checked = checked;
      checked ? selected.add(id) : selected.delete(id);
      row.classList.toggle('selected', checked);
      updateBulkBar();
    });

    // Restore btn
    row.querySelector('.btn-restore')?.addEventListener('click', async e => {
      e.stopPropagation();
      await doRestore(id);
    });

    // Delete btn
    row.querySelector('.btn-delete')?.addEventListener('click', async e => {
      e.stopPropagation();
      await doDelete(id);
    });
  });

  // Reapply selected state
  selected.forEach(id => {
    const row = container.querySelector(`[data-id="${id}"]`);
    if (row) { row.classList.add('selected'); row.querySelector('.row-chk').checked = true; }
  });

  setStatus(`${items.length} item${items.length!==1?'s':''} shown`,
    allItems.length !== items.length ? `${allItems.length} total` : '');
}

function buildRow(item) {
  const icon  = TYPE_ICON[item.typeIcon] || '📄';
  const color = `var(${TYPE_COLOR[item.typeIcon] || '--c-file'})`;
  const chked = selected.has(item.id) ? 'checked' : '';

  if (viewMode === 'list') {
    return `
    <div class="item-row" data-id="${item.id}">
      <div class="col-check"><input type="checkbox" class="row-chk" ${chked}/></div>
      <div class="col-icon" style="color:${color}">${icon}</div>
      <div class="col-name" title="${esc(item.name)}">${esc(item.name)}</div>
      <div class="col-orig" title="${esc(item.originalPath)}">${esc(shortenPath(item.originalPath))}</div>
      <div class="col-date">${fmtDate(item.deletedAt)}</div>
      <div class="col-size">${item.sizeDisplay}</div>
      <div class="col-type">
        <span class="type-badge" style="color:${color};border:1px solid ${color}20">${esc(item.fileType)}</span>
      </div>
      <div class="col-actions">
        <button class="action-btn restore btn-restore" title="Restore to original location">↩</button>
        <button class="action-btn delete  btn-delete"  title="Permanently delete">🗑</button>
      </div>
    </div>`;
  } else {
    return `
    <div class="item-row" data-id="${item.id}" title="${esc(item.name)}&#10;${esc(shortenPath(item.originalPath))}&#10;${item.sizeDisplay}">
      <div class="col-check"><input type="checkbox" class="row-chk" ${chked}/></div>
      <div class="col-icon" style="color:${color}">${icon}</div>
      <div class="col-name">${esc(item.name)}</div>
      <div class="col-actions">
        <button class="action-btn restore btn-restore" title="Restore">↩</button>
        <button class="action-btn delete  btn-delete"  title="Delete">🗑</button>
      </div>
    </div>`;
  }
}

function esc(s) {
  return String(s)
    .replace(/&/g,'&amp;').replace(/</g,'&lt;')
    .replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
function shortenPath(p) {
  if (!p) return '';
  const parts = p.replace(/\\/g,'/').split('/');
  if (parts.length <= 4) return p;
  return parts[0] + '/…/' + parts.slice(-2).join('/');
}

// ── Bulk bar ──────────────────────────────────────────────────────────────────
function updateBulkBar() {
  const bar = document.getElementById('bulk-bar');
  const cnt = document.getElementById('bulk-count');
  if (selected.size > 0) {
    bar.classList.remove('hidden');
    cnt.textContent = `${selected.size} selected`;
  } else {
    bar.classList.add('hidden');
  }
  // Sync select-all checkbox
  const vis   = getVisible();
  const allChk= document.getElementById('chk-all');
  allChk.checked = vis.length > 0 && vis.every(i => selected.has(i.id));
  allChk.indeterminate = selected.size > 0 && !allChk.checked;
}

// ── Actions ───────────────────────────────────────────────────────────────────
async function doRestore(id) {
  const item = allItems.find(i => i.id === id);
  setStatus(`Restoring ${item?.name}…`);
  try {
    const res = await api.restore(id);
    if (res.error) { toast(res.error, 'error'); return; }
    toast(`✅ Restored: ${item?.name}`, 'success');
    removeRowAnimated(id);
    selected.delete(id);
    updateBulkBar();
    await refreshData();
  } catch(e) { toast('Restore failed: ' + e.message, 'error'); }
}

async function doDelete(id) {
  const item = allItems.find(i => i.id === id);
  const ok   = await confirm(
    'Permanently Delete?',
    `"${item?.name}" will be permanently deleted and cannot be recovered.`,
    '🗑 Delete', '⚠️');
  if (!ok) return;
  setStatus(`Deleting ${item?.name}…`);
  try {
    const res = await api.deletePerm(id);
    if (res.error) { toast(res.error, 'error'); return; }
    toast(`Deleted: ${item?.name}`, 'warn');
    removeRowAnimated(id);
    selected.delete(id);
    updateBulkBar();
    await refreshData();
  } catch(e) { toast('Delete failed: ' + e.message, 'error'); }
}

function removeRowAnimated(id) {
  const row = document.querySelector(`[data-id="${id}"]`);
  if (!row) return;
  row.classList.add('removing');
  setTimeout(() => row.remove(), 270);
  allItems = allItems.filter(i => i.id !== id);
}

// ── Load / refresh ─────────────────────────────────────────────────────────────
async function refreshData(showSpinner=false) {
  if (showSpinner) {
    document.getElementById('item-container').innerHTML =
      `<div class="loading-state"><div class="spinner-large"></div><p>Reading Recycle Bin…</p></div>`;
  }
  try {
    const [items, s] = await Promise.all([api.getItems(), api.getStats()]);
    allItems = items;
    updateStats(s);
    renderList();
  } catch(e) {
    document.getElementById('item-container').innerHTML =
      `<div class="error-state">
        <div class="empty-icon">⚠️</div>
        <div class="empty-title">Could not read Recycle Bin</div>
        <div class="empty-sub">${e.message}</div>
      </div>`;
    setStatus('Error loading items');
  }
}

// ── Init ──────────────────────────────────────────────────────────────────────
(async () => {
  // View toggle
  document.getElementById('view-list').addEventListener('click', () => {
    viewMode = 'list';
    document.getElementById('item-container').className = 'item-container list-view';
    document.getElementById('view-list').classList.add('active');
    document.getElementById('view-grid').classList.remove('active');
    document.getElementById('list-header').style.display = '';
    renderList();
  });
  document.getElementById('view-grid').addEventListener('click', () => {
    viewMode = 'grid';
    document.getElementById('item-container').className = 'item-container grid-view';
    document.getElementById('view-grid').classList.add('active');
    document.getElementById('view-list').classList.remove('active');
    document.getElementById('list-header').style.display = 'none';
    renderList();
  });

  // Sort
  document.getElementById('sort-select').addEventListener('change', e => {
    sortVal = e.target.value;
    const [col, dir] = sortVal.split('-');
    allItems.sort((a,b) => {
      let va, vb;
      if (col==='name') { va=a.name.toLowerCase(); vb=b.name.toLowerCase(); }
      else if (col==='size') { va=a.sizeBytes; vb=b.sizeBytes; }
      else if (col==='type') { va=a.fileType; vb=b.fileType; }
      else { va=new Date(a.deletedAt); vb=new Date(b.deletedAt); }
      const cmp = va < vb ? -1 : va > vb ? 1 : 0;
      return dir==='asc' ? cmp : -cmp;
    });
    renderList();
  });

  // Search
  const si = document.getElementById('search-input');
  const sc = document.getElementById('search-clear');
  si.addEventListener('input', () => {
    searchQ = si.value.trim();
    sc.classList.toggle('hidden', !searchQ);
    renderList();
  });
  sc.addEventListener('click', () => {
    si.value = ''; searchQ = '';
    sc.classList.add('hidden');
    si.focus(); renderList();
  });

  // Sidebar nav
  document.querySelectorAll('.nav-item').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.nav-item').forEach(b=>b.classList.remove('active'));
      btn.classList.add('active');
      filterType = btn.dataset.filter;
      renderList();
    });
  });

  // Select all
  document.getElementById('chk-all').addEventListener('change', e => {
    const vis = getVisible();
    if (e.target.checked) vis.forEach(i => selected.add(i.id));
    else vis.forEach(i => selected.delete(i.id));
    renderList(); updateBulkBar();
  });

  // Bulk restore
  document.getElementById('btn-bulk-restore').addEventListener('click', async () => {
    if (!selected.size) return;
    const ids = [...selected];
    setStatus(`Restoring ${ids.length} items…`);
    try {
      const res = await api.bulkRestore(ids);
      toast(`Restored ${res.restored} item${res.restored!==1?'s':''}${res.failed?' · '+res.failed+' failed':''}`,
        res.failed ? 'warn' : 'success');
      ids.forEach(id => { allItems = allItems.filter(i=>i.id!==id); selected.delete(id); });
      updateBulkBar(); renderList(); await refreshData();
    } catch(e) { toast('Bulk restore failed', 'error'); }
  });

  // Bulk delete
  document.getElementById('btn-bulk-delete').addEventListener('click', async () => {
    if (!selected.size) return;
    const ids = [...selected];
    const ok  = await confirm(
      `Delete ${ids.length} items?`,
      `${ids.length} item${ids.length!==1?'s':''} will be permanently deleted and cannot be recovered.`,
      `🗑 Delete ${ids.length}`, '⚠️');
    if (!ok) return;
    setStatus(`Deleting ${ids.length} items…`);
    try {
      const res = await api.bulkDelete(ids);
      toast(`Deleted ${res.deleted} item${res.deleted!==1?'s':''}${res.failed?' · '+res.failed+' failed':''}`,
        res.failed ? 'warn' : 'warn');
      ids.forEach(id => { allItems = allItems.filter(i=>i.id!==id); selected.delete(id); });
      updateBulkBar(); renderList(); await refreshData();
    } catch(e) { toast('Bulk delete failed', 'error'); }
  });

  // Deselect
  document.getElementById('btn-deselect').addEventListener('click', () => {
    selected.clear(); updateBulkBar(); renderList();
  });

  // Refresh
  document.getElementById('btn-refresh').addEventListener('click', () => {
    selected.clear(); updateBulkBar();
    refreshData(false);
    toast('Refreshed', 'info', 1500);
  });

  // Empty all
  document.getElementById('btn-empty-all').addEventListener('click', async () => {
    if (!allItems.length) { toast('Recycle Bin is already empty', 'info'); return; }
    const ok = await confirm(
      'Empty Recycle Bin?',
      `All ${allItems.length} item${allItems.length!==1?'s':''} will be permanently deleted. This cannot be undone.`,
      '🗑 Empty All', '🗑');
    if (!ok) return;
    setStatus('Emptying Recycle Bin…');
    try {
      const res = await api.emptyAll();
      if (res.error) { toast(res.error, 'error'); return; }
      toast('Recycle Bin emptied', 'warn');
      allItems = []; selected.clear(); updateBulkBar();
      renderList(); await refreshData();
    } catch(e) { toast('Failed to empty bin: ' + e.message, 'error'); }
  });

  // Escape closes confirm
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') document.getElementById('confirm-overlay').classList.add('hidden');
  });

  // Initial load
  await refreshData(true);

  // Auto-refresh every 10s (lighter than audio mixer — file ops are slower)
  setInterval(() => refreshData(false), 10000);
})();
