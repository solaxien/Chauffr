/**
 * Client-Side Action Safety Guard
 * Enforces zero-trust execution and handles immediate user interruption.
 */
export declare class SafetyGuard {
    private static readonly FORBIDDEN_PATTERN;
    private static readonly SAFE_SELECTOR_PATTERN;
    /**
     * Validates if a target selector is permitted to be queried or interacted with.
     */
    static isSelectorSafe(selector: string | null | undefined): boolean;
    /**
     * Attaches listeners for user gestures to immediately cancel autonomous animations or scrolling.
     */
    static registerUserInterruptListener(onInterrupt: () => void): () => void;
}
