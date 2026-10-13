/**
 * SPA Router Bridge
 * Executes soft client-side transitions and stores multi-page pending actions.
 */
export declare class RouterBridge {
    private static readonly PENDING_ACTION_KEY;
    static isCurrentPath(targetUrl: string): boolean;
    static navigateSoft(targetUrl: string, pendingActionPayload?: unknown): void;
    static consumePendingAction(): unknown | null;
}
