import React, { useEffect, useMemo, useState } from "react"
import { api, type ServerEndpoint } from "../api"
import { useStore } from "../store"

type Step = 1 | 2 | 3 | 4

const defaultImap: ServerEndpoint = { host: "", port: 993, useSsl: true, useStartTls: false }
const defaultSmtp: ServerEndpoint = { host: "", port: 587, useSsl: false, useStartTls: true }

type EndpointEditorProps = {
  label: string
  value: ServerEndpoint
  onChange: (v: ServerEndpoint) => void
}

function EndpointEditor({ label, value, onChange }: EndpointEditorProps) {
  return (
    <div className="card" style={{ padding: 12, display: "grid", gap: 8 }}>
      <div style={{ fontWeight: 650 }}>{label}</div>
      <input
        className="field"
        placeholder="host"
        value={value.host}
        onChange={(e) => onChange({ ...value, host: e.target.value })}
      />
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
        <input
          className="field"
          placeholder="port"
          inputMode="numeric"
          value={value.port}
          onChange={(e) => onChange({ ...value, port: Number(e.target.value) || 0 })}
        />
        <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
          <label style={{ display: "flex", gap: 6, alignItems: "center" }}>
            <input
              type="checkbox"
              checked={value.useSsl}
              onChange={(e) => onChange({ ...value, useSsl: e.target.checked })}
            />
            SSL/TLS
          </label>
          <label style={{ display: "flex", gap: 6, alignItems: "center" }}>
            <input
              type="checkbox"
              checked={value.useStartTls}
              onChange={(e) => onChange({ ...value, useStartTls: e.target.checked })}
            />
            STARTTLS
          </label>
        </div>
      </div>
    </div>
  )
}

