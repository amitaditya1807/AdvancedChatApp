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
let timer = null;
let joinedPeople = [];

roomTitle.textContent = decodeURIComponent(roomName);
if (!token || !roomId) {
  roomMeta.textContent = "Missing token or room.";
} else {
  me = getCurrentUserId();
  roomMeta.textContent = "Connected";
  loadMessages(false);
  timer = setInterval(() => loadMessages(false), 3000);
}

window.addEventListener("beforeunload", () => timer && clearInterval(timer));

function goBack() { location.href = "./index.html"; }
function handleKey(e){ if(e.key === "Enter") sendMessage(); }

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
  const body = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error(`Status ${res.status}`);
  return body;
}

async function loadMessages(showStatus) {
  try {
    const data = await api(`/chat/rooms/${roomId}/messages`);
    render(data || []);
    if (showStatus) roomMeta.textContent = `Updated ${new Date().toLocaleTimeString()}`;
  } catch {
    roomMeta.textContent = "Failed to load messages";
  }
}

function render(list) {
  joinedPeople = [...new Set(list.map(m => m.senderName).filter(Boolean))];
  peopleCountBadge.textContent = `${joinedPeople.length}`;

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
  await api(`/chat/rooms/${roomId}/messages`, { method:"POST", body: JSON.stringify({ content }) });
  await loadMessages(false);
}

function decodeJwtPayload(jwt) {
  try {
    const payload = jwt.split(".")[1];
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + ((4 - base64.length % 4) % 4), "=");
    return JSON.parse(atob(padded));
  } catch { return null; }
}
function getCurrentUserId() {
  const p = decodeJwtPayload(token);
  return p?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || p?.sub || "";
}
function escapeHtml(v){return String(v).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/\"/g,"&quot;").replace(/'/g,"&#039;");}

function showPeopleList(){
  if (!joinedPeople.length) {
    window.alert("No people joined yet.");
    return;
  }
  window.alert(`People joined (${joinedPeople.length}):\n- ${joinedPeople.join("\n- ")}`);
}