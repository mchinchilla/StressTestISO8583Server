<p align="center">
  <h1 align="center">⚡ StressTestISO8583Server</h1>
  <p align="center">
    A high-performance stress testing tool for ISO 8583 financial transaction servers.
    <br />
    Built with .NET 10 · Spectre.Console · Async I/O
  </p>
</p>

<p align="center">
  <a href="#-features">Features</a> •
  <a href="#-architecture">Architecture</a> •
  <a href="#-getting-started">Getting Started</a> •
  <a href="#-usage">Usage</a> •
  <a href="#-how-it-works">How It Works</a> •
  <a href="#-license">License</a>
</p>

---

## 🎯 Goal

Validate the **throughput**, **stability**, and **resilience** of ISO 8583 servers under heavy concurrent load by sending configurable batches of financial transaction messages (type `0200`) over TCP or TLS.

## ✨ Features

| Feature | Description |
|---|---|
| 🚀 **Async I/O** | Fully asynchronous networking with `ConnectAsync`, `WriteAsync`, `ReadAsync` |
| 📊 **Live Progress Bar** | Real-time progress with percentage, ETA, and spinner via Spectre.Console |
| 🔒 **TLS Support** | Optional TLS/SSL transport for encrypted connections |
| 🎛️ **Configurable Concurrency** | Control batch size (concurrent connections) and quantity (number of batches) |
| 📝 **Verbose Mode** | Per-message result logging for debugging |
| ♻️ **Memory Efficient** | `ArrayPool<byte>` buffer recycling instead of per-message allocations |
| 🛑 **Graceful Cancellation** | `Ctrl+C` propagates cancellation through all layers |
| 🩺 **Pre-flight Connectivity Check** | Validates server reachability before launching the stress test |

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Program.cs                           │
│                   CLI Entry Point (Spectre)                  │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    StressTestCommand.cs                      │
│         Orchestration · Display · Progress · Results         │
└──────────┬──────────────────────────────────┬───────────────┘
           │                                  │
           ▼                                  ▼
┌────────────────────────┐     ┌────────────────────────────┐
│   StressTestRunner.cs  │     │   StressTestSettings.cs    │
│  Channel<T> Producer/  │     │   CLI argument definitions │
│  Consumer · Semaphore  │     │                            │
└──────────┬─────────────┘     └────────────────────────────┘
           │
           ▼
┌────────────────────────┐
│  IsoMessageSender.cs   │
│  Async TCP/TLS I/O     │
│  ArrayPool buffers     │
└────────────────────────┘
```

### 📁 Project Structure

```
StressTestISO8583Server/
├── Program.cs                 → Entry point — CLI setup only
├── StressTestSettings.cs      → Command-line argument definitions
├── StressTestCommand.cs       → Orchestration, display, progress tracking
├── StressTestRunner.cs        → Concurrency engine (Channel + SemaphoreSlim)
├── IsoMessageSender.cs        → Async TCP/TLS networking layer
└── Resources/
    └── config.xml             → ISO 8583 message field templates
```

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- An ISO 8583 server to test against

### Build

```bash
git clone https://github.com/mchinchilla/StressTestISO8583Server.git
cd StressTestISO8583Server
dotnet build
```

### Run

```bash
cd StressTestISO8583Server
dotnet run -- -s <server> [OPTIONS]
```

## 📖 Usage

```
USAGE:
    StressTestISO8583Server [OPTIONS]

OPTIONS:
    -h, --help                   Prints help information
    -v, --verbose                Set output to verbose messages
    -s, --server                 IP address or FQDN of the target server (required)
    -p, --port        5005       Server port (default 5005)
    -t, --usetls                 Use TLS/SSL transport
    -b, --batch       10         Number of concurrent messages per batch
    -q, --quantity    100        Total number of batches to send
```

### Examples

```bash
# Basic — 100 batches × 10 messages = 1,000 messages to localhost
dotnet run -- -s 127.0.0.1

# Full load test — 1,000 batches × 10 = 10,000 messages over TLS with verbose output
dotnet run -- -s tekiumlabs.com -p 5005 -q 1000 -b 10 -t -v