export function AddAccountWizard({ onClose }: { onClose: () => void }) {
  const addAccount = useStore((s) => s.addAccount)
  const loading = useStore((s) => s.loading)

  const [step, setStep] = useState<Step>(1)
  const [email, setEmail] = useState("")
  const [displayName, setDisplayName] = useState("")
  const [providerHint, setProviderHint] = useState("")
  const [authMethod, setAuthMethod] = useState<"password" | "oauth">("password")
  const [password, setPassword] = useState("")
  const [oauthLinked, setOauthLinked] = useState(false)
  const [oauthError, setOauthError] = useState<string | null>(null)
  const [oauthLoading, setOauthLoading] = useState(false)
  const [isIcloud, setIsIcloud] = useState(false)
  const [showHelp, setShowHelp] = useState(true)

  const [imap, setImap] = useState<ServerEndpoint>(defaultImap)
  const [smtp, setSmtp] = useState<ServerEndpoint>(defaultSmtp)

  const [discovering, setDiscovering] = useState(false)
  const [discoverError, setDiscoverError] = useState<string | null>(null)
  const [validated, setValidated] = useState<{ ok: boolean; message: string } | null>(null)
  const [validating, setValidating] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  useEffect(() => {
    if (!displayName && email.includes("@")) {
      setDisplayName(email.split("@")[0])
    }
  }, [email, displayName])

  useEffect(() => {
    if (!providerHint && email.includes("@")) {
      const domain = email.split("@")[1]
      if (domain) setProviderHint(domain)
    }
  }, [email, providerHint])

  useEffect(() => {
    const domain = email.split("@")[1]?.toLowerCase() ?? ""
    setIsIcloud(["icloud.com", "me.com", "mac.com"].some((d) => domain === d))
  }, [email])

  useEffect(() => {
    const handler = (ev: MessageEvent) => {
      const data = ev.data as any
      if (data?.type === "OAUTH_DONE") {
        if (data.success) {
          setOauthLinked(true)
          useStore.getState().init().catch(() => {
            // ignore
          })
        } else {
          setOauthError(data.message ?? "OAuth failed")
        }
      }
    }
    window.addEventListener("message", handler)
    return () => window.removeEventListener("message", handler)
  }, [])

  const canProceedStep1 = email.trim().length > 3 && email.includes("@")
  const canProceedStep2 = authMethod === "password" ? password.trim().length > 0 : oauthLinked
  const canSave =
    imap.host && smtp.host && imap.port > 0 && smtp.port > 0 && (authMethod === "password" ? password : oauthLinked) && email && displayName

  async function handleDiscover() {
    setDiscovering(true)
    setDiscoverError(null)
    setValidated(null)
    try {
      const res = await api.discoverAccount({ email: email.trim(), providerHint: providerHint || undefined })
      setImap(res.imap)
      setSmtp(res.smtp)
      setProviderHint(res.providerHint)
    } catch (e: any) {
      setDiscoverError(e?.message ?? "autodiscover failed, please configure manually")
      setImap(defaultImap)
      setSmtp(defaultSmtp)
    } finally {
      setDiscovering(false)
      setStep(3)
    }
  }

  async function handleValidate() {
    setValidating(true)
    setValidated(null)
    try {
      const res = await api.validateSettings({
        email: email.trim(),
        password: password.trim(),
        imap,
        smtp
      })
      setValidated({ ok: res.ok, message: res.message ?? "ok" })
    } catch (e: any) {
      setValidated({ ok: false, message: e?.message ?? "validation failed" })
    } finally {
      setValidating(false)
    }
  }

  async function handleSave() {
    setSaveError(null)
    try {
      await addAccount({
        email: email.trim(),
        displayName: displayName.trim() || email.trim(),
        password: authMethod === "password" ? password.trim() : undefined,
        providerHint: providerHint || "custom",
        imap,
        smtp
      })
      onClose()
    } catch (e: any) {
      setSaveError(e?.message ?? "unable to save account")
    }
  }

  const stepLabel = useMemo(() => {
    switch (step) {
      case 1:
        return "Email"
      case 2:
        return "Auth method"
      case 3:
        return "Autodiscover"
      case 4:
        return "Confirm & edit"
      default:
        return ""
    }
  }, [step])

  return (
    <div className="card" style={{ padding: 14, display: "grid", gap: 12 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div style={{ fontWeight: 700 }}>Add account · {stepLabel}</div>
        <button className="btn" onClick={onClose}>close</button>
      </div>

      <div style={{ display: "grid", gap: 8, gridTemplateColumns: "repeat(4, minmax(0, 1fr))" }}>
        {[1, 2, 3, 4].map((i) => (
          <div
            key={i}
            style={{
              height: 4,
              borderRadius: 999,
              background: step >= i ? "var(--accent)" : "var(--line)"
            }}
          />
        ))}
      </div>

      {step === 1 ? (
        <div style={{ display: "grid", gap: 10 }}>
          <input className="field" placeholder="work email" value={email} onChange={(e) => setEmail(e.target.value)} />
          <input className="field" placeholder="display name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={onClose}>cancel</button>
            <button className="btn primary" disabled={!canProceedStep1} onClick={() => setStep(2)}>
              next: auth
            </button>
          </div>
        </div>
      ) : null}

      {step === 2 ? (
        <div style={{ display: "grid", gap: 10 }}>
          <div style={{ display: "grid", gap: 6 }}>
            <label
              htmlFor="auth-password"
              aria-label="Password or app password"
              style={{ display: "flex", gap: 10, alignItems: "center" }}
              className="card field"
            >
              <input id="auth-password" type="radio" checked={authMethod === "password"} onChange={() => setAuthMethod("password")} />
              <div>
                <div style={{ fontWeight: 650 }}>Password / app password</div>
                <div className="muted">Recommended for now; OAuth coming soon.</div>
              </div>
            </label>
            <label
              htmlFor="auth-oauth"
              aria-label="OAuth planned"
              style={{ display: "flex", gap: 10, alignItems: "center" }}
              className="card field"
            >
              <input id="auth-oauth" type="radio" checked={authMethod === "oauth"} onChange={() => setAuthMethod("oauth")} />
              <div>
                <div style={{ fontWeight: 650 }}>OAuth (Gmail)</div>
                <div className="muted">Opens Google in a popup. Least-privilege scopes.</div>
              </div>
            </label>
          </div>
          {authMethod === "password" ? (
            <input
              className="field"
              placeholder="password (or app password)"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          ) : (
            <div style={{ display: "grid", gap: 8 }}>
              <div className="muted">Gmail OAuth avoids app passwords. Popup may be blocked; allow popups for this site.</div>
              <button
                className="btn primary"
                disabled={oauthLoading || !email}
                onClick={async () => {
                  setOauthError(null)
                  setOauthLoading(true)
                  try {
                    const res = await api.startGoogleOAuth({ email: email.trim(), displayName: displayName.trim() || email.trim() })
                    const win = window.open(res.authUrl, "oauth", "width=520,height=720")
                    if (!win) setOauthError("popup blocked, allow popups")
                  } catch (e: any) {
                    setOauthError(e?.message ?? "oauth start failed")
                  } finally {
                    setOauthLoading(false)
                  }
                }}
              >
                {oauthLoading ? "opening..." : "Sign in with Google"}
              </button>
              {oauthError ? <div style={{ color: "#b42318" }}>{oauthError}</div> : null}
              {oauthLinked ? <div style={{ color: "#0f5132" }}>Linked! You can continue.</div> : null}
            </div>
          )}
          <div className="card" style={{ padding: 10, background: "rgba(0,0,0,0.02)", display: showHelp ? "grid" : "none", gap: 6 }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <div style={{ fontWeight: 650 }}>What to enter here</div>
              <button className="btn" onClick={() => setShowHelp(false)} style={{ padding: "6px 8px" }}>
                hide
              </button>
            </div>
            {authMethod === "password" ? (
              <>
                {isIcloud ? (
                  <>
                    <div>For iCloud/Me/Mac addresses Apple requires an <strong>app-specific password</strong>.</div>
                    <ol style={{ margin: 0, paddingLeft: 18, color: "var(--muted)", display: "grid", gap: 4 }}>
                      <li>Open <a href="https://appleid.apple.com/account/manage" target="_blank" rel="noreferrer">appleid.apple.com</a></li>
                      <li>Sign in → Security → App-Specific Passwords → Generate</li>
                      <li>Paste the generated password in the field above</li>
                    </ol>
                    <div className="muted">Your Apple ID password will not work here.</div>
                  </>
                ) : (
                  <div className="muted">Enter your mailbox password or app password if your provider enforces 2FA (e.g., Gmail/Outlook/Yahoo app passwords).</div>
                )}
              </>
            ) : (
              <div className="muted">You will be redirected to Google, no password is stored. Use this if your Gmail account has 2FA.</div>
            )}
          </div>
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={() => setStep(1)}>back</button>
            <button className="btn" onClick={() => { setStep(3); setDiscoverError(null); }}>skip autodiscover</button>
            <button className="btn primary" disabled={!canProceedStep2 || discovering} onClick={handleDiscover}>
              {discovering ? "discovering..." : "autodiscover"}
            </button>
          </div>
        </div>
      ) : null}

      {step === 3 ? (
        <div style={{ display: "grid", gap: 10 }}>
          {discoverError ? (
            <div style={{ color: "#b42318" }}>{discoverError}</div>
          ) : (
            <div className="muted">{imap.host || smtp.host ? "Autodiscover fetched these settings." : "No settings yet, continue to manual edit."}</div>
          )}
          <div className="card" style={{ padding: 12, display: "grid", gap: 8 }}>
            <div style={{ fontWeight: 650 }}>IMAP</div>
            <div className="muted">{imap.host ? `${imap.host}:${imap.port}` : "not set"}</div>
            <div className="muted">SSL: {imap.useSsl ? "yes" : "no"} · STARTTLS: {imap.useStartTls ? "yes" : "no"}</div>
          </div>
          <div className="card" style={{ padding: 12, display: "grid", gap: 8 }}>
            <div style={{ fontWeight: 650 }}>SMTP</div>
            <div className="muted">{smtp.host ? `${smtp.host}:${smtp.port}` : "not set"}</div>
            <div className="muted">SSL: {smtp.useSsl ? "yes" : "no"} · STARTTLS: {smtp.useStartTls ? "yes" : "no"}</div>
          </div>
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={() => setStep(2)}>back</button>
            <button className="btn" onClick={handleDiscover} disabled={discovering}>
              {discovering ? "retrying..." : "retry discover"}
            </button>
            <button className="btn primary" onClick={() => setStep(4)}>
              edit & confirm
            </button>
          </div>
        </div>
      ) : null}

      {step === 4 ? (
        <div style={{ display: "grid", gap: 12 }}>
          <div className="muted">Adjust any fields, validate, then save.</div>
          <EndpointEditor label="IMAP" value={imap} onChange={(v) => { setImap(v); setValidated(null); }} />
          <EndpointEditor label="SMTP" value={smtp} onChange={(v) => { setSmtp(v); setValidated(null); }} />
          {validated ? (
            <div className="card" style={{ padding: 10, color: validated.ok ? "#0f5132" : "#b42318", background: validated.ok ? "rgba(16,185,129,0.12)" : "rgba(244,67,54,0.12)" }}>
              {validated.message}
            </div>
          ) : null}
          {saveError ? <div style={{ color: "#b42318" }}>{saveError}</div> : null}
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={() => setStep(3)}>back</button>
            <button className="btn" onClick={handleValidate} disabled={validating || !canSave}>
              {validating ? "validating..." : "validate settings"}
            </button>
            {authMethod === "password" ? (
              <button className="btn primary" onClick={() => void handleSave()} disabled={!canSave || loading}>
                {loading ? "saving..." : "save account"}
              </button>
            ) : (
              <button className="btn primary" onClick={onClose} disabled={!oauthLinked}>
                close
              </button>
            )}
          </div>
        </div>
      ) : null}
    </div>
  )
}
