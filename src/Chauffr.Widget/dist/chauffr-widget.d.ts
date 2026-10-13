import { CoBrowsingAction } from "./action-executor";
export interface AssistantResponse {
    message: string;
    action?: CoBrowsingAction | null;
    sources?: string[] | null;
}
export declare class ChauffrWidgetElement extends HTMLElement {
    private shadow;
    private executor;
    private hubUrl;
    private isOpen;
    private messagesContainer;
    private inputField;
    constructor();
    connectedCallback(): void;
    disconnectedCallback(): void;
    private injectHostStyles;
    private render;
    private handleUserSend;
    private appendMessage;
    private isElementInViewport;
}
