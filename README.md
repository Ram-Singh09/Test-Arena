# Test-Arena

# TestArena 🎓

A modern, full-featured **room-based coding assessment platform** built to make online testing effortless. Create a test room, share a code, watch participants answer in real-time, and grade everyone instantly on a live leaderboard.

## ✨ Features

### For Test Creators (Hosts)
- **Intuitive Room Creation** – Set duration (1–180 min), marking scheme, and password in seconds
- **Dynamic Question Builder** – Add unlimited questions (up to 500) with 4 bubble-sheet options each; drag to remove or reorder
- **Shareable Room Codes** – Auto-generate numeric IDs and passwords that participants can join instantly
- **Live Control Panel** – Start the room, lock in the test paper, monitor participants, and jump to the leaderboard
- **Paper Replacement** – Swap out the test before you start, but lock it in once participants begin
- **Instant Grading** – Questions are marked automatically based on your positive/negative mark scheme

### For Participants
- **Seamless Registration & Login** – Email/password with OTP-verified registration
- **Quick Room Entry** – Just enter the room ID and password; no invite links needed
- **Waiting Room** – Sit tight with auto-polling; the moment the host starts, you're in the test
- **OMR-Style Answer Sheet** – Clean, numbered bubble grid for selecting answers; visual feedback on answered/unanswered
- **Question Navigator** – Jump between questions instantly; answered ones are highlighted
- **Elapsed Stopwatch** – Track time without pressure; the server enforces the real deadline
- **Single Submission** – Submit once, then see yourself climb the leaderboard

### Platform-Wide
- **Live Leaderboard** – Real-time standings updated as submissions come in; host and participants both see the same board
- **Light & Dark Mode** – Full theme toggle, persisted across sessions, respects system preference on first load
- **Professional UI** – Distinctive "exam ticket" design with perforated headers and clean typography; no templated look
- **Zero Dependencies** – Plain HTML/CSS/JavaScript; no build step, no frameworks, no npm install
- **Responsive Design** – Scales perfectly from mobile to desktop

## 🔐 Security & Design

- **OTP-Verified Auth** – Email verification on signup and password reset
- **JWT Bearer Tokens** – Session management via auth headers
- **Client-Side Validation** – Mirrors server constraints (duration 1–180 min, 1–500 questions, marks 0–255)
- **Room Passwords** – Numeric passwords (0–65535) prevent random joining; only correct room+password grants entry
- **One Submission Rule** – Enforced server-side; frontend prevents accidental double-submission
- **Creator-Only Actions** – Only the room creator can start their room or replace the test paper

## 🎨 Design & UX

