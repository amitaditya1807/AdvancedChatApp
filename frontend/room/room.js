const API_GATEWAY_URL = "https://apigateway1-khy4.onrender.com";
const TOKEN_KEY = "advanced_chat_jwt";

const output = document.getElementById("output");
const sessionText = document.getElementById("sessionText");
const sessionBadge = document.getElementById("sessionBadge");
const roomList = document.getElementById("roomList");
const roomIdInput = document.getElementById("roomIdInput");
const roomPasswordInput = document.getElementById("roomPasswordInput");
const enterRoomButton = document.getElementById("enterRoomButton");
const gatewayUrl = document.getElementById("gatewayUrl");

gatewayUrl.textContent = API_GATEWAY_URL;

let token = localStorage.getItem(TOKEN_KEY) || "";
let rooms = [];

renderSession();
if (token) loadRooms();

function renderSession() {
  const enabled = !!token;
  roomIdInput.disabled = !enabled;
  roomPasswordInput.disabled = !enabled;
  enterRoomButton.disabled = !enabled;

  if (!enabled) {
    sessionText.textContent = "Not signed in";
    sessionBadge.className = "badge";
    sessionBadge.innerHTML = '<span class="dot"></span>Disconnected';
  } else {
    sessionText.textContent = "JWT active";
    sessionBadge.className = "badge connected";
    sessionBadge.innerHTML = '<span class="dot"></span>Connected';
  }
}

async function apiRequest(path, options = {}) {
  const res = await fetch(`${API_GATEWAY_URL}${path}`, {
    method: options.method || "GET",
    headers: {
      "Authorization": `Bearer ${token}`,
      "Content-Type": "application/json",
      ...(options.headers || {})
    },
    body: options.body
  });

  const text = await res.text();
  let body = text;
  try { body = text ? JSON.parse(text) : null; } catch {}

  if (!res.ok) {
    throw new Error(`Status: ${res.status}\n${typeof body === "string" ? body : JSON.stringify(body, null, 2)}`);
  }
  return body;
}

async function loadRooms() {
  try {
    rooms = await apiRequest("/chat/rooms");
    renderRooms();
  } catch (e) {
    output.textContent = `❌ ${e.message}`;
  }
}

function renderRooms() {
  if (!rooms.length) {
    roomList.innerHTML = '<div class="chat-empty">No rooms yet.</div>';
    return;
  }

  roomList.innerHTML = rooms.map(room => `
    <div class="room-card">
      <button class="room-item" onclick="openRoom('${room.id}','${escapeHtml(room.name)}')">
        <span class="room-name">${escapeHtml(room.name)}</span>
        <span class="room-meta">Room ID: ${escapeHtml(room.id)}</span>
      </button>
    </div>
  `).join("");
}

async function enterRoom() {
  const idOrName = roomIdInput.value.trim();
  const password = roomPasswordInput.value.trim();

  if (!idOrName || !password) {
    output.textContent = "❌ Enter Room ID/Name and password.";
    return;
  }

  try {
    await loadRooms();
    let room = rooms.find(r => r.id === idOrName || r.name.toLowerCase() === idOrName.toLowerCase());

    if (!room) {
      room = await apiRequest("/chat/rooms", {
        method: "POST",
        body: JSON.stringify({ name: idOrName, password })
      });
      output.textContent = `✅ Room created. Share ID: ${room.id}`;
    }

    sessionStorage.setItem(`room_password_${room.id}`, password);
    window.location.href = `./chat/index.html?roomId=${encodeURIComponent(room.id)}&roomName=${encodeURIComponent(room.name)}`;
  } catch (e) {
    output.textContent = `❌ ${e.message}`;
  }
}

function openRoom(roomId, roomName) {
  const password = prompt("Enter room password:");
  if (password == null) return;
  sessionStorage.setItem(`room_password_${roomId}`, password);
  window.location.href = `./chat/index.html?roomId=${encodeURIComponent(roomId)}&roomName=${encodeURIComponent(roomName)}`;
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function clearOutput() {
  output.textContent = "Ready.";
}