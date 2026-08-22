/* =====================================================================
   leaderboard.js
   Test_Leaderboard.Names_and_scores is a flat List<string>, so we
   parse each entry defensively (e.g. "Name - 12", "Name: 12") rather
   than assuming a fixed separator. Order is left exactly as the
   server sent it, on the assumption it's already ranked.
   ===================================================================== */

const Leaderboard = (() => {
  let current = null; // { roomId, password, name, viewerName }

  function parseEntry(raw) {
    const match = String(raw).match(/^(.*?)[\s:,\-–]+(-?\d+(?:\.\d+)?)\s*$/);
    if (match) {
      return { name: match[1].trim(), score: match[2] };
    }
    return { name: String(raw).trim(), score: null };
  }

  async function open({ roomId, password, name, viewerName }) {
    current = { roomId, password, name, viewerName };
    document.getElementById("lb-room-id").textContent = String(roomId);
    showView("view-leaderboard");
    await refresh();
  }

  async function refresh() {
    if (!current) return;
    const listEl = document.getElementById("leaderboard-list");
    const emptyEl = document.getElementById("lb-empty");
    const refreshBtn = document.getElementById("lb-refresh-btn");
    setButtonBusy(refreshBtn, true, "Refreshing…");
    try {
      const data = await Api.getLeaderboard({
        roomId: current.roomId, 
        password: current.password, 
        name: current.name,
      });

      // Handle raw array from Results.Ok(Scores) or wrapped object fallback
      let entries = [];
      if (Array.isArray(data)) {
        entries = data;
      } else {
        entries = readField(data, "Names_and_scores", "namesAndScores", "scores") || [];
      }

      listEl.innerHTML = "";
      // Hide empty message if there are entries; show if 0 entries
      emptyEl.classList.toggle("hidden", entries.length > 0);

      entries.forEach((raw, idx) => {
        const { name, score } = parseEntry(raw);
        const li = document.createElement("li");
        const isYou = current.viewerName && name.toLowerCase() === current.viewerName.toLowerCase();
        li.innerHTML = `
          <span class="lb-rank">${idx + 1}</span>
          <span class="lb-name${isYou ? " is-you" : ""}"></span>
          <span class="lb-score"></span>
        `;
        li.querySelector(".lb-name").textContent = name;
        li.querySelector(".lb-score").textContent = score !== null ? score : "";
        listEl.appendChild(li);
      });
    } catch (err) {
      showToast(err.message || "Couldn't load the leaderboard.", "error");
    } finally {
      setButtonBusy(refreshBtn, false);
    }
  }

  return { open, refresh };
})();

function wireLeaderboard() {
  document.getElementById("lb-refresh-btn").addEventListener("click", () => Leaderboard.refresh());
  document.getElementById("lb-back-btn").addEventListener("click", () => {
    stopWaitingPoll();
    goToDashboard();
  });
}
