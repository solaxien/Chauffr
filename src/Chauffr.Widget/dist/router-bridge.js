/**
 * SPA Router Bridge
 * Executes soft client-side transitions and stores multi-page pending actions.
 */
export class RouterBridge {
    static PENDING_ACTION_KEY = "CHAUFFR_PENDING_ACTION";
    static isCurrentPath(targetUrl) {
        const current = window.location.pathname;
        const target = targetUrl.split("#")[0].split("?")[0];
        return current === target || current === target + "/";
    }
    static navigateSoft(targetUrl, pendingActionPayload) {
        if (pendingActionPayload) {
            window.sessionStorage.setItem(this.PENDING_ACTION_KEY, JSON.stringify(pendingActionPayload));
        }
        try {
            // 1. Attempt HTML5 History API soft push
            window.history.pushState({}, "", targetUrl);
            window.dispatchEvent(new PopStateEvent("popstate"));
            // Custom event for host frameworks (Next.js, React Router, Vue)
            window.dispatchEvent(new CustomEvent("chauffr:route-change", { detail: { url: targetUrl } }));
        }
        catch {
            // 2. Fallback to standard location assign
            window.location.assign(targetUrl);
        }
    }
    static consumePendingAction() {
        try {
            const raw = window.sessionStorage.getItem(this.PENDING_ACTION_KEY);
            if (raw) {
                window.sessionStorage.removeItem(this.PENDING_ACTION_KEY);
                return JSON.parse(raw);
            }
        }
        catch {
            // Ignore storage errors
        }
        return null;
    }
}
