/* =====================================================================
   create-room.js — room settings + question builder
   Also powers the host panel's "replace test paper" flow, since both
   need the same question/answer builder UI.
   ===================================================================== */

const CreateRoomBuilder = (() => {
  let qCounter = 0;
  let mode = "create"; // "create" | "replace"
  let replaceContext = null; // { roomId, password, name } — used only in "replace" mode

  const listEl = () => document.getElementById("question-builder-list");

  function renumber() {
    listEl().querySelectorAll(".qb-item").forEach((item, idx) => {
      item.querySelector(".qb-item-index").textContent = `Question ${idx + 1}`;
    });
  }

  function addQuestionRow() {
    qCounter += 1;
    const uid = qCounter;
    const wrap = document.createElement("div");
    wrap.className = "qb-item";
    wrap.dataset.uid = String(uid);
    wrap.innerHTML = `
      <div class="qb-item-head">
        <span class="qb-item-index">Question</span>
        <button type="button" class="qb-remove-btn">Remove</button>
      </div>
      <label>Question text</label>
      <input type="text" class="qb-question-text" placeholder="e.g. What does 'const' do in JavaScript?">
      <div class="qb-options-grid">
        ${["A", "B", "C", "D"].map((letter, i) => `
          <div class="qb-option-row">
            <label><input type="radio" name="qb-correct-${uid}" value="${i}" ${i === 0 ? "checked" : ""}> ${letter}</label>
            <input type="text" class="qb-option-text" placeholder="Option ${letter}">
          </div>
        `).join("")}
      </div>
    `;
    wrap.querySelector(".qb-remove-btn").addEventListener("click", () => {
      const items = listEl().querySelectorAll(".qb-item");
      if (items.length <= 1) {
        showToast("A room needs at least one question.", "error");
        return;
      }
      wrap.remove();
      renumber();
    });
    listEl().appendChild(wrap);
    renumber();
    return wrap;
  }

  function resetBuilder(initialCount = 1) {
    listEl().innerHTML = "";
    qCounter = 0;
    setFieldError("builder-error", "");
    for (let i = 0; i < initialCount; i++) addQuestionRow();
  }

  /** Reads the DOM and returns { questions, options, ans } flattened
   *  to match the Test_Paper record shape, or throws Error on invalid input. */
  function collectTestPaper() {
    const items = Array.from(listEl().querySelectorAll(".qb-item"));
    if (items.length < 1) throw new Error("Add at least one question.");
    if (items.length > 500) throw new Error("A test can have at most 500 questions.");

    const questions = [];
    const options = [];
    const ans = [];

    items.forEach((item, idx) => {
      const qText = item.querySelector(".qb-question-text").value.trim();
      if (!qText) throw new Error(`Question ${idx + 1} needs its question text filled in.`);

      const optionInputs = Array.from(item.querySelectorAll(".qb-option-text"));
      const optionValues = optionInputs.map((inp) => inp.value.trim());
      if (optionValues.some((v) => !v)) {
        throw new Error(`Question ${idx + 1} needs all four options filled in.`);
      }

      const checkedRadio = item.querySelector(`input[type="radio"]:checked`);
      if (!checkedRadio) throw new Error(`Question ${idx + 1} needs a correct answer selected.`);

      questions.push(qText);
      options.push(...optionValues);
      ans.push(parseInt(checkedRadio.value, 10));
    });

    return { questions, options, ans };
  }

  function setMode(newMode, context) {
    mode = newMode;
    replaceContext = context || null;
    const settingsCard = document.getElementById("create-room-form");
    const heading = document.getElementById("create-room-heading");
    const submitBtn = document.getElementById("create-room-submit-btn");
    if (mode === "replace") {
      settingsCard.classList.add("hidden");
      heading.textContent = "Replace test paper";
      submitBtn.textContent = "Upload test paper";
    } else {
      settingsCard.classList.remove("hidden");
      heading.textContent = "Create a room";
      submitBtn.textContent = "Create room";
    }
  }

  function getMode() { return mode; }
  function getReplaceContext() { return replaceContext; }

  return { addQuestionRow, resetBuilder, collectTestPaper, setMode, getMode, getReplaceContext };
})();

