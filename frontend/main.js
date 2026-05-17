const API_GATEWAY_URL = "https://apigateway1-khy4.onrender.com";
const TOKEN_KEY = "advanced_chat_jwt";

const output = document.getElementById("output");
const chatButton = document.getElementById("chatButton");
const roomServiceButton = document.getElementById("roomServiceButton");
const cloudStorageButton = document.getElementById("cloudStorageButton");
const sessionPill = document.getElementById("sessionPill");

const authRequired = new URLSearchParams(window.location.search).get("authRequired");

let token = readTokenFromUrl() || localStorage.getItem(TOKEN_KEY) || "";
if (token) {
  localStorage.setItem(TOKEN_KEY, token);
  removeTokenFromUrl();
}

renderSession();

if (authRequired === "room" && !token) {
  output.textContent = "❌ Please sign in with Google before opening Room Service.";
}
if (authRequired === "cloudstorage" && !token) {
  output.textContent = "❌ Please sign in with Google before opening CloudStorage.";
}

function readTokenFromUrl() {
  const query = new URLSearchParams(window.location.search);
  const hash = new URLSearchParams(window.location.hash.replace(/^#/, ""));
  return query.get("token") || hash.get("token") || "";
}

function removeTokenFromUrl() {
  window.history.replaceState({}, document.title, window.location.pathname || "/");
}

function renderSession() {
  if (!token) {
    sessionPill.className = "session-pill disconnected";
    sessionPill.innerHTML = '<span class="dot"></span>Not signed in';
    roomServiceButton.disabled = true;
    cloudStorageButton.disabled = true;
    return;
  }

  sessionPill.className = "session-pill connected";
  sessionPill.innerHTML = '<span class="dot"></span>Logged in · Session active';
  roomServiceButton.disabled = false;
  cloudStorageButton.disabled = false;
}

function login() {
  window.location.href = `${API_GATEWAY_URL}/auth/google/login`;
}

function openRoomService() {
  if (!token) {
    output.textContent = "❌ Please sign in with Google before opening Room Service.";
    return;
  }

  window.location.href = "./room/index.html";
}

function openCloudStorage() {
  if (!token) {
    output.textContent = "❌ Please sign in with Google before opening CloudStorage.";
    return;
  }

  window.location.href = "./cloudstorage/index.html";
}

async function callChatService() {
  if (!token) {
    output.textContent = "❌ Please sign in with Google first.";
    return;
  }

  try {
    chatButton.disabled = true;
    output.textContent = "⏳ Checking ChatService...";

    const response = await fetch(`${API_GATEWAY_URL}/chat`, {
      headers: { Authorization: `Bearer ${token}` }
    });

    const text = await response.text();
    let body = text;
    try { body = text ? JSON.parse(text) : null; } catch {}

    if (!response.ok) {
      output.textContent = `❌ ERROR:\nStatus: ${response.status}\n${typeof body === "string" ? body : JSON.stringify(body, null, 2)}`;
      return;
    }

    output.textContent = `✅ ChatService reachable\n\n${JSON.stringify(body, null, 2)}`;
  } catch (e) {
    output.textContent = `❌ ERROR:\n${e.message}`;
  } finally {
    chatButton.disabled = false;
  }
}

function logout() {
  localStorage.removeItem(TOKEN_KEY);
  token = "";
  renderSession();
  output.textContent = "👋 Logged out.";
}

function clearOutput() {
  output.textContent = "Ready.";
}
