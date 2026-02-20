/* Recommendations page interactions:
 * - Workspace picker -> reloads with workspaceId
 * - "Create action" -> creates a planned action via RecommendationsController
 */

(function () {
  function getAntiForgeryToken() {
    var form = document.getElementById('cdRecsAnti');
    if (!form) return null;
    var input = form.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : null;
  }

  function setBusy(btn, busy) {
    if (!btn) return;
    if (busy) {
      btn.dataset.cdBusyText = btn.textContent;
      btn.disabled = true;
      btn.textContent = 'Creating…';
    } else {
      btn.disabled = false;
      btn.textContent = btn.dataset.cdBusyText || btn.textContent;
      delete btn.dataset.cdBusyText;
    }
  }

  // Workspace select
  var wsSel = document.getElementById('cdRecWorkspace');
  if (wsSel) {
    wsSel.addEventListener('change', function () {
      var v = (wsSel.value || '').trim();
      var url = new URL(window.location.href);
      if (v) url.searchParams.set('workspaceId', v);
      else url.searchParams.delete('workspaceId');
      window.location.href = url.toString();
    });
  }

  // Create action buttons
  document.addEventListener('click', async function (e) {
    var btn = e.target && e.target.closest ? e.target.closest('[data-cd-recs-create-action]') : null;
    if (!btn) return;

    e.preventDefault();

    var token = getAntiForgeryToken();
    if (!token) {
      alert('Security token missing. Refresh the page and try again.');
      return;
    }

    var title = btn.getAttribute('data-title') || '';
    var note = btn.getAttribute('data-note') || '';
    var workspaceId = btn.getAttribute('data-workspace-id') || '';

    setBusy(btn, true);

    try {
      var body = new URLSearchParams();
      body.set('title', title);
      body.set('note', note);
      body.set('__RequestVerificationToken', token);
      if (workspaceId) body.set('workspaceId', workspaceId);

      var resp = await fetch('/Recommendations/CreateActionFromRecommendation', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
          'RequestVerificationToken': token
        },
        body: body.toString()
      });

      if (!resp.ok) {
        var errText = await resp.text();
        throw new Error(errText || 'Request failed');
      }

      var json = await resp.json();
      if (!json || json.ok !== true) {
        throw new Error((json && json.error) || 'Could not create action');
      }

      // Redirect to the edit screen so the user can refine details.
      if (json.redirectUrl) {
        window.location.href = json.redirectUrl;
        return;
      }

      window.location.href = '/Actions';
    } catch (err) {
      console.error(err);
      alert('Could not create action. ' + (err && err.message ? err.message : ''));
    } finally {
      setBusy(btn, false);
    }
  });
})();
