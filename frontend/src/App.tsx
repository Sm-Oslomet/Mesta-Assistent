import { useEffect, useMemo, useRef, useState } from "react";

type Role = "user" | "assistant";

type ApiMessage = { role: "user" | "assistant"; content: string };

type ChatRequest = {
    question: string;
    messages: ApiMessage[];
    topK: number;
};

type SourceHit = {
    title?: string | null;
    url?: string | null;
    contentSnippet: string;
};

type ChatApiResponse = {
    answer?: string;
    sources?: SourceHit[];
};

type Message = {
    id: string;
    role: Role;
    content: string;
    sources?: SourceHit[];
};

function uid() {
    return Math.random().toString(16).slice(2) + Date.now().toString(16);
}

function useIsDesktop() {
    const [isDesktop, setIsDesktop] = useState(() => window.innerWidth >= 900);

    useEffect(() => {
        const mq = window.matchMedia("(min-width: 900px)");
        const handler = () => setIsDesktop(mq.matches);
        handler();
        mq.addEventListener("change", handler);
        return () => mq.removeEventListener("change", handler);
    }, []);

    return isDesktop;
}

async function askBackend(req: ChatRequest): Promise<ChatApiResponse> {
    const base = import.meta.env.VITE_API_BASE_URL as string;

    if (!base) {
        throw new Error(
            "Mangler VITE_API_BASE_URL. Lag frontend/.env med f.eks. VITE_API_BASE_URL=https://localhost:56510 og restart npm run dev."
        );
    }

    const res = await fetch(`${base}/api/Chat`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            Accept: "application/json",
        },
        body: JSON.stringify(req),
    });

    if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new Error(`Backend ${res.status}: ${text || res.statusText}`);
    }

    return (await res.json()) as ChatApiResponse;
}

