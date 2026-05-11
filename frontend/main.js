const API_GATEWAY_URL = "https://apigateway1-khy4.onrender.com";
const TOKEN_KEY = "advanced_chat_jwt";

const output = document.getElementById("output");
const chatButton = document.getElementById("chatButton");
const roomServiceButton = document.getElementById("roomServiceButton");
const sessionText = document.getElementById("sessionText");
const sessionBadge = document.getElementById("sessionBadge");
const tokenPreview = document.getElementById("tokenPreview");
const gatewayUrl = document.getElementById("gatewayUrl");

gatewayUrl.textContent = API_GATEWAY_URL;

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
    sessionText.textContent = "Not signed in";
    sessionBadge.className = "badge";
    sessionBadge.innerHTML = '<span class="dot"></span>Disconnected';
    tokenPreview.textContent = "No token stored";
    roomServiceButton.disabled = true;
    return;
  }

  sessionText.textContent = "JWT stored in this browser";
  sessionBadge.className = "badge connected";
  sessionBadge.innerHTML = '<span class="dot"></span>Connected';
  tokenPreview.textContent = token.length > 34 ? `${token.slice(0, 18)}...${token.slice(-12)}` : token;
  roomServiceButton.disabled = false;
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