function openCreateRoom() {
  CreateRoomBuilder.setMode("create");
  CreateRoomBuilder.resetBuilder(1);
  const user = Auth.getUser();
  document.getElementById("room-creator-name").value = user?.name || "";
  document.getElementById("room-minutes").value = "30";
  document.getElementById("room-password").value = "";
  document.getElementById("room-positive-marks").value = "4";
  document.getElementById("room-negative-marks").value = "1";
  showView("view-create-room");
}

function openReplaceTest(hostContext) {
  CreateRoomBuilder.setMode("replace", hostContext);
  CreateRoomBuilder.resetBuilder(1);
  showView("view-create-room");
}

function wireCreateRoom() {
  document.getElementById("add-question-btn").addEventListener("click", () => {
    CreateRoomBuilder.addQuestionRow();
  });

  document.getElementById("create-room-cancel-btn").addEventListener("click", async () => {
    const ok = await confirmAction({
      title: "Discard this test?",
      message: "Anything you've written here will be lost.",
      confirmLabel: "Discard",
    });
    if (!ok) return;
    if (CreateRoomBuilder.getMode() === "replace") {
      showView("view-host-panel");
    } else {
      showView("view-dashboard");
    }
  });

  document.getElementById("create-room-submit-btn").addEventListener("click", async () => {
    setFieldError("builder-error", "");
    const btn = document.getElementById("create-room-submit-btn");

    let testPaper;
    try {
      testPaper = CreateRoomBuilder.collectTestPaper();
    } catch (err) {
      setFieldError("builder-error", err.message);
      return;
    }

    if (CreateRoomBuilder.getMode() === "replace") {
      setButtonBusy(btn, true, "Uploading…");
      try {
        await Api.uploadTest(testPaper);
        showToast("Test paper updated.", "success");
        showView("view-host-panel");
      } catch (err) {
        setFieldError("builder-error", err.message || "Couldn't upload the test paper — if the room has already started, questions can no longer be changed.");
      } finally {
        setButtonBusy(btn, false);
      }
      return;
    }

    // mode === "create"
    const minutes = parseInt(document.getElementById("room-minutes").value, 10);
    const password = parseInt(document.getElementById("room-password").value, 10);
    const positiveMarks = parseInt(document.getElementById("room-positive-marks").value, 10);
    const negativeMarks = parseInt(document.getElementById("room-negative-marks").value, 10);
    const creatorName = document.getElementById("room-creator-name").value.trim();

    if (!minutes || minutes < 1 || minutes > 180) {
      setFieldError("builder-error", "Duration must be between 1 and 180 minutes.");
      return;
    }
    if (Number.isNaN(password) || password < 0 || password > 65535) {
      setFieldError("builder-error", "Room password must be a number between 0 and 65535.");
      return;
    }
    if (Number.isNaN(positiveMarks) || positiveMarks < 0 || positiveMarks > 255 ||
        Number.isNaN(negativeMarks) || negativeMarks < 0 || negativeMarks > 255) {
      setFieldError("builder-error", "Marks must be numbers between 0 and 255.");
      return;
    }
    if (!creatorName) {
      setFieldError("builder-error", "Enter the name participants will see as the host.");
      return;
    }

    setButtonBusy(btn, true, "Creating room…");
    try {
      const data = await Api.createRoom({
        minutes, password, positiveMarks, negativeMarks, creatorName, testPaper,
      });
      HostPanel.openWithRoom({
        roomId: readField(data, "Room_id", "RoomId", "Id", "room_id", "id"),
        password, minutes, positiveMarks, negativeMarks, creatorName,
        questionCount: testPaper.questions.length,
      });
      showToast("Room created.", "success");
    } catch (err) {
      setFieldError("builder-error", err.message || "Couldn't create the room.");
    } finally {
      setButtonBusy(btn, false);
    }
  });
}