- **Ticket-Stub Aesthetic** – Each card mimics an exam ticket: dark header strip, perforated tear-line, clean body. A visual metaphor that makes sense for a testing platform.
- **OMR Grid** – Question numbering uses a real answer-grid layout because order actually *matters* here (not decoration).
- **Type System** – [Space Grotesk](https://fonts.google.com/specimen/Space+Grotesk) for headers (bold, technical); [Inter](https://fonts.google.com/specimen/Inter) for body (readable, professional); [IBM Plex Mono](https://fonts.google.com/specimen/IBM+Plex+Mono) for room codes and timestamps.
- **Deliberate Palette** – Amber accent (#F5A623) signals action; clean grays for focus; success/danger states are never ambiguous.
- **Animation Restraint** – Smooth transitions on interactions, pulse feedback on waiting states, but no gratuitous motion.

## 🚀 Quick Start

### Prerequisites
- A running TestArena backend (ASP.NET Core or equivalent)
- CORS enabled on your backend for your frontend's origin
- A modern browser (Chrome, Firefox, Safari, Edge)

### Setup
1. **Clone or download** this repository
2. **Open `index.html`** in your browser (or serve the folder with `python3 -m http.server 8080` if needed)
3. **On first load**, enter your backend's base URL (e.g., `https://localhost:7042/`)—it's stored locally and you can change it anytime from the footer
4. **Done** — no build step, no dependencies, no fuss

## 📋 Project Structure

```
testarena/
├── index.html                 # Single-page app shell with all views
├── README.md                  # Setup & assumptions doc
├── css/
│   └── styles.css            # ~2500 lines: tokens, components, themes
└── js/
    ├── config.js             # Endpoints + OTP enum (from your backend)
    ├── utils.js              # Storage, toasts, confirm modal, router
    ├── theme.js              # Light/dark mode toggle
    ├── api.js                # Fetch wrapper + base-URL config
    ├── auth.js               # Login, register, forgot password
    ├── profile.js            # Account management
    ├── create-room.js        # Room settings + question builder
    ├── host-panel.js         # Room control & leaderboard
    ├── join-room.js          # Join + waiting room
    ├── test-flow.js          # Question navigator, answers, submit
    ├── leaderboard.js        # Live standings
    └── app.js                # Initialization & module wiring
```

## 🔗 API Integration

The frontend expects these endpoints (copied from your backend's exact naming):

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `TestArena/RegisterNewUser` | POST | Create account + OTP verification |
| `TestArena/LoginUser` | POST | Log in → JWT token |
| `TestArena/SendOTP` | POST | Request verification code (email) |
| `TestArena/ForgotPassword` | POST | Reset password + OTP |
| `TestArena/TestRoom/CreateRoom` | POST | Host creates a room |
| `TestArena/TestRoom/StartRoom` | POST | Host starts the test |
| `TestArena/TestRoom/UploadTest` | PATCH | Replace the test paper |
| `TestArena/TestRoom/GetQues` | POST | Participant retrieves questions |
| `TestArena/User/JoinRoom` | POST | Participant joins a room |
| `TestArena/User/LeaveRoom` | PATCH | Participant leaves a room |
| `TestArena/TestRoom/SubmitTest` | POST | Participant submits answers |
| `TestArena/TestRoom/GetLeaderboard` | POST | Fetch live standings |
| `TestArena/SignOut` | POST | Log out |

See the **`README.md`** in the root folder for assumptions made about response shapes and field naming (especially for `Create_Room`, `Get_Questions`, and the leaderboard format).

## 🛠️ Built With

### Frontend Technologies
- **Vanilla JavaScript** (ES6+, no transpiler needed)
- **Modern CSS** with CSS variables for theming, grid/flexbox for layout
- **Google Fonts** (Space Grotesk, Inter, IBM Plex Mono)
- **localStorage** for persistence (theme, API base URL, auth token, session state)
- **Fetch API** for HTTP requests with proper error handling

### Development & Design
- **✨ Crafted with Claude Code** – AI-powered code generation and architecture for clean, modular JavaScript
- **✨ Enhanced with Gemini** – Design consultation, UX refinement, and visual identity development

The combination of Claude's structured code generation and Gemini's design thinking resulted in a codebase that's readable, maintainable, and visually distinct—no templated look, every pixel with purpose.

## 📖 How It Works

### As a Test Creator
1. Log in or create an account (OTP-verified)
2. Go to Dashboard → "Create a room"
3. Set the test duration, marking scheme, and your name
4. Write questions and mark the correct answers
5. Click "Create room" — get a shareable room ID + password
6. Host panel shows your code; share it with participants
7. Click "Start room" when everyone has joined
8. Watch the leaderboard update as people submit
9. Optionally replace the test paper before starting, or manage standings after

### As a Participant
1. Log in or register
2. Go to Dashboard → "Join a room"
3. Enter the room ID, password, and your name
4. Sit in the waiting room; the app auto-polls for the start
5. Once the host starts, the test loads automatically
6. Answer questions using the bubble grid; jump between questions at will
7. When ready, submit — one chance, confirmed before going live
8. Leaderboard shows where you stand in real-time

## 🎯 Key Design Decisions

**No external dependencies** – Every feature is custom-built to avoid version hell and keep load times instant.

**Defensive response parsing** – Since DTOs don't pin down response casing, the frontend tries multiple field name variants (Token/token/TOKEN, Room_id/RoomId/id, etc.) to handle whatever your backend outputs.

**Proactive UI feedback** – Buttons disable themselves based on room state; help text explains why. Errors come straight from the server and are shown in context.

**Accessible by default** – Focus rings, semantic HTML, aria-live toasts, keyboard navigation on all forms, high contrast in both themes.

## 🚦 Known Limitations & Future Work

- **Display name updates** – Not currently exposed by the backend; Profile shows a disabled field as a placeholder
- **Offline mode** – Answers are held only in memory; no auto-save if the connection drops
- **Role-based views** – Host and participant flows are separate (by design), but UI doesn't yet show what happens if both roles join the same room
- **Bulk room management** – Only one room at a time; no admin dashboard for viewing past results

## 💬 Feedback & Contributions

Found a bug? Want to refactor the CSS? Have a feature idea? Open an issue or PR — the codebase is intentionally readable and modular, so it's easy to jump in.

## 📄 License

---

**Built to make online testing simple, professional, and fast.** Whether you're assessing 5 developers or 500 students, TestArena gets out of the way and lets the code speak.

*Frontend crafted with intelligence: powered by Claude Code (architecture & logic) and Gemini (design & UX). No template, no cruft—just thoughtful, intentional code.*

Backend code is made by me, frotnend and this readme file is made by claude/gemini