# High concurrency — 50 batches × 100 concurrent = 5,000 messages
dotnet run -- -s 192.168.1.50 -p 8080 -q 50 -b 100
```

> 💡 **Total messages sent = `quantity × batch`**. The `batch` controls how many concurrent connections are open at a time, and `quantity` is how many rounds of batches to send.

## ⚙️ How It Works

```
            ┌──────────────────────────────────────────────────────┐
            │               STRESS TEST EXECUTION FLOW             │
            └──────────────────────────────────────────────────────┘

 ┌─────────┐    ┌─────────────────────┐    ┌────────────────────┐
 │  Parse   │───▶│  Display Config    │───▶│  Connectivity      │
 │  CLI     │    │  Table             │    │  Check (5s timeout)│
 └─────────┘    └─────────────────────┘    └────────┬───────────┘
                                                    │
                                              ┌─────┴─────┐
                                              │           │
                                           ✅ OK      ❌ Fail
                                              │        (exit 1)
                                              ▼
                           ┌────────────────────────────────────┐
                           │     Build ISO 8583 Message (0x200) │
                           └──────────────┬─────────────────────┘
                                          │
                                          ▼
                           ┌───────────────────────────────────┐
                           │     Channel<T> Producer           │
                           │  Enqueue (batch, msg) pairs       │
                           └──────────────┬────────────────────┘
                                          │
                    ┌─────────────────────┼─────────────────────┐
                    │                     │                     │
                    ▼                     ▼                     ▼
            ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
            │  Consumer 1  │     │  Consumer 2  │     │  Consumer N  │
            │  SendAsync() │     │  SendAsync() │     │  SendAsync() │
            └──────┬───────┘     └──────┬───────┘     └──────┬───────┘
                   │                    │                    │
                   │      SemaphoreSlim(batchSize)           │
                   │       limits concurrency                │
                   ▼                    ▼                    ▼
            ┌─────────────────────────────────────────────────────┐
            │              ISO 8583 Server (Target)               │
            │                  TCP / TLS                          │
            └─────────────────────────────────────────────────────┘
                                    │
                                    ▼
                    ┌───────────────────────────────┐
                    │    Results Summary Table      │
                    │  ✅ Success  ❌ Failed  ⏱ Time │
                    └───────────────────────────────┘
```

### Pre-flight Connectivity Check

Before launching any messages, the tool performs a TCP connectivity check against the target server with a **5-second timeout**:

- **Success** → Proceeds with the stress test
- **Timeout** → `"Connection timed out — did not respond within 5 seconds"` and exits
- **Socket error** → `"Connection failed — <reason>"` and exits

This prevents wasting time and resources sending thousands of messages to an unreachable server.

### Concurrency Model

The application uses a **producer/consumer pattern** powered by `System.Threading.Channels`:

1. **Producer** — A single writer enqueues all `(batch, message)` pairs into a bounded channel
2. **Consumers** — Multiple readers dequeue and send messages concurrently
3. **SemaphoreSlim** — Limits the number of simultaneous in-flight connections to the configured `batch` size
4. **CancellationToken** — Propagated from `Ctrl+C` through every layer down to the socket

### ISO 8583 Message

The tool sends a standard **0200** (Authorization Request) message built from `Resources/config.xml`, which includes fields such as:

| Field | Type | Description |
|-------|------|-------------|
| 3 | NUMERIC(6) | Processing Code |
| 4 | AMOUNT | Transaction Amount |
| 11 | NUMERIC(6) | STAN |
| 35 | LLVAR | Track 2 Data |
| 41 | ALPHA(8) | Terminal ID |
| 42 | ALPHA(15) | Merchant ID |

## 🔧 Tech Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 |
| CLI Framework | [Spectre.Console.Cli](https://spectreconsole.net/) |
| Progress & UI | [Spectre.Console](https://spectreconsole.net/) |
| ISO 8583 | [NetCore8583](https://github.com/nicholasrs/NetCore8583) |
| Concurrency | `System.Threading.Channels`, `SemaphoreSlim` |
| Networking | `TcpClient` + async streams, `SslStream` |
| Memory | `System.Buffers.ArrayPool<byte>` |

## 📄 License

This project is open source and available under the [MIT License](LICENSE).

---

<p align="center">
  Made with ❤️ for the fintech community
</p>
