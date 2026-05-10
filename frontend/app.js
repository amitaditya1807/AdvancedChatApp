const API_GATEWAY_URL = "https://apigateway1-khy4.onrender.com";
const TOKEN_KEY = "advanced_chat_jwt";

const output = document.getElementById("output");
const chatButton = document.getElementById("chatButton");
const enterRoomButton = document.getElementById("enterRoomButton");
const sessionText = document.getElementById("sessionText");
const sessionBadge = document.getElementById("sessionBadge");
const tokenPreview = document.getElementById("tokenPreview");
const expiryBadge = document.getElementById("expiryBadge");
const expiryText = document.getElementById("expiryText");
const expiryMeta = document.getElementById("expiryMeta");
const expiryProgress = document.getElementById("expiryProgress");
const gatewayUrl = document.getElementById("gatewayUrl");
const roomList = document.getElementById("roomList");
const roomIdInput = document.getElementById("roomIdInput");
const roomPasswordInput = document.getElementById("roomPasswordInput");

gatewayUrl.textContent = API_GATEWAY_URL;

let token = readTokenFromUrl() || localStorage.getItem(TOKEN_KEY) || "";
let rooms = [];
let roomPollTimerId = null;

if (token) {
  localStorage.setItem(TOKEN_KEY, token);
  removeTokenFromUrl();
}

renderSession();