export default function App() {
    const isDesktop = useIsDesktop();

    const [messages, setMessages] = useState<Message[]>([]);
    const [input, setInput] = useState("");
    const [isSending, setIsSending] = useState(false);

    const listRef = useRef<HTMLDivElement | null>(null);
    const inputRef = useRef<HTMLTextAreaElement | null>(null);

    const canSend = useMemo(
        () => input.trim().length > 0 && !isSending,
        [input, isSending]
    );

    const isEmpty = messages.length === 0;

    useEffect(() => {
        listRef.current?.scrollTo({
            top: listRef.current.scrollHeight,
            behavior: "smooth",
        });
    }, [messages.length]);

    const startNew = () => {
        setMessages([]);
        setInput("");
        setTimeout(() => inputRef.current?.focus(), 0);
    };

    const applySuggestion = (text: string) => {
        setInput(text);
        setTimeout(() => inputRef.current?.focus(), 0);
    };

    const send = async () => {
        const q = input.trim();
        if (!q || isSending) return;

        setIsSending(true);
        setInput("");

        const userMsg: Message = { id: uid(), role: "user", content: q };
        setMessages((prev) => [...prev, userMsg]);

        const typingId = uid();
        setMessages((prev) => [
            ...prev,
            { id: typingId, role: "assistant", content: "Søker…" },
        ]);

        try {
            const history: ApiMessage[] = messages
                .filter((m) => m.role === "user" || m.role === "assistant")
                .slice(-6)
                .map((m) => ({ role: m.role, content: m.content }));

            const req: ChatRequest = {
                question: q,
                messages: history,
                topK: 5,
            };

            const response = await askBackend(req);
            const answerText =
                response.answer?.trim() || "Ingen svartekst ble returnert fra backend.";

            setMessages((prev) =>
                prev.map((m) =>
                    m.id === typingId
                        ? {
                            id: typingId,
                            role: "assistant",
                            content: answerText,
                            sources: response.sources ?? [],
                        }
                        : m
                )
            );
        } catch (err) {
            const msg =
                err instanceof Error ? err.message : "Noe gikk galt. Prøv igjen.";
            setMessages((prev) =>
                prev.map((m) =>
                    m.id === typingId
                        ? { id: typingId, role: "assistant", content: msg, sources: [] }
                        : m
                )
            );
        } finally {
            setIsSending(false);
            setTimeout(() => inputRef.current?.focus(), 0);
        }
    };

    const onKeyDown: React.KeyboardEventHandler<HTMLTextAreaElement> = (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            void send();
        }
    };

    const shellStyle = isDesktop ? styles.shellDesktop : styles.shellMobile;

    return (
        <div style={styles.page}>
            <div style={shellStyle}>
                <div style={styles.header}>
                    <div style={styles.headerLeft}>
                        <div style={styles.appIcon} aria-hidden>
                            📄
                        </div>
                        <div>
                            <div style={styles.appTitle}>Mesta AI Assistent</div>
                            <div style={styles.appSubtitle}>
                                Spør om prosedyrer og feltarbeid
                            </div>
                        </div>
                    </div>

                    <button style={styles.iconBtn} title="Profil">
                        <span style={{ fontSize: 16 }}>👤</span>
                    </button>
                </div>

                <div style={styles.content} ref={listRef}>
                    {isEmpty ? (
                        <EmptyState onPick={applySuggestion} isDesktop={isDesktop} />
                    ) : (
                        <div style={styles.chat}>
                            {messages.map((m) => (
                                <Bubble
                                    key={m.id}
                                    role={m.role}
                                    content={m.content}
                                    sources={m.sources}
                                />
                            ))}
                        </div>
                    )}
                </div>

                <div style={styles.composerBar}>
                    <div style={styles.inputWrap}>
                        <textarea
                            ref={inputRef}
                            value={input}
                            onChange={(e) => setInput(e.target.value)}
                            onKeyDown={onKeyDown}
                            placeholder={isEmpty ? "Still et spørsmål…" : "Skriv en oppfølging…"}
                            style={styles.textarea}
                            rows={1}
                            disabled={isSending}
                        />
                    </div>

                    <button
                        onClick={() => void send()}
                        disabled={!canSend}
                        style={{
                            ...styles.roundBtn,
                            ...styles.sendBtn,
                            opacity: canSend ? 1 : 0.5,
                            cursor: canSend ? "pointer" : "not-allowed",
                        }}
                        title="Send"
                    >
                        ➤
                    </button>
                </div>

                <div style={styles.footerHint}>
                    Enter sender · Shift+Enter ny linje ·{" "}
                    <button onClick={startNew} style={styles.linkBtn}>
                        Ny chat
                    </button>
                </div>
            </div>
        </div>
    );
}

function EmptyState({
    onPick,
    isDesktop,
}: {
    onPick: (t: string) => void;
    isDesktop: boolean;
}) {
    return (
        <div style={styles.empty}>
            <div style={styles.bigIcon} aria-hidden>
                📄
            </div>
            <div style={styles.emptyTitle}>Hvordan kan jeg hjelpe deg i dag?</div>
            <div style={styles.emptyText}>
                Spør om HMS, prosedyrer, inspeksjoner eller rapportering.
            </div>

            <div style={{ ...styles.cards, maxWidth: isDesktop ? 720 : "100%" }}>
                <SuggestionCard
                    title="Registrer ny inspeksjon"
                    subtitle="Hvordan starte en inspeksjon"
                    onClick={() => onPick("Hvordan registrerer jeg en ny inspeksjon?")}
                />
                <SuggestionCard
                    title="Sikkerhetsprosedyrer"
                    subtitle="Finn krav og retningslinjer"
                    onClick={() =>
                        onPick("Hvilke sikkerhetsprosedyrer gjelder for feltarbeid?")
                    }
                />
                <SuggestionCard
                    title="Lever arbeidsrapport"
                    subtitle="Hvordan fylle ut og sende rapport"
                    onClick={() => onPick("Hvordan leverer jeg arbeidsrapport?")}
                />
            </div>
        </div>
    );
}

