const API_GATEWAY_URL = "https://apigateway1-khy4.onrender.com";
const TOKEN_KEY = "advanced_chat_jwt";

const output = document.getElementById("output");
const chatButton = document.getElementById("chatButton");
const roomsButton = document.getElementById("roomsButton");
const refreshMessagesButton = document.getElementById("refreshMessagesButton");
const createRoomButton = document.getElementById("createRoomButton");
const sendMessageButton = document.getElementById("sendMessageButton");
const sessionText = document.getElementById("sessionText");
const sessionBadge = document.getElementById("sessionBadge");
const tokenPreview = document.getElementById("tokenPreview");
const expiryBadge = document.getElementById("expiryBadge");
const expiryText = document.getElementById("expiryText");
const expiryMeta = document.getElementById("expiryMeta");
const expiryProgress = document.getElementById("expiryProgress");
const gatewayUrl = document.getElementById("gatewayUrl");
const roomList = document.getElementById("roomList");
const messageList = document.getElementById("messageList");
const roomNameInput = document.getElementById("roomNameInput");
const roomPasswordInput = document.getElementById("roomPasswordInput");
const messageInput = document.getElementById("messageInput");

const EXPIRING_SOON_SECONDS = 5 * 60;
const DEFAULT_TOKEN_LIFETIME_SECONDS = 60 * 60;
const MESSAGE_POLL_INTERVAL_MS = 3000;

gatewayUrl.textContent = API_GATEWAY_URL;

let token = readTokenFromUrl() || localStorage.getItem(TOKEN_KEY) || "";
let expiryTimerId = null;
let messagePollTimerId = null;
let isRefreshingMessages = false;
let rooms = [];
let activeRoomId = "";
const roomPasswords = new Map();

if (token) {
    localStorage.setItem(TOKEN_KEY, token);
    removeTokenFromUrl();
}

renderSession();
setChatControlsEnabled(Boolean(token));

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
        stopTokenExpiryTicker();
        stopMessagePolling();
        sessionText.textContent = "Not signed in";
        sessionBadge.className = "badge";
        sessionBadge.innerHTML = '<span class="dot"></span>Disconnected';
        tokenPreview.textContent = "No token stored";
        resetExpiryVisualization();
        setChatControlsEnabled(false);
        return;
    }

    const expiryState = getTokenExpiryState(token);

    if (expiryState.status === "expired") {
        clearExpiredToken();
        return;
    }

    sessionText.textContent = "JWT stored in this browser";
    sessionBadge.className = "badge connected";
    sessionBadge.innerHTML = '<span class="dot"></span>Connected';
    tokenPreview.textContent = token.length > 34
        ? `${token.slice(0, 18)}...${token.slice(-12)}`
        : token;

    setChatControlsEnabled(true);
    renderTokenExpiry(expiryState);
    startTokenExpiryTicker();
}

function startTokenExpiryTicker() {
    if (expiryTimerId) {
        return;
    }

    expiryTimerId = window.setInterval(() => {
        if (!token) {
            renderSession();
            return;
        }

        const expiryState = getTokenExpiryState(token);

        if (expiryState.status === "expired") {
            clearExpiredToken(true);
            return;
        }

        renderTokenExpiry(expiryState);
    }, 1000);
}

function stopTokenExpiryTicker() {
    if (!expiryTimerId) {
        return;
    }

    window.clearInterval(expiryTimerId);
    expiryTimerId = null;
}

function resetExpiryVisualization() {
    expiryBadge.className = "expiry-badge";
    expiryBadge.textContent = "No token";
    expiryText.textContent = "Not available";
    expiryMeta.textContent = "Sign in to see token lifetime and countdown.";
    expiryProgress.className = "expiry-meter-fill";
    expiryProgress.style.width = "0%";
}

