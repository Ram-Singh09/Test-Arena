/* =====================================================================
   join-room.js — join a room, wait for start, or enter test directly
   ===================================================================== */

const RoomSession = {
  get() { return Store.get(CONFIG.STORAGE_KEYS.ROOM_SESSION, null); },
  set(session) { Store.set(CONFIG.STORAGE_KEYS.ROOM_SESSION, session); },
  clear() { Store.remove(CONFIG.STORAGE_KEYS.ROOM_SESSION); },
};

function hasSubmitted(roomId) {
  const list = Store.get(CONFIG.STORAGE_KEYS.SUBMITTED_ROOMS, []);
  return list.includes(roomId);
}
function markSubmitted(roomId) {
  const list = Store.get(CONFIG.STORAGE_KEYS.SUBMITTED_ROOMS, []);
  if (!list.includes(roomId)) {
    list.push(roomId);
    Store.set(CONFIG.STORAGE_KEYS.SUBMITTED_ROOMS, list);
  }
}

let waitingPollTimer = null;

function stopWaitingPoll() {
  if (waitingPollTimer) { clearInterval(waitingPollTimer); waitingPollTimer = null; }
}

function enterWaitingRoom(session) {
  RoomSession.set(session);
  document.getElementById("waiting-room-id").textContent = String(session.roomId);
  document.getElementById("waiting-name").textContent = session.name;
  setFieldError("waiting-error", "");
  showView("view-waiting-room");

  // Check immediately if the room has already started
  checkRoomStarted(session, { silent: true });

  const autoToggle = document.getElementById("waiting-auto-toggle");
  stopWaitingPoll();
  if (autoToggle && autoToggle.checked) {
    waitingPollTimer = setInterval(() => checkRoomStarted(session, { silent: true }), 3000);
  }
}

async function checkRoomStarted(session, { silent = false } = {}) {
  setFieldError("waiting-error", "");
  try {
    const data = await Api.getQuestions({ roomId: session.roomId, password: session.password, name: session.name });
    stopWaitingPoll();
    if (hasSubmitted(session.roomId)) {
      showToast("You've already submitted this test — showing the leaderboard.", "info");
      Leaderboard.open({ roomId: session.roomId, password: session.password, name: session.name, viewerName: session.name });
      return;
    }
    const questions = readField(data, "Questions", "questions") || [];
    const options = readField(data, "Options", "options") || [];
    
    if (questions.length > 0) {
      TestFlow.start({ session, questions, options });
    }
  } catch (err) {
    if (!silent) setFieldError("waiting-error", err.message || "The room hasn't started yet.");
  }
}

function wireJoinRoom() {
  document.getElementById("join-room-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    setFieldError("join-room-error", "");
    const roomId = parseInt(document.getElementById("join-room-id").value, 10);
    const password = parseInt(document.getElementById("join-room-password").value, 10);
    const name = document.getElementById("join-room-name").value.trim();

    if (Number.isNaN(roomId) || Number.isNaN(password) || !name) {
      setFieldError("join-room-error", "Fill in the room ID, password, and your name.");
      return;
    }
    const btn = document.getElementById("join-room-submit-btn");
    setButtonBusy(btn, true, "Joining…");
    try {
      await Api.joinRoom({ roomId, password, name });
      showToast("Joined the room.", "success");
      document.getElementById("join-room-form").reset();
      enterWaitingRoom({ roomId, password, name });
    } catch (err) {
      // If already joined or room exists, allow entering directly into the room flow
      enterWaitingRoom({ roomId, password, name });
    } finally {
      setButtonBusy(btn, false);
    }
  });

  document.getElementById("waiting-check-btn").addEventListener("click", () => {
    const session = RoomSession.get();
    if (session) checkRoomStarted(session);
  });

  document.getElementById("waiting-auto-toggle")?.addEventListener("change", (e) => {
    const session = RoomSession.get();
    stopWaitingPoll();
    if (e.target.checked && session) {
      waitingPollTimer = setInterval(() => checkRoomStarted(session, { silent: true }), 3000);
    }
  });

  document.getElementById("waiting-leave-btn").addEventListener("click", async () => {
    const session = RoomSession.get();
    if (!session) { showView("view-dashboard"); return; }
    const ok = await confirmAction({
      title: "Leave this room?",
      message: "You can rejoin later with the same room ID and password.",
      confirmLabel: "Leave room",
    });
    if (!ok) return;
    const btn = document.getElementById("waiting-leave-btn");
    setButtonBusy(btn, true, "Leaving…");
    try {
      await Api.leaveRoom(session);
      stopWaitingPoll();
      RoomSession.clear();
      showToast("Left the room.", "info");
      goToDashboard();
    } catch (err) {
      showToast(err.message || "Couldn't leave the room.", "error");
    } finally {
      setButtonBusy(btn, false);
    }
  });
}