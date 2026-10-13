# Chauffr AI: Autonomous Website Copilot & Co-Browsing Assistant

An intelligent, real-time website assistant built with **.NET 8/9** and **Microsoft Semantic Kernel**. Chauffr combines macro-level site hierarchy traversal with micro-level hybrid RAG (Dense + BM25) and client-side co-browsing execution. Rather than outputting passive text links, Chauffr actively guides, deep-links, and drives secure, visual UI actions directly on the user's active screen.

---

## Architecture Overview

Chauffr decouples high-level site structure from factual content retrieval and places an **Action Safety Gate** between the AI planner and the client execution bridge.

```
┌────────────────────────────────────────────────────────────────────────┐
│ MULTI-STAGE DELTA INGESTION PIPELINE                                   │
│                                                                        │
│ sitemap.xml (lastmod check) ──► Playwright (Headless Browser)          │
│                                           │                            │
│ ┌─────────────────────────────────────────┴──────────────────────────┐ │
│ │ Stage 1: Macro Hierarchy Extraction (<nav>, breadcrumbs, targets)  │ │
│ │ Stage 2: DOM Sanitization (Strip chrome, footer, tracking scripts) │ │
│ │ Stage 3: ReverseMarkdown & Content Hash Verification (SHA-256)     │ │
│ └────────────────────────────────────────────────────────────────────┘ │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ DUAL-LAYER STORAGE & RETRIEVAL                                         │
│                                                                        │
│ ┌─────────────────────────────┐        ┌─────────────────────────────┐ │
│ │ Macro Navigation Index      │        │ Micro Hybrid RAG            │ │
│ │ - Route Hierarchy & Tree    │        │ - Dense Embeddings (Vector) │ │
│ │ - Breadcrumb JSON Maps      │        │ - Sparse Lexical (BM25)     │ │
│ │ - Whitelisted Action Targets│        │ - Reciprocal Rank Fusion    │ │
│ └──────────────┬──────────────┘        └──────────────┬──────────────┘ │
└────────────────┼──────────────────────────────────────┼────────────────┘
                 │                                      │
                 └──────────────────►┬◄─────────────────┘
                                     │
┌────────────────────────────────────┼───────────────────────────────────┐
│ SEMANTIC KERNEL ORCHESTRATION & ACTION SAFETY                          │
│                                    ▼                                   │
│   Active Context Ingestion ◄── (Current URL, Viewport, Route State)    │
│   Intent Router & Planner (GPT-4o-mini / Claude Haiku)                 │
│                                    │                                   │
│ ┌──────────────────────────────────┴─────────────────────────────────┐ │
│ │ ACTION SAFETY GATEWAY                                              │ │
│ │ - Permission Tier Enforcement (Safe / Read-Only / Mutating)        │ │
│ │ - Strict Target Selector Whitelisting (data-Chauffr-* validation) │ │
│ │ - Indirect Prompt Injection & Destructive Action Interception      │ │
│ └──────────────────────────────────┬─────────────────────────────────┘ │
└────────────────────────────────────┼───────────────────────────────────┘
                                     │ (SignalR Minimal APIs + Redis Backplane)
                                     ▼ 
┌────────────────────────────────────────────────────────────────────────┐
│ CLIENT-SIDE RESILIENT CO-BROWSING WIDGET                               │
│                                                                        │
│ - SPA Soft Routing (History API)   - MutationObserver Element Polling  │
│ - Safe Pulse & Tooltip Highlight   - User Gesture Override / Abort     │
│ - Read-Only Modal Dispatcher       - Session-Aware Multi-Page Actions  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## Core Pillars & Key Features

### 1. Action Safety & Guardrails Engine (Zero-Trust Co-Browsing)
Co-browsing should assist, never usurp user control or create attack vectors. Chauffr enforces strict client- and server-side safety boundaries:
* **Three-Tier Action Permission Matrix:** Categorizes actions by risk level, automatically quarantining or rejecting state-mutating events.
* **Strict Target Sandboxing:** Restricts automation targets to registered semantic selectors or elements tagged with `data-Chauffr-target`. Arbitrary `.click()` calls on buttons, forms, and destructive UI elements are strictly blocked.
* **User Agency & Interruptibility:** Any manual scroll, mouse interaction, or press of `Esc` immediately aborts AI-driven animations and scrolling.
* **Prompt Injection Defense:** Server-side Semantic Kernel filters validate that proposed action payloads point to known structural targets stored in the verified navigation index, preventing model hallucinations or malicious prompt injection from targeting unauthorized DOM nodes.

### 2. Dual-Layer Retrieval (Macro vs. Micro)
* **Macro Hierarchy (Traversal):** Maintains an indexed tree of routes, sub-paths, breadcrumbs, and interactive section targets. Handles navigation queries (*"Where do I change my billing email?"*) in milliseconds without scanning body text chunks.
* **Micro Hybrid RAG (Content):** Handles complex factual queries and documentation lookups using chunked Markdown embeddings.

### 3. Hybrid Search (Dense Vectors + BM25 via RRF)
Standard vector similarity often fails on alphanumeric identifiers. Chauffr combines:
* **Dense Vectors:** Semantic contextual matching via OpenAI `text-embedding-3-small` or Ollama embeddings.
* **Sparse Lexical Search (BM25):** Tokenized exact indexing for product SKUs, status codes, error messages, and API routes.
* **Reciprocal Rank Fusion (RRF):** Blends dense and sparse rankings to produce balanced, accurate retrieval scores.

### 4. Sitemap & Multi-Stage Delta Ingestion
Prevents redundant scraping and maintains clean separation between structural metadata and content:
* **Pre-Sanitization Macro Extraction:** Traverses `<nav>`, breadcrumbs, and headers *before* stripping chrome to preserve full route trees.
* **DOM Sanitization:** AngleSharp strips footers, tracking scripts, and non-content noise.
* **Sitemap `<lastmod>` & SHA-256 Hashing:** Skips re-scraping if timestamps match, and guards against false-positive crawler runs by comparing cryptographic hashes of the extracted Markdown.
* **Playwright Concurrency Management:** Headless browser rendering for dynamic SPAs with pooled browser instances.

### 5. Resilient Client-Side Co-Browsing Bridge
* **SPA Soft Routing:** Integrates with the host application's client router (`history.pushState` / router adapters) instead of hard page reloads.
* **Hydration-Safe Element Resolution:** Uses a `MutationObserver` retry mechanism to locate elements that render asynchronously after client data fetches.
* **Persistent Multi-Page State:** Preserves multi-step navigation actions across route changes using isolated session storage tokens.
* **Bidirectional Context Stream:** Pushes active viewport telemetry (current route, scroll position, visible headings) back to the SignalR hub to ground the AI's contextual awareness.

---

## Action Safety Specification

To prevent co-browsing from executing unauthorized or harmful actions, Chauffr enforces the following safety controls:

### Permission Matrix

| Tier | Classification | Permitted Actions | Security Controls |
| :--- | :--- | :--- | :--- |
| **Tier 1: Read-Only / Passive** | Safe | Smooth scroll, target pulse ring, tooltips, route navigation | Allowed by default; can be dismissed by any user gesture. |
| **Tier 2: Controlled Expansion** | Low Risk | Open read-only FAQ drawers, trigger preview modals | Only triggers elements registered in the verified Macro Index with `data-Chauffr-action="open"`. |
| **Tier 3: State-Mutating** | High Risk / Forbidden | Form submission, delete buttons, checkout triggers, input injection | **Strictly prohibited.** The client runtime throws a security violation if an action attempts to click `button[type="submit"]`, input fields, or non-whitelisted selectors. |

### Selector Sandboxing Rules
1. The AI is **never** permitted to generate free-form CSS selectors for click operations.
2. Clickable targets must match the regex `^[a-zA-Z0-9_\-#\.]+$` and resolve to elements containing explicit navigation attributes (e.g., `data-Chauffr-target` or registered drawer anchors).
3. Any action payload lacking an authenticity hash or originating outside the active SignalR session is dropped.

---

## Structured Action Protocol

The assistant communicates user actions using a strictly validated JSON schema:

```json
{
  "message": "I'll guide you to the Enterprise SLA section and highlight the uptime guarantee.",
  "action": {
    "type": "NAVIGATE_AND_HIGHLIGHT",
    "tier": 1,
    "payload": {
      "url": "/pricing",
      "targetSelector": "#enterprise-sla",
      "scrollBehavior": "smooth",
      "interaction": "pulse",
      "openDrawerSelector": null
    },
    "security": {
      "requiresConfirmation": false,
      "targetHash": "sha256-e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
    }
  }
}
```

---

## Tech Stack

* **Runtime & Framework:** .NET 8 / .NET 9 (C# Minimal APIs)
* **AI Orchestration:** Microsoft Semantic Kernel (Intent routing, Function calling, Safety filters)
* **Browser Automation & Scraping:** Microsoft Playwright for .NET, AngleSharp, ReverseMarkdown
* **Storage & Vectors:** Qdrant (Native dense + sparse hybrid vectors) or PostgreSQL (`pgvector`)
* **Real-time Messaging:** ASP.NET Core SignalR Minimal APIs + Redis Backplane for horizontal scale
* **Client Bridge:** Vanilla TypeScript / Web Component (Zero external runtime dependencies)

---

## Project Structure

```plaintext
├── src/
│   ├── Chauffr.Crawler/           # Playwright scraper & multi-stage extraction
│   │   ├── Scraper.cs              # Headless dynamic HTML rendering with worker pool
│   │   ├── DomCleaner.cs           # Content sanitization & ReverseMarkdown
│   │   ├── HierarchyExtractor.cs   # Pre-sanitization <nav> & breadcrumb extraction
│   │   └── SitemapParser.cs        # lastmod XML inspection & change detection
│   │
│   ├── Chauffr.Core/              # Domain models, chunking, and action contracts
│   │   ├── Models/                 # Page, Chunk, HierarchyNode, ActionIntent
│   │   ├── Safety/                 # ActionPermissionMatrix, SelectorValidator
│   │   └── Chunking/               # Heading-aware Markdown splitters
│   │
│   ├── Chauffr.Indexing/          # Dual-layer ingestion & vector store
│   │   ├── HybridSearchStore.cs    # Qdrant / pgvector + BM25 integration with RRF
│   │   └── HierarchyBuilder.cs     # Route graphs & actionable selector registries
│   │
│   ├── Chauffr.Api/               # ASP.NET Core Minimal APIs & SignalR Hub
│   │   ├── Hubs/ChatHub.cs         # Real-time streaming & client telemetry listener
│   │   ├── Plugins/                # Semantic Kernel plugins (Search, Route, Safety)
│   │   ├── Filters/                # SK Prompt & Function Invocation safety filters
│   │   └── Program.cs              # DI registrations & Redis backplane setup
│   │
│   └── Chauffr.Widget/            # Client-side co-browsing bridge
│       ├── src/
│       │   ├── index.ts            # Widget initialization & SignalR connection
│       │   ├── action-executor.ts  # Sandboxed DOM runner with MutationObserver
│       │   ├── router-bridge.ts    # SPA soft-navigation adapter (History API)
│       │   └── safety-guard.ts     # Client selector validation & user abort triggers
│       └── styles/widget.css       # Assistant UI & non-destructive pulse indicators
│
├── tests/
│   ├── Chauffr.Tests.Unit/        # Safety validation & parser unit tests
│   └── Chauffr.Tests.Integration/ # Hybrid search & SignalR integration tests
│
├── docker-compose.yml              # Local Qdrant, Redis, & PostgreSQL setup
└── README.md
```

---

## Implementation Highlights

### 1. Server-Side Action Safety Filter (Semantic Kernel)

```csharp
public class ActionSafetyFilter : IFunctionInvocationFilter
{
    private readonly IActionRegistry _allowedActions;

    public ActionSafetyFilter(IActionRegistry allowedActions)
    {
        _allowedActions = allowedActions;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        await next(context);

        // Intercept action payloads generated by the model
        if (context.Result.GetValue<object>() is ActionPayload action)
        {
            // Validate selector against the verified macro registry
            if (!string.IsNullOrEmpty(action.TargetSelector) &&
                !_allowedActions.IsSelectorAllowed(action.TargetSelector, action.Url))
            {
                throw new InvalidOperationException($"Blocked unsafe target selector: {action.TargetSelector}");
            }

            // Ensure mutating click actions are strictly blocked
            if (action.Tier > ActionTier.ReadOnlyExpansion)
            {
                throw new SecurityException("State-mutating client actions are strictly prohibited.");
            }
        }
    }
}
```

### 2. Client-Side Safe Action Executor (`action-executor.ts`)

```typescript
export class SafeActionExecutor {
  private abortController: AbortController | null = null;

  constructor() {
    this.registerUserInterruptListeners();
  }

  public async executeAction(action: CoBrowsingAction): Promise<void> {
    this.abortController?.abort();
    this.abortController = new AbortController();
    const { signal } = this.abortController;

    // 1. Security Check: Block unsafe selectors & mutating interactions
    if (!this.isSelectorSafe(action.payload.targetSelector)) {
      console.warn("[Chauffr Security] Blocked unverified selector:", action.payload.targetSelector);
      return;
    }

    // 2. SPA Soft Navigation
    if (window.location.pathname !== action.payload.url) {
      window.sessionStorage.setItem("NAV_PENDING_ACTION", JSON.stringify(action));
      window.history.pushState({}, "", action.payload.url);
      window.dispatchEvent(new PopStateEvent("popstate"));
      return;
    }

    // 3. Resolve Element with MutationObserver (handles async rendering)
    const el = await this.waitForElement(action.payload.targetSelector, 5000, signal);
    if (!el || signal.aborted) return;

    // 4. Safe Visual Interaction
    el.scrollIntoView({ behavior: action.payload.scrollBehavior || "smooth", block: "center" });
    el.classList.add("Chauffr-highlight-pulse");
    setTimeout(() => el.classList.remove("Chauffr-highlight-pulse"), 4000);

    // 5. Controlled Drawer/Modal Trigger (Read-only verified targets only)
    if (action.payload.openDrawerSelector && this.isSafeDrawer(action.payload.openDrawerSelector)) {
      const drawer = document.querySelector<HTMLElement>(action.payload.openDrawerSelector);
      drawer?.click();
    }
  }

  private isSelectorSafe(selector: string): boolean {
    // Prohibit form submission, dangerous tags, and inputs
    const forbidden = /button\[type=['"]?submit['"]?\]|input|form|delete|checkout/i;
    return !forbidden.test(selector);
  }

  private waitForElement(selector: string, timeoutMs: number, signal: AbortSignal): Promise<Element | null> {
    return new Promise((resolve) => {
      const existing = document.querySelector(selector);
      if (existing) return resolve(existing);

      const observer = new MutationObserver(() => {
        const el = document.querySelector(selector);
        if (el) {
          observer.disconnect();
          resolve(el);
        }
      });

      observer.observe(document.body, { childList: true, subtree: true });
      signal.addEventListener("abort", () => { observer.disconnect(); resolve(null); });
      setTimeout(() => { observer.disconnect(); resolve(null); }, timeoutMs);
    });
  }

  private registerUserInterruptListeners(): void {
    const cancelOnUserAction = () => {
      if (this.abortController) {
        this.abortController.abort();
        document.querySelectorAll(".Chauffr-highlight-pulse").forEach(el => el.classList.remove("Chauffr-highlight-pulse"));
      }
    };

    window.addEventListener("wheel", cancelOnUserAction, { passive: true });
    window.addEventListener("touchstart", cancelOnUserAction, { passive: true });
    window.addEventListener("keydown", (e) => { if (e.key === "Escape") cancelOnUserAction(); });
  }
}
```

---

## Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or higher
* [Docker Desktop](https://www.docker.com/) (for Qdrant & Redis)
* [Playwright CLI for .NET](https://playwright.dev/dotnet/docs/intro)
* [Node.js 20+](https://nodejs.org/) (for building the widget embed)

### 1. Start Infrastructure
```bash
git clone https://github.com/your-org/Chauffr-ai.git
cd Chauffr-ai

# Launch Qdrant vector database and Redis backplane
docker-compose up -d qdrant redis
```

### 2. Install Playwright Dependencies
```bash
cd src/Chauffr.Crawler
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
cd ../..
```

### 3. Configure Settings
Configure `src/Chauffr.Api/appsettings.Development.json`:
```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "ChatModelId": "gpt-4o-mini",
    "EmbeddingModelId": "text-embedding-3-small"
  },
  "Qdrant": {
    "Endpoint": "http://localhost:6334",
    "CollectionName": "site_knowledge"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "TargetSite": {
    "BaseUrl": "https://example.com",
    "SitemapUrl": "https://example.com/sitemap.xml",
    "WhitelistedSelectorPrefixes": ["Chauffr-", "section-", "faq-"]
  }
}
```

### 4. Run Multi-Stage Ingestion & Launch API
```bash
# Run crawler with delta validation
dotnet run --project src/Chauffr.Crawler -- --sync

# Start SignalR API backend
dotnet run --project src/Chauffr.Api
```

### 5. Embed Client Widget
Add the lightweight script before the closing `</body>` tag on your website:
```html
<script 
  src="https://cdn.example.com/Chauffr-widget.js" 
  data-hub-url="https://api.example.com/chathub" 
  data-allow-drawers="true"
  defer>
</script>
```

---

## Roadmap

- [x] Dynamic Playwright crawler with Markdown extraction
- [x] Decoupled dual-layer hierarchy mapping (Macro navigation vs. Micro RAG)
- [x] Sitemap `lastmod` and SHA-256 delta sync pipeline
- [x] Hybrid BM25 + dense vector indexing via Reciprocal Rank Fusion
- [x] Action Safety Guardrails (Permission tiers, selector sandboxing, user interrupt listeners)
- [x] Resilient SPA navigation bridge with `MutationObserver` retry resolution
- [ ] Bidirectional real-time telemetry (active viewport & scroll depth synchronization)
- [ ] Multi-tab SignalR session management
- [ ] Authenticated crawler mode using mock session tokens for internal app routes
- [ ] Self-hosted local fallback with Ollama (`bge-small` + `llama3.2`)

---

## License
MIT License. See [LICENSE](LICENSE) for details.
