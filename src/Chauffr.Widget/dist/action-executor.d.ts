export interface CoBrowsingAction {
    type: string;
    tier: number;
    payload: {
        url: string;
        targetSelector?: string | null;
        scrollBehavior?: "smooth" | "auto";
        interaction?: "pulse" | "none";
        openDrawerSelector?: string | null;
        tooltipText?: string | null;
    };
    security?: {
        requiresConfirmation: boolean;
        targetHash?: string | null;
    };
}
export declare class ActionExecutor {
    private activeAbortController;
    private cleanupInterrupt;
    constructor();
    execute(action: CoBrowsingAction): Promise<void>;
    abortCurrentAction(): void;
    checkAndExecutePending(): void;
    private waitForElement;
    dispose(): void;
}