function readTokenFromUrl() {
  const query = new URLSearchParams(window.location.search);
  const hash = new URLSearchParams(window.location.hash.replace(/^#/, ""));
  return query.get("token") || hash.get("token") || "";
}

function removeTokenFromUrl() {
  window.history.replaceState({}, document.title, window.location.pathname || "/");
}

function renderSession() {
  const enabled = Boolean(token);
  enterRoomButton.disabled = !enabled;
  roomIdInput.disabled = !enabled;
  roomPasswordInput.disabled = !enabled;

  if (!enabled) {
    stopRoomPolling();
    sessionText.textContent = "Not signed in";
    sessionBadge.className = "badge";
    sessionBadge.innerHTML = '<span class="dot"></span>Disconnected';
    tokenPreview.textContent = "No token stored";
    expiryBadge.className = "expiry-badge";
    expiryBadge.textContent = "No token";
    expiryText.textContent = "Not available";
    expiryMeta.textContent = "Sign in to see token lifetime and countdown.";
    expiryProgress.style.width = "0%";
    roomList.innerHTML = '<div class="chat-empty">Sign in to load your rooms.</div>';
    return;
  }

  sessionText.textContent = "JWT stored in this browser";
  sessionBadge.className = "badge connected";
  sessionBadge.innerHTML = '<span class="dot"></span>Connected';
  tokenPreview.textContent = token.length > 34 ? `${token.slice(0, 18)}...${token.slice(-12)}` : token;
  expiryBadge.className = "expiry-badge active";
  expiryBadge.textContent = "Active";
  expiryText.textContent = "Session active";
  expiryMeta.textContent = "JWT is available for authenticated room actions.";
  expiryProgress.style.width = "100%";

  loadRooms();
  startRoomPolling();
}

function login() { window.location.href = `${API_GATEWAY_URL}/auth/google/login`; }

async function callChatService() {
  if (!ensureSignedIn()) return;
  try {
    chatButton.disabled = true;
    output.textContent = "⏳ Checking ChatService...";
    const body = await apiRequest("/chat");
    output.textContent = `✅ ChatService reachable\n\n${JSON.stringify(body, null, 2)}`;
  } catch (error) {
    output.textContent = `❌ ERROR:\n${error.message}`;
  } finally {
    chatButton.disabled = false;
  }
}

async function loadRooms() {
  if (!ensureSignedIn()) return;
  try {
    rooms = await apiRequest("/chat/rooms");
    renderRooms();
  } catch (error) {
    output.textContent = `❌ ERROR:\n${error.message}`;
  }
}

async function enterRoom() {
  if (!ensureSignedIn()) return;

  const input = roomIdInput.value.trim();
  const password = roomPasswordInput.value.trim();
  if (!input) { output.textContent = "❌ Enter room ID or room name."; return; }
  if (!password) { output.textContent = "❌ Enter room password."; return; }

  enterRoomButton.disabled = true;

  try {
    await loadRooms();
    let room = rooms.find(r => r.id === input || r.name.toLowerCase() === input.toLowerCase());

    if (!room) {
      room = await apiRequest("/chat/rooms", {
        method: "POST",
        body: JSON.stringify({ name: input, password })
      });
      output.textContent = `✅ Room created: ${room.name}\nShare this Room ID: ${room.id}`;
      rooms = [...rooms, room];
      renderRooms();
    }

    sessionStorage.setItem(`room_password_${room.id}`, password);
    window.location.href = `./chat.html?roomId=${encodeURIComponent(room.id)}&roomName=${encodeURIComponent(room.name || "Room")}`;
  } catch (error) {
    output.textContent = `❌ ERROR:\n${error.message}`;
  } finally {
    enterRoomButton.disabled = false;
  }
}

function renderRooms() {
  if (!token) { roomList.innerHTML = '<div class="chat-empty">Sign in to load your rooms.</div>'; return; }
  const currentUserId = getCurrentUserId();
  const myRooms = rooms.filter(r => r.createdByUserId === currentUserId);

  if (!myRooms.length) {
    roomList.innerHTML = '<div class="chat-empty">You have not created any rooms yet.</div>';
    return;
  }

  roomList.innerHTML = myRooms.map(room => `
    <div class="room-card">
      <button class="room-item" onclick="openExistingRoom('${room.id}','${escapeHtml(room.name)}')">
        <span class="room-name">${escapeHtml(room.name)}</span>
        <span class="room-meta">Room ID: ${escapeHtml(room.id)}</span>
      </button>
      <button class="danger room-delete" onclick="deleteRoom('${room.id}')">Delete</button>
    </div>`).join("");
}

function openExistingRoom(roomId, roomName) {
  const pass = window.prompt("Enter room password to open chat");
  if (pass === null) return;
  sessionStorage.setItem(`room_password_${roomId}`, pass);
  window.location.href = `./chat.html?roomId=${encodeURIComponent(roomId)}&roomName=${encodeURIComponent(roomName || "Room")}`;
}

async function deleteRoom(roomId) {
  if (!ensureSignedIn()) return;
  try {
    await apiRequest(`/chat/rooms/${roomId}`, { method: "DELETE" });
    rooms = rooms.filter(r => r.id !== roomId);
    renderRooms();
    output.textContent = "✅ Room deleted.";
  } catch (error) {
    output.textContent = `❌ ERROR:\n${error.message}`;
  }
}

async function apiRequest(path, options = {}) {
  const response = await fetch(`${API_GATEWAY_URL}${path}`, {
    method: options.method || "GET",
    headers: {
      "Authorization": `Bearer ${token}`,
      "Content-Type": "application/json",
      ...(options.headers || {})
    },
    body: options.body
  });
  const text = await response.text();
  let body = text;
  if (text) { try { body = JSON.parse(text); } catch {} }
  if (!response.ok) throw new Error(`Status: ${response.status}\n${typeof body === "string" ? body : JSON.stringify(body, null, 2)}`);
  return body || null;
}

function startRoomPolling() {
  stopRoomPolling();
  roomPollTimerId = window.setInterval(() => { if (token) loadRooms(); }, 5000);
}

function stopRoomPolling() {
  if (!roomPollTimerId) return;
  clearInterval(roomPollTimerId);
  roomPollTimerId = null;
}

function ensureSignedIn() {
  if (!token) { output.textContent = "❌ Please sign in with Google first."; return false; }
  return true;
}

function getCurrentUserId() {
  const payload = decodeJwtPayload(token);
  return payload?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || payload?.sub || "";
}

function decodeJwtPayload(jwt) {
  try {
    const payload = jwt.split(".")[1];
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + ((4 - base64.length % 4) % 4), "=");
    return JSON.parse(atob(padded));
  } catch { return null; }
}

function escapeHtml(value) {
  return String(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/\"/g, "&quot;").replace(/'/g, "&#039;");
}

function handleRoomInputKey(event) { if (event.key === "Enter") enterRoom(); }

function logout() {
  localStorage.removeItem(TOKEN_KEY);
  token = "";
  rooms = [];
  stopRoomPolling();
  renderSession();
  output.textContent = "👋 Logged out.";
}

function clearOutput() { output.textContent = "Ready."; }