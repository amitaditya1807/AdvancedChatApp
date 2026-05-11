const API_GATEWAY_URL = "https://apigateway1-khy4.onrender.com";
const TOKEN_KEY = "advanced_chat_jwt";

const token = localStorage.getItem(TOKEN_KEY) || "";
const params = new URLSearchParams(location.search);
const roomId = params.get("roomId") || "";
const roomName = params.get("roomName") || "Room";
const password = sessionStorage.getItem(`room_password_${roomId}`) || "";

const messagesEl = document.getElementById("messages");
const input = document.getElementById("messageInput");
const roomTitle = document.getElementById("roomTitle");
const roomMeta = document.getElementById("roomMeta");
const peopleCountBadge = document.getElementById("peopleCountBadge");

let me = "";
let roomStateTimer = null;
let heartbeatTimer = null;
let joinedPeople = [];

roomTitle.textContent = decodeURIComponent(roomName);
if (!token) {
  window.location.replace("../../index.html?authRequired=room");
} else if (!roomId || !password) {
  roomMeta.textContent = "Missing room or password.";
} else {
  me = getCurrentUserId();
  roomMeta.textContent = "Connected";
  startLiveRoomUpdates();
}

window.addEventListener("beforeunload", stopLiveRoomUpdates);

function goHome() {
  location.href = "../../index.html";
}

function goBack() {
  location.href = "../index.html";
}

function handleKey(e) {
  if (e.key === "Enter") sendMessage();
}

async function api(path, options = {}) {
  const res = await fetch(`${API_GATEWAY_URL}${path}`, {
    method: options.method || "GET",
    headers: {
      "Authorization": `Bearer ${token}`,
      "Content-Type": "application/json",
      ...(password ? { "X-Room-Password": password } : {})
    },
    body: options.body
  });

  const text = await res.text();
  let body = null;
  try { body = text ? JSON.parse(text) : null; } catch { body = text; }

  if (!res.ok) {
    const message = typeof body === "string" ? body : (body?.error || `Status ${res.status}`);
    throw new Error(message);
  }

  return body;
}

function startLiveRoomUpdates() {
  touchParticipant();
  loadRoomState(false);

  heartbeatTimer = setInterval(touchParticipant, 4000);
  roomStateTimer = setInterval(() => loadRoomState(false), 1000);
}

function stopLiveRoomUpdates() {
  if (heartbeatTimer) clearInterval(heartbeatTimer);
  if (roomStateTimer) clearInterval(roomStateTimer);
  heartbeatTimer = null;
  roomStateTimer = null;
}

async function touchParticipant() {
  try {
    await api(`/chat/rooms/${roomId}/participants/heartbeat`, { method: "POST" });
  } catch (error) {
    roomMeta.textContent = error.message || "Failed to update presence";
  }
}

async function loadRoomState(showStatus) {
  try {
    const [messages, participants] = await Promise.all([
      api(`/chat/rooms/${roomId}/messages`),
      api(`/chat/rooms/${roomId}/participants`)
    ]);

    setParticipants(participants || []);
    render(messages || []);

    if (showStatus) roomMeta.textContent = `Updated ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    roomMeta.textContent = error.message || "Failed to load room";
  }
}

async function loadMessages(showStatus) {
  await touchParticipant();
  await loadRoomState(showStatus);
}

function setParticipants(participants) {
  joinedPeople = [...new Set((participants || []).filter(Boolean))];
  peopleCountBadge.textContent = `${joinedPeople.length}`;
}

function render(list) {
  messagesEl.innerHTML = list.map(m => {
    const isYou = me && (m.senderUserId === me);
    return `<div class="bubble ${isYou ? "you" : "other"}">${escapeHtml(m.content)}<span class="meta">${escapeHtml(m.senderName)} • ${new Date(m.sentAtUtc).toLocaleTimeString()}</span></div>`;
  }).join("") || '<div class="meta">No messages yet.</div>';

  messagesEl.scrollTop = messagesEl.scrollHeight;
}

async function sendMessage() {
  const content = input.value.trim();
  if (!content) return;
  input.value = "";

  try {
    await api(`/chat/rooms/${roomId}/messages`, {
      method: "POST",
      body: JSON.stringify({ content })
    });

    await loadRoomState(false);
  } catch (error) {
    roomMeta.textContent = error.message || "Failed to send message";
  }
}

function showPeopleList() {
  if (!joinedPeople.length) {
    alert("No people joined yet.");
    return;
  }
  alert(`People joined (${joinedPeople.length}):\n- ${joinedPeople.join("\n- ")}`);
}

function decodeJwtPayload(jwt) {
  try {
    const payload = jwt.split(".")[1];
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + ((4 - base64.length % 4) % 4), "=");
    return JSON.parse(atob(padded));
  } catch {
    return null;
  }
}

function getCurrentUserId() {
  const p = decodeJwtPayload(token);
  return p?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || p?.sub || "";
}

function escapeHtml(v) {
  return String(v)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}