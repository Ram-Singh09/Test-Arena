/* =====================================================================
   test-flow.js — answering questions and submitting once
   ===================================================================== */

const TestFlow = (() => {
  let state = null;
  let timerHandle = null;

  function start({ session, questions, options }) {
    state = {
      session,
      questions,
      options, // flat, 4 per question
      answers: new Array(questions.length).fill(-1),
      currentIndex: 0,
      startedAt: Date.now(),
    };
    document.getElementById("test-room-id").textContent = String(session.roomId);
    renderNav();
    renderQuestion(0);
    updateProgress();
    startStopwatch();
    showView("view-test");
  }

  function startStopwatch() {
    stopStopwatch();
    const elapsedEl = document.getElementById("test-elapsed");
    timerHandle = setInterval(() => {
      const secs = Math.floor((Date.now() - state.startedAt) / 1000);
      elapsedEl.textContent = formatElapsed(secs);
    }, 1000);
  }

  function stopStopwatch() {
    if (timerHandle) {
      clearInterval(timerHandle);
      timerHandle = null;
    }
  }

  function renderNav() {
    const nav = document.getElementById("question-nav");
    nav.innerHTML = "";
    state.questions.forEach((_, idx) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "qnav-btn";
      btn.textContent = String(idx + 1);
      btn.addEventListener("click", () => { renderQuestion(idx); });
      nav.appendChild(btn);
    });
    refreshNavStates();
  }

  function refreshNavStates() {
    const nav = document.getElementById("question-nav");
    Array.from(nav.children).forEach((btn, idx) => {
      btn.classList.toggle("answered", state.answers[idx] !== -1);
      btn.classList.toggle("current", idx === state.currentIndex);
    });
  }

  function renderQuestion(idx) {
    state.currentIndex = idx;
    document.getElementById("test-question-count").textContent = `Question ${idx + 1} of ${state.questions.length}`;
    document.getElementById("test-question-text").textContent = state.questions[idx];

    const optWrap = document.getElementById("test-options");
    optWrap.innerHTML = "";
    const letters = ["A", "B", "C", "D"];
    const optionSlice = state.options.slice(idx * 4, idx * 4 + 4);
    optionSlice.forEach((optText, optIdx) => {
      const label = document.createElement("label");
      label.className = "bubble-option";
      label.innerHTML = `
        <input type="radio" name="test-answer" value="${optIdx}" ${state.answers[idx] === optIdx ? "checked" : ""}>
        <span class="bubble">${letters[optIdx]}</span>
        <span class="bubble-text"></span>
      `;
      label.querySelector(".bubble-text").textContent = optText;
      label.querySelector("input").addEventListener("change", () => {
        state.answers[idx] = optIdx;
        refreshNavStates();
        updateProgress();
      });
      optWrap.appendChild(label);
    });

    document.getElementById("test-prev-btn").disabled = idx === 0;
    document.getElementById("test-next-btn").disabled = idx === state.questions.length - 1;
    refreshNavStates();
  }

  function updateProgress() {
    const answered = state.answers.filter((a) => a !== -1).length;
    document.getElementById("test-progress").textContent = `${answered} / ${state.questions.length} answered`;
  }

  function getState() { return state; }
  function stop() { stopStopwatch(); }

  return { start, renderQuestion, getState, stop };
})();

function wireTestFlow() {
  document.getElementById("test-prev-btn").addEventListener("click", () => {
    const s = TestFlow.getState();
    if (s && s.currentIndex > 0) TestFlow.renderQuestion(s.currentIndex - 1);
  });

  document.getElementById("test-next-btn").addEventListener("click", () => {
    const s = TestFlow.getState();
    if (s && s.currentIndex < s.questions.length - 1) TestFlow.renderQuestion(s.currentIndex + 1);
  });

  document.getElementById("test-submit-btn").addEventListener("click", async () => {
    const s = TestFlow.getState();
    if (!s) return;

    const unanswered = s.answers.filter((a) => a === -1).length;
    const message = unanswered > 0
      ? `You have ${unanswered} unanswered question${unanswered === 1 ? "" : "s"}. You can only submit once — continue anyway?`
      : "You can only submit once. Submit your answers now?";
    
    const ok = await confirmAction({ title: "Submit test?", message, confirmLabel: "Submit" });
    if (!ok) return;

    const btn = document.getElementById("test-submit-btn");
    setButtonBusy(btn, true, "Submitting…");

    try {
      // 1. Submit answers and capture raw Score returned by backend
      const score = await Api.submitTest({
        answers: s.answers,
        roomId: s.session.roomId,
        password: s.session.password,
      });

      TestFlow.stop();

      // 2. Notify the user of their score
      showToast(`Test submitted! Your score: ${score}`, "success", 6000);

      // 3. Navigate directly to the leaderboard
      Leaderboard.open({
        roomId: s.session.roomId,
        password: s.session.password,
        name: s.session.name,
        viewerName: s.session.name,
      });
    } catch (err) {
      showToast(err.message || "Couldn't submit the test — you may have already submitted it.", "error");
    } finally {
      setButtonBusy(btn, false);
    }
  });
}