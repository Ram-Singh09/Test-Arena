/* =====================================================================
   host-panel.js — the room creator's control panel
   ===================================================================== */

const HostPanel = (() => {
  let state = null;

  function openWithRoom(data) {
    state = {
      roomId: data.roomId ?? null,
      password: data.password,
      minutes: data.minutes,
      positiveMarks: data.positiveMarks,
      negativeMarks: data.negativeMarks,
      creatorName: data.creatorName,
      questionCount: data.questionCount,
      started: data.started ?? false,
    };
    render();
    showView("view-host-panel");
  }

  function render() {
    const idEl = document.getElementById("host-room-id");
    if (state.roomId === null || state.roomId === undefined || state.roomId === "") {
      idEl.innerHTML = `<input type="number" id="host-room-id-input" placeholder="Enter room ID" style="font-family:var(--font-mono);font-size:1rem;width:140px;">`;
      document.getElementById("host-room-id-input").addEventListener("input", (e) => {
        state.roomId = e.target.value ? parseInt(e.target.value, 10) : null;
      });
    } else {
      idEl.textContent = String(state.roomId);
    }

    document.getElementById("host-room-password").textContent = String(state.password);

    const summary = document.getElementById("host-room-summary");
    summary.innerHTML = `
      <div><dt>Duration</dt><dd>${state.minutes} min</dd></div>
      <div><dt>Questions</dt><dd>${state.questionCount}</dd></div>
      <div><dt>Marks (+)</dt><dd>${state.positiveMarks}</dd></div>
      <div><dt>Marks (−)</dt><dd>${state.negativeMarks}</dd></div>
      <div class="field-span-2"><dt>Host name</dt><dd>${escapeHtml(state.creatorName)}</dd></div>
    `;

    const banner = document.getElementById("host-status-banner");
    banner.textContent = state.started ? "Room is live — questions are locked in." : "Not started yet — you can still edit the questions.";
    banner.classList.toggle("started", state.started);

    document.getElementById("host-replace-test-btn").disabled = state.started;
    document.getElementById("host-start-room-btn").disabled = state.started;
  }

  function getState() { return state; }

  function wire()
  {
    document.getElementById("host-copy-btn").addEventListener("click", async () => {
      if (!state) return;
      const text = `Join my TestArena room:\nRoom ID: ${state.roomId ?? "(ask host)"}\nPassword: ${state.password}`;
      try {
        await navigator.clipboard.writeText(text);
        showToast("Invite copied to clipboard.", "success");
      } catch {
        showToast("Couldn't copy automatically — select and copy manually.", "error");
      }
    });

    document.getElementById("host-replace-test-btn").addEventListener("click", () => {
      if (!state || state.started) return;
      openReplaceTest({ roomId: state.roomId, password: state.password, name: state.creatorName });
    });

    document.getElementById("host-start-room-btn").addEventListener("click", async () => {
      if (!state) return;
      if (state.roomId === null || state.roomId === undefined) {
        showToast("Enter the room ID before starting.", "error");
        return;
      }
      const ok = await confirmAction({
        title: "Start this room?",
        message: "Once started, the test paper can't be changed and the timer begins for everyone who has joined.",
        confirmLabel: "Start room",
      });
      if (!ok) return;

      const btn = document.getElementById("host-start-room-btn");
      setButtonBusy(btn, true, "Starting…");
      try {
        await Api.startRoom({ roomId: state.roomId, password: state.password, name: state.creatorName });
        state.started = true;
        render();

        // Sync started status to the dashboard's saved live room
        const saved = Store.get("ta_hosted_room", {});
        Store.set("ta_hosted_room", { ...saved, started: true });

        showToast("Room started.", "success");
      } catch (err) {
        showToast(err.message || "Couldn't start the room — only the room's creator can start it.", "error");
      } finally {
        setButtonBusy(btn, false);
      }
    });

    document.getElementById("host-leaderboard-btn").addEventListener("click", () => {
      if (!state) return;
      if (state.roomId === null || state.roomId === undefined) {
        showToast("Enter the room ID first.", "error");
        return;
      }
      Leaderboard.open({ roomId: state.roomId, password: state.password, name: state.creatorName, viewerName: state.creatorName });
    });

    document.getElementById("host-take-test-btn")?.addEventListener("click", () => {
      if (!state || state.roomId === null) {
        showToast("Enter or create a room first.", "error");
        return;
      }
      enterWaitingRoom({
        roomId: state.roomId,
        password: state.password,
        name: state.creatorName
      });
    });
  }

  return { openWithRoom, getState, wire };
})();