'use strict';
(() => {
  const id = document.getElementById('BatchId')?.value, base = '/Platform/WorkingCalendarImports/api';
  const unwrap = x => x?.data ?? x?.Data ?? x;
  let batch, calendarVersion;
  const post = (url, body) => fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const load = async () => {
    batch = unwrap(await (await fetch(`${base}/${id}`, { credentials: 'same-origin' })).json());
    const calendar = unwrap(await (await fetch(`${base}/calendars/${batch.targetCalendarId}`, { credentials: 'same-origin' })).json());
    calendarVersion = calendar.version;
    document.getElementById('batchSummary').textContent = `${batch.batchCode} · ${batch.countryCode} ${batch.calendarYear} · ${batch.importStatus}`;
    const tbody = document.querySelector('#candidateTable tbody'); tbody.innerHTML = '';
    (batch.candidates || []).forEach(c => {
      const tr = document.createElement('tr');
      tr.innerHTML = `<td>${c.date}</td><td><span class="fw-medium text-heading">${c.mappedDayName}</span></td><td>${c.changeKind}</td><td class="cell-fit text-end pe-3"><select class="form-select form-select-sm w-auto d-inline-block decision" data-id="${c.candidateId}"><option value="undecided">undecided</option><option value="approved">approved</option><option value="rejected">rejected</option></select></td>`;
      tr.querySelector('select').value = c.decision; tbody.appendChild(tr);
    });
    document.getElementById('skeleton-loader')?.classList.add('d-none');
    document.querySelectorAll('.decision').forEach(el => el.addEventListener('change', async () => {
      const r = await post(`${base}/${id}/candidates/${el.dataset.id}/decision`, { decision: el.value, reason: null });
      if (!r.ok) { window.showToast?.(await r.text(), 'error'); await load(); } else await load();
    }));
  };
  document.getElementById('btnApply')?.addEventListener('click', async () => {
    const r = await post(`${base}/${id}/apply`, { expectedBatchVersion: batch.version, expectedCalendarVersion: calendarVersion });
    if (r.ok) location.reload(); else window.showToast?.(await r.text(), 'error');
  });
  document.getElementById('btnDiscard')?.addEventListener('click', async () => {
    const r = await post(`${base}/${id}/discard`, { expectedVersion: batch.version, reason: null });
    if (r.ok) location.href = '/Platform/WorkingCalendarImports'; else window.showToast?.(await r.text(), 'error');
  });
  load();
})();