function renderTokenExpiry(expiryState) {
    if (expiryState.status === "unknown") {
        expiryBadge.className = "expiry-badge";
        expiryBadge.textContent = "Unknown";
        expiryText.textContent = "Expiry claim missing";
        expiryMeta.textContent = "This JWT does not include a readable exp claim.";
        expiryProgress.className = "expiry-meter-fill warning";
        expiryProgress.style.width = "100%";
        return;
    }

    const isWarning = expiryState.status === "warning";
    expiryBadge.className = `expiry-badge ${isWarning ? "warning" : "active"}`;
    expiryBadge.textContent = isWarning ? "Expiring soon" : "Active";
    expiryText.textContent = formatDuration(expiryState.remainingSeconds);
    expiryMeta.textContent = `Expires ${expiryState.expiresAt.toLocaleString()} • ${expiryState.remainingPercent}% lifetime remaining`;
    expiryProgress.className = `expiry-meter-fill${isWarning ? " warning" : ""}`;
    expiryProgress.style.width = `${expiryState.remainingPercent}%`;
}

function getTokenExpiryState(jwt) {
    const payload = decodeJwtPayload(jwt);
    const exp = Number(payload?.exp);

    if (!Number.isFinite(exp)) {
        return { status: "unknown" };
    }

    const nowSeconds = Date.now() / 1000;
    const remainingSeconds = Math.max(0, Math.floor(exp - nowSeconds));
    const expiresAt = new Date(exp * 1000);

    if (remainingSeconds <= 0) {
        return { status: "expired", expiresAt, remainingSeconds: 0, remainingPercent: 0 };
    }

    const issuedAt = Number(payload?.iat);
    const lifetimeSeconds = Number.isFinite(issuedAt)
        ? Math.max(1, exp - issuedAt)
        : DEFAULT_TOKEN_LIFETIME_SECONDS;
    const remainingPercent = Math.max(1, Math.min(100, Math.round((remainingSeconds / lifetimeSeconds) * 100)));

    return {
        status: remainingSeconds <= EXPIRING_SOON_SECONDS ? "warning" : "active",
        expiresAt,
        remainingSeconds,
        remainingPercent
    };
}

function clearExpiredToken(showMessage = false) {
    localStorage.removeItem(TOKEN_KEY);
    token = "";
    rooms = [];
    activeRoomId = "";
    roomPasswords.clear();
    stopTokenExpiryTicker();
    stopMessagePolling();
    setChatControlsEnabled(false);
    renderRooms();
    renderMessages([]);
    sessionText.textContent = "Session expired";
    sessionBadge.className = "badge";
    sessionBadge.innerHTML = '<span class="dot"></span>Expired';
    tokenPreview.textContent = "Expired token cleared";
    expiryBadge.className = "expiry-badge expired";
    expiryBadge.textContent = "Expired";
    expiryText.textContent = "00:00";
    expiryMeta.textContent = "Your JWT expired. Sign in again to get a fresh token.";
    expiryProgress.className = "expiry-meter-fill expired";
    expiryProgress.style.width = "100%";

    if (showMessage) {
        output.textContent = "⏰ Session expired. Please sign in with Google again.";
    }
}

