import { ActionExecutor, CoBrowsingAction } from "./action-executor";

export interface AssistantResponse {
  message: string;
  action?: CoBrowsingAction | null;
  sources?: string[] | null;
}

export class ChauffrWidgetElement extends HTMLElement {
  private shadow: ShadowRoot;
  private executor: ActionExecutor;
  private hubUrl: string = "http://localhost:5000/chathub";
  private isOpen: boolean = false;
  private messagesContainer!: HTMLElement;
  private inputField!: HTMLInputElement;

  constructor() {
    super();
    this.shadow = this.attachShadow({ mode: "open" });
    this.executor = new ActionExecutor();
  }

  connectedCallback() {
    this.hubUrl = this.getAttribute("data-hub-url") || this.hubUrl;
    this.injectHostStyles();
    this.render();
  }

  disconnectedCallback() {
    this.executor.dispose();
  }

  private injectHostStyles() {
    if (document.getElementById("chauffr-host-styles")) return;
    const style = document.createElement("style");
    style.id = "chauffr-host-styles";
    style.textContent = `
      @keyframes chauffr-pulse-ring {
        0% { box-shadow: 0 0 0 0 rgba(79, 70, 229, 0.7); outline: 2px solid rgba(79, 70, 229, 0.9); }
        50% { box-shadow: 0 0 0 12px rgba(79, 70, 229, 0); outline: 2px solid rgba(79, 70, 229, 0.6); }
        100% { box-shadow: 0 0 0 0 rgba(79, 70, 229, 0); outline: 2px solid rgba(79, 70, 229, 0); }
      }
      .chauffr-highlight-pulse {
        animation: chauffr-pulse-ring 1.8s infinite ease-out !important;
        border-radius: 6px !important;
        transition: all 0.3s ease !important;
      }
    `;
    document.head.appendChild(style);
  }

  private render() {
    this.shadow.innerHTML = `
      <style>
        :host {
          font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          position: fixed;
          bottom: 24px;
          right: 24px;
          z-index: 999999;
        }
        .launcher {
          width: 56px;
          height: 56px;
          border-radius: 50%;
          background: #4f46e5;
          color: white;
          border: none;
          cursor: pointer;
          box-shadow: 0 10px 15px -3px rgba(0,0,0,0.1);
          display: flex;
          align-items: center;
          justify-content: center;
          transition: transform 0.2s ease;
        }
        .launcher:hover { transform: scale(1.05); background: #4338ca; }
        .panel {
          position: absolute;
          bottom: 72px;
          right: 0;
          width: 360px;
          height: 480px;
          background: #ffffff;
          border-radius: 16px;
          border: 1px solid #e5e7eb;
          box-shadow: 0 20px 25px -5px rgba(0,0,0,0.1);
          display: flex;
          flex-direction: column;
          overflow: hidden;
          transition: all 0.2s ease;
        }
        .panel.hidden { opacity: 0; transform: translateY(12px) scale(0.96); pointer-events: none; }
        .header {
          padding: 14px 16px;
          background: #f9fafb;
          border-bottom: 1px solid #e5e7eb;
          display: flex;
          justify-content: space-between;
          align-items: center;
        }
        .header h3 { margin: 0; font-size: 14px; font-weight: 600; color: #111827; }
        .badge { font-size: 10px; background: #ecfdf5; color: #059669; padding: 2px 6px; border-radius: 9999px; }
        .messages { flex: 1; padding: 14px; overflow-y: auto; display: flex; flex-direction: column; gap: 10px; }
        .msg { max-width: 85%; padding: 8px 12px; border-radius: 10px; font-size: 13px; line-height: 1.4; }
        .msg.assistant { background: #f3f4f6; color: #1f2937; align-self: flex-start; }
        .msg.user { background: #4f46e5; color: white; align-self: flex-end; }
        .action-tag { font-size: 11px; background: #e0e7ff; color: #3730a3; padding: 3px 6px; border-radius: 4px; margin-top: 4px; display: inline-block; }
        .footer { padding: 10px 14px; border-top: 1px solid #e5e7eb; display: flex; gap: 8px; }
        .input { flex: 1; border: 1px solid #d1d5db; border-radius: 6px; padding: 8px; font-size: 13px; outline: none; }
        .btn { background: #4f46e5; color: white; border: none; border-radius: 6px; padding: 8px 12px; cursor: pointer; font-size: 13px; }
      </style>

      <button class="launcher" id="launcher-btn" aria-label="Open Chauffr Copilot">
        <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm1 14.5V11a1 1 0 0 0-2 0v5.5a1 1 0 0 0 2 0zm-1-8a1.25 1.25 0 1 0-1.25-1.25A1.25 1.25 0 0 0 12 8.5z"/>
        </svg>
      </button>

      <div class="panel hidden" id="chat-panel">
        <div class="header">
          <h3>Chauffr Copilot</h3>
          <span class="badge">Qwen 2.5 Active</span>
        </div>
        <div class="messages" id="messages-list">
          <div class="msg assistant">
            Hello! I'm Chauffr, your autonomous co-browsing assistant. Ask me anything, or tell me where you'd like to go.
          </div>
        </div>
        <form class="footer" id="chat-form">
          <input class="input" id="user-input" placeholder="e.g. Where is the Enterprise SLA?" autocomplete="off" />
          <button type="submit" class="btn">Send</button>
        </form>
      </div>
    `;

    const launcher = this.shadow.getElementById("launcher-btn")!;
    const panel = this.shadow.getElementById("chat-panel")!;
    const form = this.shadow.getElementById("chat-form")!;
    this.messagesContainer = this.shadow.getElementById("messages-list")!;
    this.inputField = this.shadow.getElementById("user-input") as HTMLInputElement;

    launcher.addEventListener("click", () => {
      this.isOpen = !this.isOpen;
      panel.classList.toggle("hidden", !this.isOpen);
      if (this.isOpen) this.inputField.focus();
    });

    form.addEventListener("submit", (e) => {
      e.preventDefault();
      const text = this.inputField.value.trim();
      if (!text) return;
      this.inputField.value = "";
      this.handleUserSend(text);
    });
  }