function SuggestionCard({
    title,
    subtitle,
    onClick,
}: {
    title: string;
    subtitle: string;
    onClick: () => void;
}) {
    return (
        <button onClick={onClick} style={styles.card} type="button">
            <div style={styles.cardTitle}>{title}</div>
            <div style={styles.cardSubtitle}>{subtitle}</div>
        </button>
    );
}

function Bubble({
    role,
    content,
    sources,
}: {
    role: Role;
    content: string;
    sources?: SourceHit[];
}) {
    const isUser = role === "user";
    const visibleSources = (sources ?? []).filter(
        (source) => source.title || source.url
    );

    return (
        <div
            style={{
                ...styles.row,
                justifyContent: isUser ? "flex-end" : "flex-start",
            }}
        >
            <div
                style={{
                    ...styles.bubble,
                    ...(isUser ? styles.userBubble : styles.assistantBubble),
                }}
            >
                {content.split("\n").map((line, i) => (
                    <div key={i} style={{ whiteSpace: "pre-wrap" }}>
                        {line || "\u00A0"}
                    </div>
                ))}

                {!isUser && visibleSources.length > 0 && (
                    <div style={styles.sourcesWrap}>
                        <div style={styles.sourcesTitle}>Kilder</div>
                        <div style={styles.sourcesList}>
                            {visibleSources.map((source, index) => (
                                <div key={`${source.url ?? source.title ?? "source"}-${index}`} style={styles.sourceItem}>
                                    <div style={styles.sourceName}>
                                        {source.title || `Kilde ${index + 1}`}
                                    </div>

                                    {source.url ? (
                                        <a
                                            href={source.url}
                                            target="_blank"
                                            rel="noreferrer noopener"
                                            style={styles.sourceLink}
                                        >
                                            Åpne i SharePoint
                                        </a>
                                    ) : (
                                        <div style={styles.sourceSnippet}>{source.contentSnippet}</div>
                                    )}
                                </div>
                            ))}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}

const styles: Record<string, React.CSSProperties> = {
    page: {
        minHeight: "100vh",
        background: "#111827",
        display: "flex",
        justifyContent: "center",
        alignItems: "stretch",
    },

    shellDesktop: {
        width: "100%",
        height: "100vh",
        background: "#F6F7FB",
        display: "flex",
        flexDirection: "column",
    },

    shellMobile: {
        width: "100%",
        maxWidth: 420,
        height: "min(860px, 95vh)",
        background: "#F6F7FB",
        borderRadius: 28,
        overflow: "hidden",
        boxShadow: "0 20px 60px rgba(0,0,0,0.35)",
        display: "flex",
        flexDirection: "column",
        border: "1px solid rgba(0,0,0,0.08)",
        margin: 16,
    },

    header: {
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "14px 16px",
        background: "#FFFFFF",
        borderBottom: "1px solid rgba(0,0,0,0.08)",
    },
    headerLeft: { display: "flex", gap: 12, alignItems: "center" },
    appIcon: {
        width: 40,
        height: 40,
        borderRadius: 12,
        background: "#E8EDFF",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        fontSize: 18,
    },
    appTitle: { fontWeight: 800, fontSize: 15, color: "#111827" },
    appSubtitle: { fontSize: 12, color: "#6B7280", marginTop: 2 },

    iconBtn: {
        border: "1px solid rgba(0,0,0,0.08)",
        background: "#fff",
        width: 38,
        height: 38,
        borderRadius: 999,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        cursor: "pointer",
    },

    content: {
        flex: 1,
        overflowY: "auto",
        padding: 16,
        display: "flex",
        justifyContent: "center",
    },

    empty: {
        width: "100%",
        maxWidth: 900,
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        paddingTop: 28,
        gap: 10,
    },
    bigIcon: {
        width: 74,
        height: 74,
        borderRadius: 22,
        background: "#E8EDFF",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        fontSize: 26,
        marginBottom: 8,
    },
    emptyTitle: {
        fontSize: 26,
        fontWeight: 800,
        color: "#111827",
        textAlign: "center",
    },
    emptyText: {
        fontSize: 13,
        color: "#6B7280",
        textAlign: "center",
        maxWidth: 520,
    },

    cards: {
        width: "100%",
        marginTop: 14,
        display: "flex",
        flexDirection: "column",
        gap: 12,
    },
    card: {
        width: "100%",
        textAlign: "left",
        background: "#FFFFFF",
        border: "1px solid rgba(0,0,0,0.08)",
        borderRadius: 14,
        padding: "14px 14px",
        cursor: "pointer",
        boxShadow: "0 8px 20px rgba(17,24,39,0.06)",
    },
    cardTitle: { fontWeight: 800, color: "#111827", fontSize: 14 },
    cardSubtitle: { marginTop: 4, fontSize: 12, color: "#6B7280" },

    chat: {
        width: "100%",
        maxWidth: 900,
        display: "flex",
        flexDirection: "column",
        gap: 10,
    },
    row: { display: "flex", width: "100%" },
    bubble: {
        maxWidth: "85%",
        borderRadius: 16,
        padding: "10px 12px",
        fontSize: 13.5,
        lineHeight: 1.35,
        border: "1px solid rgba(0,0,0,0.08)",
        boxShadow: "0 6px 18px rgba(17,24,39,0.06)",
        background: "#fff",
    },
    userBubble: { background: "#E8EDFF", color: "#111827" },
    assistantBubble: { background: "#FFFFFF", color: "#111827" },

    sourcesWrap: {
        marginTop: 12,
        paddingTop: 10,
        borderTop: "1px solid rgba(0,0,0,0.08)",
    },
    sourcesTitle: {
        fontSize: 12,
        fontWeight: 800,
        color: "#374151",
        marginBottom: 8,
    },
    sourcesList: {
        display: "flex",
        flexDirection: "column",
        gap: 8,
    },
    sourceItem: {
        padding: "8px 10px",
        borderRadius: 12,
        background: "#F9FAFB",
        border: "1px solid rgba(0,0,0,0.06)",
    },
    sourceName: {
        fontWeight: 700,
        fontSize: 12.5,
        color: "#111827",
        marginBottom: 4,
    },
    sourceLink: {
        fontSize: 12.5,
        color: "#4F46E5",
        textDecoration: "none",
        fontWeight: 700,
    },
    sourceSnippet: {
        fontSize: 12,
        color: "#6B7280",
    },

    composerBar: {
        padding: "10px 12px",
        background: "#FFFFFF",
        borderTop: "1px solid rgba(0,0,0,0.08)",
        display: "flex",
        gap: 10,
        alignItems: "flex-end",
        justifyContent: "center",
    },
    roundBtn: {
        width: 44,
        height: 44,
        borderRadius: 999,
        border: "1px solid rgba(0,0,0,0.10)",
        background: "#F3F4F6",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        cursor: "pointer",
        fontSize: 16,
    },
    sendBtn: { background: "#4F46E5", color: "#fff", border: "none" },

    inputWrap: {
        width: "min(900px, 100%)",
        flex: 1,
        border: "1px solid rgba(0,0,0,0.10)",
        background: "#F9FAFB",
        borderRadius: 18,
        padding: "10px 12px",
        display: "flex",
        maxWidth: 900,
    },
    textarea: {
        width: "100%",
        border: "none",
        outline: "none",
        resize: "none",
        background: "transparent",
        fontSize: 13.5,
        lineHeight: 1.35,
        color: "#111827",
    },

    footerHint: {
        padding: "8px 14px 12px",
        background: "#FFFFFF",
        fontSize: 11.5,
        color: "#6B7280",
        textAlign: "center",
        borderTop: "1px solid rgba(0,0,0,0.06)",
    },
    linkBtn: {
        border: "none",
        background: "transparent",
        color: "#4F46E5",
        fontWeight: 700,
        cursor: "pointer",
        padding: 0,
    },
};
