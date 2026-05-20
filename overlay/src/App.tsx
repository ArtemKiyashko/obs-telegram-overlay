import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import './App.css'

type OverlayConfig = {
  overlayId: string
  negotiateUrl: string
  siteTitle: string
  ttsMode?: 'off' | 'browser'
}

type NegotiationResult = {
  url: string
  accessToken: string
}

type OverlayMessage = {
  chatId: number
  messageId: number
  sender: string
  text: string
  sentUtc: string
}

const MAX_VISIBLE_MESSAGES = 10

function formatClock(value: string) {
  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return ''
  }

  return new Intl.DateTimeFormat('ru-RU', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

async function loadOverlayConfig(): Promise<OverlayConfig> {
  const response = await fetch('./config.json', { cache: 'no-store' })

  if (!response.ok) {
    throw new Error(`Overlay config request failed with ${response.status}`)
  }

  return response.json() as Promise<OverlayConfig>
}

async function negotiate(url: string): Promise<NegotiationResult> {
  const response = await fetch(url, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
    },
  })

  if (!response.ok) {
    throw new Error(`Negotiate request failed with ${response.status}`)
  }

  return response.json() as Promise<NegotiationResult>
}

function App() {
  const [status, setStatus] = useState('Loading overlay configuration...')
  const [title, setTitle] = useState('Telegram Stream Overlay')
  const [messages, setMessages] = useState<OverlayMessage[]>([])
  const [overlayId, setOverlayId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const ttsQueueRef = useRef<string[]>([])
  const ttsProcessingRef = useRef(false)

  useEffect(() => {
    let disposed = false
    let connection: HubConnection | null = null

    const speakNext = () => {
      if (ttsProcessingRef.current) {
        return
      }

      const nextText = ttsQueueRef.current.shift()
      if (!nextText) {
        return
      }

      ttsProcessingRef.current = true
      const utterance = new SpeechSynthesisUtterance(nextText)
      utterance.rate = 1
      utterance.pitch = 1
      utterance.onend = () => {
        ttsProcessingRef.current = false
        speakNext()
      }
      utterance.onerror = () => {
        ttsProcessingRef.current = false
        speakNext()
      }
      window.speechSynthesis.speak(utterance)
    }

    const enqueueTts = (text: string) => {
      if (!('speechSynthesis' in window)) {
        return
      }

      ttsQueueRef.current.push(text)
      speakNext()
    }

    const boot = async () => {
      try {
        const config = await loadOverlayConfig()
        if (disposed) {
          return
        }

        setOverlayId(config.overlayId)
        setTitle(config.siteTitle)
        setStatus('Negotiating SignalR session...')

        let cachedNegotiation: NegotiationResult | null = await negotiate(config.negotiateUrl)
        if (disposed) {
          return
        }

        connection = new HubConnectionBuilder()
          .withUrl(cachedNegotiation.url, {
            accessTokenFactory: async () => {
              const current = cachedNegotiation ?? (await negotiate(config.negotiateUrl))
              cachedNegotiation = null
              return current.accessToken
            },
          })
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Warning)
          .build()

        connection.on('newMessage', (payload: OverlayMessage) => {
          setMessages((current) => {
            const next = [payload, ...current]
            return next.slice(0, MAX_VISIBLE_MESSAGES)
          })

          if (config.ttsMode === 'browser') {
            enqueueTts(`${payload.sender}: ${payload.text}`)
          }
        })

        connection.onreconnecting(() => {
          setStatus('Reconnecting...')
        })

        connection.onreconnected(() => {
          setStatus('Connected')
        })

        connection.onclose(() => {
          setStatus('Disconnected')
        })

        await connection.start()
        if (disposed) {
          return
        }

        setStatus('Connected')
      } catch (cause) {
        if (disposed) {
          return
        }

        const message = cause instanceof Error ? cause.message : 'Unknown startup error'
        setError(message)
        setStatus('Overlay unavailable')
      }
    }

    void boot()

    return () => {
      disposed = true
      window.speechSynthesis.cancel()

      if (connection && connection.state !== HubConnectionState.Disconnected) {
        void connection.stop()
      }
    }
  }, [])

  return (
    <main className="overlay-shell">
      <section className="overlay-frame">
        <header className="overlay-header">
          <div>
            <p className="eyebrow">Live Telegram Feed</p>
            <h1>{title}</h1>
          </div>
          <div className={`status-pill ${error ? 'is-error' : ''}`}>
            <span className="status-dot" />
            <span>{status}</span>
          </div>
        </header>

        <div className="message-stack" role="log" aria-live="polite">
          {messages.length === 0 ? (
            <article className="empty-state">
              <p>Waiting for Telegram messages.</p>
              {overlayId ? <span>Overlay ID: {overlayId}</span> : null}
            </article>
          ) : (
            messages.map((message) => (
              <article className="message-card" key={`${message.chatId}-${message.messageId}`}>
                <div className="message-meta">
                  <strong>{message.sender}</strong>
                  <span>{formatClock(message.sentUtc)}</span>
                </div>
                <p>{message.text}</p>
              </article>
            ))
          )}
        </div>

        {error ? <p className="error-text">{error}</p> : null}
      </section>
    </main>
  )
}

export default App
