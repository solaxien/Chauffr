import { ChauffrWidgetElement } from "./chauffr-widget";
export { ActionExecutor } from "./action-executor";
export { SafetyGuard } from "./safety-guard";
export { RouterBridge } from "./router-bridge";

// Register custom elements
if (typeof window !== "undefined") {
  if (!customElements.get("chauffr-copilot")) {
    customElements.define("chauffr-copilot", ChauffrWidgetElement);
  }
  // Backwards compatibility alias
  if (!customElements.get("navguide-copilot")) {
    customElements.define("navguide-copilot", ChauffrWidgetElement);
  }
}