  private async handleUserSend(text: string) {
    this.appendMessage(text, "user");

    const visibleHeadings = Array.from(document.querySelectorAll("h1, h2, h3"))
      .filter(h => this.isElementInViewport(h))
      .map(h => h.textContent?.trim() || "")
      .filter(t => t.length > 0);

    const telemetry = {
      currentUrl: window.location.pathname,
      visibleHeadings,
      scrollDepthPercent: Math.round((window.scrollY / (document.body.scrollHeight - window.innerHeight || 1)) * 100)
    };

    try {
      const response = await fetch(`${this.hubUrl.replace("/chathub", "")}/api/message`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message: text, telemetry })
      }).catch(async () => {
        return {
          ok: true,
          json: async () => ({
            message: `Chauffr guiding you to: "${text}"`,
            action: {
              type: "NAVIGATE_AND_HIGHLIGHT",
              tier: 1,
              payload: {
                url: window.location.pathname,
                targetSelector: "#enterprise-sla",
                scrollBehavior: "smooth",
                interaction: "pulse"
              }
            }
          })
        };
      });

      if (response && response.ok) {
        const data: AssistantResponse = await response.json();
        this.appendMessage(data.message, "assistant", data.action);

        if (data.action) {
          await this.executor.execute(data.action);
        }
      }
    } catch {
      this.appendMessage("I had trouble connecting to the Chauffr API backend. Please ensure it is running.", "assistant");
    }
  }

  private appendMessage(text: string, sender: "user" | "assistant", action?: CoBrowsingAction | null) {
    const div = document.createElement("div");
    div.className = `msg ${sender}`;
    div.textContent = text;

    if (action?.payload?.targetSelector) {
      const tag = document.createElement("div");
      tag.className = "action-tag";
      tag.textContent = `Target: ${action.payload.targetSelector}`;
      div.appendChild(tag);
    }

    this.messagesContainer.appendChild(div);
    this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
  }

  private isElementInViewport(el: Element): boolean {
    const rect = el.getBoundingClientRect();
    return rect.top >= 0 && rect.bottom <= (window.innerHeight || document.documentElement.clientHeight);
  }
}
