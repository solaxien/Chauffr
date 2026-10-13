/**
 * Client-Side Action Safety Guard
 * Enforces zero-trust execution and handles immediate user interruption.
 */

export class SafetyGuard {
  private static readonly FORBIDDEN_PATTERN = 
    /(button\[type=['"]?submit['"]?\]|input|textarea|select|form|delete|destroy|remove|checkout|purchase|pay|buy|transfer|revoke)/i;

  private static readonly SAFE_SELECTOR_PATTERN = /^[a-zA-Z0-9_\-\.\#\:\(\)\[\]\=\'"" ]+$/;

  /**
   * Validates if a target selector is permitted to be queried or interacted with.
   */
  public static isSelectorSafe(selector: string | null | undefined): boolean {
    if (!selector || typeof selector !== "string") return false;
    const trimmed = selector.trim();
    if (trimmed.length === 0 || trimmed.length > 256) return false;
    if (!this.SAFE_SELECTOR_PATTERN.test(trimmed)) return false;
    if (this.FORBIDDEN_PATTERN.test(trimmed)) return false;

    return true;
  }

  /**
   * Attaches listeners for user gestures to immediately cancel autonomous animations or scrolling.
   */
  public static registerUserInterruptListener(onInterrupt: () => void): () => void {
    const handleGesture = (e: Event) => {
      // If user presses Escape or scrolls, abort
      if (e instanceof KeyboardEvent && e.key !== "Escape") return;
      onInterrupt();
    };

    window.addEventListener("wheel", handleGesture, { passive: true });
    window.addEventListener("touchstart", handleGesture, { passive: true });
    window.addEventListener("keydown", handleGesture, { passive: true });

    return () => {
      window.removeEventListener("wheel", handleGesture);
      window.removeEventListener("touchstart", handleGesture);
      window.removeEventListener("keydown", handleGesture);
    };
  }
}
