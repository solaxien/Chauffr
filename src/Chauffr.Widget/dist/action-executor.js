import { SafetyGuard } from "./safety-guard";
import { RouterBridge } from "./router-bridge";
export class ActionExecutor {
    activeAbortController = null;
    cleanupInterrupt = null;
    constructor() {
        this.cleanupInterrupt = SafetyGuard.registerUserInterruptListener(() => this.abortCurrentAction());
        // Process any action left over from a previous page navigation
        this.checkAndExecutePending();
    }
    async execute(action) {
        this.abortCurrentAction();
        this.activeAbortController = new AbortController();
        const { signal } = this.activeAbortController;
        const { payload } = action;
        // 1. Check if navigation to another page is required
        if (!RouterBridge.isCurrentPath(payload.url)) {
            RouterBridge.navigateSoft(payload.url, action);
            return;
        }
        // 2. Validate target selector
        if (!payload.targetSelector || !SafetyGuard.isSelectorSafe(payload.targetSelector)) {
            return;
        }
        // 3. Resolve target element with MutationObserver (handles async render/hydration)
        const element = await this.waitForElement(payload.targetSelector, 5000, signal);
        if (!element || signal.aborted)
            return;
        // 4. Smooth scroll to target
        element.scrollIntoView({
            behavior: payload.scrollBehavior || "smooth",
            block: "center",
            inline: "nearest"
        });
        // 5. Visual pulse highlight
        if (payload.interaction !== "none") {
            element.classList.add("chauffr-highlight-pulse");
            const timer = window.setTimeout(() => {
                element.classList.remove("chauffr-highlight-pulse");
            }, 4000);
            signal.addEventListener("abort", () => {
                clearTimeout(timer);
                element.classList.remove("chauffr-highlight-pulse");
            });
        }
        // 6. Safe Read-Only Drawer / Modal Expansion (if specified and safe)
        if (payload.openDrawerSelector && SafetyGuard.isSelectorSafe(payload.openDrawerSelector)) {
            const drawer = document.querySelector(payload.openDrawerSelector);
            if (drawer && !signal.aborted) {
                drawer.click();
            }
        }
    }
    abortCurrentAction() {
        if (this.activeAbortController) {
            this.activeAbortController.abort();
            this.activeAbortController = null;
        }
        document.querySelectorAll(".chauffr-highlight-pulse").forEach(el => {
            el.classList.remove("chauffr-highlight-pulse");
        });
    }
    checkAndExecutePending() {
        const pending = RouterBridge.consumePendingAction();
        if (pending && pending.payload) {
            setTimeout(() => this.execute(pending), 300);
        }
    }
    waitForElement(selector, timeoutMs, signal) {
        return new Promise((resolve) => {
            const immediate = document.querySelector(selector);
            if (immediate)
                return resolve(immediate);
            const observer = new MutationObserver(() => {
                const found = document.querySelector(selector);
                if (found) {
                    observer.disconnect();
                    resolve(found);
                }
            });
            observer.observe(document.body, { childList: true, subtree: true });
            const timeout = window.setTimeout(() => {
                observer.disconnect();
                resolve(null);
            }, timeoutMs);
            signal.addEventListener("abort", () => {
                clearTimeout(timeout);
                observer.disconnect();
                resolve(null);
            });
        });
    }
    dispose() {
        this.abortCurrentAction();
        this.cleanupInterrupt?.();
    }
}