function formatDuration(totalSeconds) {
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    if (hours > 0) {
        return `${hours}h ${String(minutes).padStart(2, "0")}m ${String(seconds).padStart(2, "0")}s`;
    }

    return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

function login() {
    window.location.href = `${API_GATEWAY_URL}/auth/google/login`;
}

async function callChatService() {
    if (!ensureSignedIn()) {
        return;
    }

    const startedAt = performance.now();
    chatButton.disabled = true;
    chatButton.textContent = "Getting response...";
    output.textContent = "⏳ Getting response from ChatService through the API Gateway...";

    try {
        const body = await apiRequest("/chat");
        const elapsedSeconds = ((performance.now() - startedAt) / 1000).toFixed(1);
        output.textContent = formatChatServiceSuccess(body, elapsedSeconds);
    } catch (error) {
        output.textContent = `❌ ERROR:\n${error.message}`;
    } finally {
        chatButton.disabled = false;
        chatButton.textContent = "Call ChatService";
    }
}

async function loadRooms() {
    if (!ensureSignedIn()) {
        return;
    }

    roomsButton.disabled = true;
    roomsButton.textContent = "Loading rooms...";
    output.textContent = "⏳ Loading rooms...";

    try {
        rooms = await apiRequest("/chat/rooms");
        activeRoomId = rooms.some(room => room.id === activeRoomId) ? activeRoomId : "";
        renderRooms();

        if (!activeRoomId) {
            stopMessagePolling();
            renderMessages([]);
        }
        output.textContent = formatRoomsSuccess(rooms);
    } catch (error) {
        output.textContent = `❌ ERROR:\n${error.message}`;
    } finally {
        roomsButton.disabled = false;
        roomsButton.textContent = "Load Rooms";
    }
}

async function createRoom() {
    if (!ensureSignedIn()) {
        return;
    }

    const name = roomNameInput.value.trim();
    const password = roomPasswordInput.value.trim();

    if (!name) {
        output.textContent = "❌ Enter a room name first.";
        roomNameInput.focus();
        return;
    }

    if (!password) {
        output.textContent = "❌ Enter a room password first.";
        roomPasswordInput.focus();
        return;
    }

    createRoomButton.disabled = true;
    createRoomButton.textContent = "Creating...";

    try {
        const room = await apiRequest("/chat/rooms", {
            method: "POST",
            body: JSON.stringify({ name, password })
        });

        roomNameInput.value = "";
        roomPasswordInput.value = "";
        roomPasswords.set(room.id, password);
        rooms = [...rooms.filter(existingRoom => existingRoom.id !== room.id), room];
        await joinRoom(room.id);
        output.textContent = `✅ Created and joined room: ${room.name}`;
    } catch (error) {
        output.textContent = `❌ ERROR:\n${error.message}`;
    } finally {
        createRoomButton.disabled = false;
        createRoomButton.textContent = "Create & Join";
    }
}

async function joinRoom(roomId) {
    if (!ensureSignedIn()) {
        return;
    }

    const room = rooms.find(existingRoom => existingRoom.id === roomId);

    if (room?.isPasswordProtected && !roomPasswords.has(roomId)) {
        const password = window.prompt(`Enter password for ${room.name}`);

        if (password === null) {
            output.textContent = "🔒 Room join cancelled.";
            return;
        }

        roomPasswords.set(roomId, password);
    }

    activeRoomId = roomId;
    renderRooms();
    await refreshActiveRoomMessages(true);
    startMessagePolling();
}

async function refreshActiveRoomMessages(showStatus = true) {
    if (!ensureSignedIn()) {
        return;
    }

    if (!activeRoomId) {
        stopMessagePolling();

        if (showStatus) {
            output.textContent = "❌ Join a room first.";
        }

        return;
    }

    if (isRefreshingMessages) {
        return;
    }

    isRefreshingMessages = true;

    if (showStatus) {
        refreshMessagesButton.disabled = true;
        refreshMessagesButton.textContent = "Refreshing...";
    }

    try {
        const messages = await apiRequest(`/chat/rooms/${activeRoomId}/messages`, {
            roomPassword: roomPasswords.get(activeRoomId)
        });
        renderMessages(messages);

        if (showStatus) {
            const activeRoom = rooms.find(room => room.id === activeRoomId);
            output.textContent = `✅ Joined ${activeRoom?.name || "room"} and loaded ${messages.length} message${messages.length === 1 ? "" : "s"}. Auto-refresh is on.`;
        }
    } catch (error) {
        if (error.message.includes("Status: 403")) {
            roomPasswords.delete(activeRoomId);

            if (showStatus) {
                activeRoomId = "";
                renderRooms();
                renderMessages([]);
            }
        }

        if (showStatus) {
            output.textContent = `❌ ERROR:\n${error.message}`;
        } else {
            console.error("Auto-refresh failed", error);
        }
    } finally {
        isRefreshingMessages = false;

        if (showStatus) {
            refreshMessagesButton.disabled = false;
            refreshMessagesButton.textContent = "Refresh Messages";
        }
    }
}

function startMessagePolling() {
    stopMessagePolling();

    if (!activeRoomId || !token) {
        return;
    }

    messagePollTimerId = window.setInterval(() => {
        if (!activeRoomId || !token || getTokenExpiryState(token).status === "expired") {
            stopMessagePolling();
            return;
        }

        refreshActiveRoomMessages(false);
    }, MESSAGE_POLL_INTERVAL_MS);
}

function stopMessagePolling() {
    if (!messagePollTimerId) {
        return;
    }

    window.clearInterval(messagePollTimerId);
    messagePollTimerId = null;
}

async function sendMessage() {
    if (!ensureSignedIn()) {
        return;
    }

    if (!activeRoomId) {
        output.textContent = "❌ Join a room before sending a message.";
        return;
    }

    const content = messageInput.value.trim();

    if (!content) {
        output.textContent = "❌ Write a message first.";
        messageInput.focus();
        return;
    }

    sendMessageButton.disabled = true;
    sendMessageButton.textContent = "Sending...";

    try {
        await apiRequest(`/chat/rooms/${activeRoomId}/messages`, {
            method: "POST",
            roomPassword: roomPasswords.get(activeRoomId),
            body: JSON.stringify({ content })
        });

        messageInput.value = "";
        await refreshActiveRoomMessages(false);
        output.textContent = "✅ Message sent.";
    } catch (error) {
        output.textContent = `❌ ERROR:\n${error.message}`;
    } finally {
        sendMessageButton.disabled = false;
        sendMessageButton.textContent = "Send";
    }
}

async function apiRequest(path, options = {}) {
    const response = await fetch(`${API_GATEWAY_URL}${path}`, {
        method: options.method || "GET",
        headers: {
            "Authorization": `Bearer ${token}`,
            "Content-Type": "application/json",
            ...(options.roomPassword ? { "X-Room-Password": options.roomPassword } : {}),
            ...(options.headers || {})
        },
        body: options.body
    });

    const text = await response.text();
    let body = text;

    if (text) {
        try {
            body = JSON.parse(text);
        } catch {
            body = text;
        }
    }

    if (!response.ok) {
        const formattedBody = typeof body === "string"
            ? body
            : JSON.stringify(body, null, 2);

        throw new Error(`Status: ${response.status}\n${formattedBody}`);
    }

    return body || null;
}

function renderRooms() {
    if (!token) {
        roomList.innerHTML = '<div class="chat-empty">Sign in, then click “Load Rooms”.</div>';
        return;
    }

    if (!rooms.length) {
        stopMessagePolling();
        roomList.innerHTML = '<div class="chat-empty">No rooms loaded yet. Click “Load Rooms” or create one.</div>';
        return;
    }

    const currentUserId = getCurrentUserId();

    roomList.innerHTML = rooms.map(room => {
        const isOwner = room.createdByUserId === currentUserId;
        const lockedText = room.isPasswordProtected ? "🔒 Password protected" : "Open room";

        return `
            <div class="room-card${room.id === activeRoomId ? " active" : ""}">
                <button class="room-item" onclick="joinRoom('${room.id}')">
                    <span class="room-name">${escapeHtml(room.name)}</span>
                    <span class="room-meta">${room.id === activeRoomId ? "Joined" : "Click to join"} · ${lockedText}</span>
                    <span class="room-meta">Created ${formatDateTime(room.createdAtUtc)}</span>
                </button>
                ${isOwner ? `<button class="danger room-delete" onclick="deleteRoom('${room.id}')">Delete</button>` : ""}
            </div>
        `;
    }).join("");
}

function renderMessages(messages) {
    if (!token) {
        messageList.innerHTML = '<div class="chat-empty">Sign in to view messages.</div>';
        return;
    }

    if (!activeRoomId) {
        messageList.innerHTML = '<div class="chat-empty">Join a room to view messages.</div>';
        return;
    }

    if (!messages || messages.length === 0) {
        messageList.innerHTML = '<div class="chat-empty">No messages yet. Say hello.</div>';
        return;
    }

    messageList.innerHTML = messages.map(message => `
        <article class="message-item">
            <div class="message-author">${escapeHtml(message.senderName)}</div>
            <div class="message-content">${escapeHtml(message.content)}</div>
            <span class="message-meta">${formatDateTime(message.sentAtUtc)}</span>
        </article>
    `).join("");
    messageList.scrollTop = messageList.scrollHeight;
}

async function deleteRoom(roomId) {
    if (!ensureSignedIn()) {
        return;
    }

    const roomName = rooms.find(room => room.id === roomId)?.name || "this room";

    if (!window.confirm(`Delete room "${roomName}"? This also removes its messages.`)) {
        return;
    }

    try {
        await apiRequest(`/chat/rooms/${roomId}`, { method: "DELETE" });
        rooms = rooms.filter(room => room.id !== roomId);
        roomPasswords.delete(roomId);

        if (activeRoomId === roomId) {
            activeRoomId = "";
            stopMessagePolling();
            renderMessages([]);
        }

        renderRooms();
        output.textContent = `✅ Deleted room: ${roomName}`;
    } catch (error) {
        output.textContent = `❌ ERROR:\n${error.message}`;
    }
}

function formatChatServiceSuccess(body, elapsedSeconds) {
    const profile = getUserProfileFromToken();
    const serviceMessage = typeof body?.message === "string"
        ? body.message.replace(/^Hello\s+.*?,\s*/i, "")
        : "ChatService accepted your authenticated request.";
    const serverTime = body?.time
        ? new Date(body.time).toLocaleString()
        : "Not provided";

    return [
        "✅ ChatService is ready",
        "",
        `👤 Signed in as: ${profile.displayName}`,
        `📧 Email: ${profile.email}`,
        `💬 Chat status: ${serviceMessage}`,
        `⚡ Gateway response time: ${elapsedSeconds}s`,
        `🕒 Server time: ${serverTime}`,
        "",
        "Technical response:",
        JSON.stringify(body, null, 2)
    ].join("\n");
}

function formatRoomsSuccess(loadedRooms) {
    if (!Array.isArray(loadedRooms)) {
        return ["✅ Rooms endpoint responded", "", "Technical response:", JSON.stringify(loadedRooms, null, 2)].join("\n");
    }

    if (!loadedRooms.length) {
        return "✅ Rooms loaded\n\nNo rooms found yet.";
    }

    return [
        `✅ Loaded ${loadedRooms.length} room${loadedRooms.length === 1 ? "" : "s"}`,
        "",
        ...loadedRooms.map((room, index) => `${index + 1}. ${room.name} (${room.id})`),
        "",
        "Click any room card to join and load messages."
    ].join("\n");
}

function ensureSignedIn() {
    if (!token) {
        output.textContent = "❌ Please sign in with Google first.";
        return false;
    }

    if (getTokenExpiryState(token).status === "expired") {
        clearExpiredToken(true);
        return false;
    }

    return true;
}

function getCurrentUserId() {
    const payload = decodeJwtPayload(token);

    return payload?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]
        || payload?.sub
        || payload?.nameid
        || "";
}

