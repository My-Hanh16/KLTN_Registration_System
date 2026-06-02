const pageConfig = document.querySelector(".chat-page-wrap")?.dataset || {};
        const TOPIC_ID = Number(pageConfig.topicId || 0);
        const CURRENT_USER = pageConfig.currentUserId || "";

        let pendingFile = null;
        let typingTimer = null;
        let isTyping    = false;
        let sending     = false;
        let pendingDeleteId = null;

        // SignalR connection
        const conn = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat")
            .withAutomaticReconnect()
            .build();

        conn.on("ReceiveMessage", function (msg) {
            if (!msg || (msg.topicId && msg.topicId != TOPIC_ID)) return;
            appendMessage(msg);
            scrollToBottom();
        });

        conn.on("MessageDeleted", function (id) {
            document.getElementById("msg-" + id)?.remove();
        });

        conn.on("UserTyping", function (name) {
            const el = document.getElementById("typing-indicator");
            el.textContent = (name || "Ai đó") + " đang gõ...";
            clearTimeout(window._typingClear);
            window._typingClear = setTimeout(() => el.textContent = "", 2000);
        });

        conn.on("Error", function (message) {
            showChatNotice(message || "Có lỗi xảy ra khi xử lý tin nhắn.", "error");
        });

        conn.onreconnected(async () => {
            await conn.invoke("JoinTopicRoom", TOPIC_ID);
        });

        async function startConn() {
            try {
                await conn.start();
                await conn.invoke("JoinTopicRoom", TOPIC_ID);
            } catch {
                setTimeout(startConn, 3000);
            }
        }

        startConn();

        // Send message
        async function sendMessage() {
            if (sending) return;

            const input   = document.getElementById("msg-input");
            const content = (input.value || "").trim();
            if (!content && !pendingFile) return;

            sending = true;
            const btn = document.getElementById("btn-send");
            btn.disabled = true;
            btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang gửi...';

            try {
                await conn.invoke(
                    "SendMessage",
                    TOPIC_ID,
                    content,
                    pendingFile?.url ?? null,
                    pendingFile?.name ?? null
                );
                input.value = "";
                input.style.height = "";
                clearAttach();
            } catch {
                showChatNotice("Gửi thất bại. Vui lòng thử lại.", "error");
            } finally {
                sending = false;
                btn.disabled = false;
                btn.innerHTML = '<i class="fa-solid fa-paper-plane me-1"></i> Gửi';
                input.focus();
            }
        }

        function deleteMsg(id) {
            pendingDeleteId = id;
            document.getElementById("deleteConfirm")?.classList.add("open");
            document.getElementById("deleteConfirm")?.setAttribute("aria-hidden", "false");
        }

        function closeDeleteConfirm() {
            pendingDeleteId = null;
            document.getElementById("deleteConfirm")?.classList.remove("open");
            document.getElementById("deleteConfirm")?.setAttribute("aria-hidden", "true");
        }

        function sendTyping() {
            if (isTyping) return;
            isTyping = true;
            conn.invoke("Typing", TOPIC_ID).catch(() => {});
            clearTimeout(typingTimer);
            typingTimer = setTimeout(() => isTyping = false, 1500);
        }

        // Append new message to DOM
        function appendMessage(msg) {
            if (!msg) return;

            const isMine  = msg.senderId === CURRENT_USER;
            const initial = msg.senderName ? msg.senderName[0].toUpperCase() : "?";
            const avClass = msg.senderRole === "Lecturer" ? "lecturer" : "student";

            const attachHtml = msg.attachmentUrl
                ? `<div>
                    <a class="attachment-link" href="${escAttr(safeLocalUrl(msg.attachmentUrl))}" target="_blank" rel="noopener noreferrer">
                        <i class="fa-solid fa-paperclip"></i> ${escHtml(msg.attachmentName || "File đính kèm")}
                    </a>
                   </div>`
                : "";

            const delBtn = isMine
                ? `<button class="btn-del" type="button" data-delete-message="${msg.id}" title="Xóa">
                    <i class="fa-solid fa-xmark"></i>
                   </button>`
                : "";

            const row = document.createElement("div");
            row.className = "msg-row" + (isMine ? " mine" : "");
            row.id = "msg-" + msg.id;
            row.innerHTML = `
                <div class="avatar ${avClass}">${initial}</div>
                <div class="bubble-wrap">
                    <div class="sender-name">${escHtml(msg.senderName || "")}</div>
                    <div class="bubble ${isMine ? "mine" : "them"}">
                        ${msg.content ? `<span>${escHtml(msg.content)}</span>` : ""}
                        ${attachHtml}
                        ${delBtn}
                    </div>
                    <div class="bubble-time">${msg.createdAtShort || ""}</div>
                </div>`;

            document.getElementById("chat-messages").appendChild(row);
        }

        // Load more
        document.getElementById("load-more-btn")?.addEventListener("click", async function () {
            const btn     = this;
            const firstId = parseInt(btn.dataset.firstId || "0");

            btn.disabled  = true;
            btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Đang tải...';

            let msgs = [];
            try {
                const res = await fetch(`/Chat/LoadMore?topicId=${TOPIC_ID}&beforeId=${firstId}`);
                if (!res.ok) throw new Error();
                msgs = await res.json();
            } catch {
                showChatNotice("Không thể tải thêm tin nhắn cũ.", "error");
                btn.disabled = false;
                btn.innerHTML = '<i class="fa-solid fa-chevron-up"></i> Tải thêm tin nhắn cũ';
                return;
            }

            if (!msgs || msgs.length === 0) {
                btn.remove();
                return;
            }

            const container = document.getElementById("chat-messages");
            const oldHeight = container.scrollHeight;

            msgs.forEach(msg => {
                const isMine = msg.senderId === CURRENT_USER;
                const row = document.createElement("div");
                row.className = "msg-row" + (isMine ? " mine" : "");
                row.id = "msg-" + msg.id;
                row.innerHTML = `
                    <div class="avatar ${msg.senderRole === "Lecturer" ? "lecturer" : "student"}">
                        ${(msg.senderName || "?")[0]}
                    </div>
                    <div class="bubble-wrap">
                        <div class="sender-name">${escHtml(msg.senderName || "")}</div>
                        <div class="bubble ${isMine ? "mine" : "them"}">
                            ${msg.content ? `<span>${escHtml(msg.content)}</span>` : ""}
                            ${msg.attachmentUrl ? `<div>
                                <a class="attachment-link" href="${escAttr(safeLocalUrl(msg.attachmentUrl))}" target="_blank" rel="noopener noreferrer">
                                    <i class="fa-solid fa-paperclip"></i> ${escHtml(msg.attachmentName || "File đính kèm")}
                                </a>
                            </div>` : ""}
                        </div>
                        <div class="bubble-time">${msg.createdAtFmt || ""}</div>
                    </div>`;
                btn.insertAdjacentElement("afterend", row);
            });

            btn.dataset.firstId = msgs[0].id;
            container.scrollTop = container.scrollHeight - oldHeight;
            btn.disabled = false;
            btn.innerHTML = '<i class="fa-solid fa-chevron-up"></i> Tải thêm tin nhắn cũ';
        });

        // File upload
        async function handleFileSelect(input) {
            const file = input.files[0];
            if (!file) return;

            const maxSize = 10 * 1024 * 1024;
            if (file.size > maxSize) {
                showChatNotice("File tối đa 10MB.", "error");
                input.value = "";
                return;
            }

            const formData = new FormData();
            formData.append("file", file);
            formData.append("topicId", TOPIC_ID);

            document.getElementById("attach-preview")?.classList.remove("is-hidden");
            document.getElementById("attach-name").textContent = "Đang tải lên...";

            let res;
            let data = {};
            try {
                res  = await fetch("/Chat/UploadFile", {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? ""
                    },
                    body: formData
                });
                data = await res.json();
            } catch {
                data = { error: "Không thể tải file lên. Vui lòng thử lại." };
            }

            if (!res || !res.ok || data.error) {
                showChatNotice(data.error || "Không thể tải file lên. Vui lòng thử lại.", "error");
                clearAttach();
                return;
            }

            pendingFile = { url: data.url, name: data.name };
            document.getElementById("attach-name").textContent = data.name;
        }

        function clearAttach() {
            pendingFile = null;
            document.getElementById("attach-preview")?.classList.add("is-hidden");
            const fileInput = document.getElementById("file-input");
            if (fileInput) fileInput.value = "";
        }

        function handleKey(e) {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        }

        function autoResize(el) {
            el.style.height = "";
            el.style.height = Math.min(el.scrollHeight, 120) + "px";
        }

        function scrollToBottom() {
            const el = document.getElementById("chat-messages");
            el.scrollTop = el.scrollHeight;
        }

        function escHtml(str) {
            if (!str) return "";
            return String(str)
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#39;");
        }

        function escAttr(str) {
            return escHtml(str).replace(/"/g, "&quot;");
        }

        function safeLocalUrl(url) {
            if (!url || typeof url !== "string") return "#";
            const value = url.trim();
            if (!value.startsWith("/") || value.startsWith("//") || value.includes("\\")) return "#";
            return value;
        }

        function showChatNotice(message, type = "success") {
            const notice = document.getElementById("chatNotice");
            if (!notice) return;

            notice.textContent = message;
            notice.className = "chat-notice show " + type;
            clearTimeout(window._chatNoticeTimer);
            window._chatNoticeTimer = setTimeout(() => {
                notice.classList.remove("show");
            }, 3500);
        }


        document.getElementById("file-input")?.addEventListener("change", function () {
            handleFileSelect(this);
        });

        document.getElementById("msg-input")?.addEventListener("keydown", handleKey);

        document.getElementById("msg-input")?.addEventListener("input", function () {
            autoResize(this);
            sendTyping();
        });

        document.getElementById("btn-send")?.addEventListener("click", sendMessage);

        document.getElementById("clearAttachBtn")?.addEventListener("click", clearAttach);

        document.getElementById("chat-messages")?.addEventListener("click", (event) => {
            const button = event.target.closest("[data-delete-message]");
            if (!button) return;
            deleteMsg(parseInt(button.dataset.deleteMessage || "0"));
        });

        document.getElementById("cancelDeleteBtn")?.addEventListener("click", closeDeleteConfirm);

        document.getElementById("deleteConfirm")?.addEventListener("click", (event) => {
            if (event.target.id === "deleteConfirm") {
                closeDeleteConfirm();
            }
        });

        document.getElementById("confirmDeleteBtn")?.addEventListener("click", async () => {
            if (!pendingDeleteId) return;

            const id = pendingDeleteId;
            closeDeleteConfirm();

            try {
                await conn.invoke("DeleteMessage", id);
            } catch {
                showChatNotice("Không thể xóa tin nhắn. Vui lòng thử lại.", "error");
            }
        });

        scrollToBottom();