function getUserProfileFromToken() {
    const payload = decodeJwtPayload(token);
    const name = payload?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"]
        || payload?.name
        || "Google user";
    const email = payload?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"]
        || payload?.email
        || "Email not available";

    return { displayName: name, email };
}

function decodeJwtPayload(jwt) {
    try {
        const payload = jwt.split(".")[1];
        const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
        const paddedBase64 = base64.padEnd(base64.length + ((4 - base64.length % 4) % 4), "=");
        const json = decodeURIComponent(atob(paddedBase64)
            .split("")
            .map((char) => `%${(`00${char.charCodeAt(0).toString(16)}`).slice(-2)}`)
            .join(""));

        return JSON.parse(json);
    } catch {
        return null;
    }
}

function handleRoomInputKey(event) {
    if (event.key === "Enter") {
        createRoom();
    }
}

function handleMessageInputKey(event) {
    if (event.key === "Enter") {
        sendMessage();
    }
}

function setChatControlsEnabled(isEnabled) {
    roomsButton.disabled = !isEnabled;
    refreshMessagesButton.disabled = !isEnabled;
    createRoomButton.disabled = !isEnabled;
    sendMessageButton.disabled = !isEnabled;
    roomNameInput.disabled = !isEnabled;
    roomPasswordInput.disabled = !isEnabled;
    messageInput.disabled = !isEnabled;
}

function formatDateTime(value) {
    return value ? new Date(value).toLocaleString() : "Unknown time";
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function escapeHtmlForAttribute(value) {
    return escapeHtml(value).replace(/`/g, "&#096;");
}

function logout() {
    localStorage.removeItem(TOKEN_KEY);
    token = "";
    rooms = [];
    activeRoomId = "";
    roomPasswords.clear();
    stopMessagePolling();
    renderRooms();
    renderMessages([]);
    renderSession();
    output.textContent = "👋 Logged out.";
}

function clearOutput() {
    output.textContent = "Ready.";
}