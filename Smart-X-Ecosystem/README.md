# Smart-X Industrial IoT Ecosystem (Final PoE — Parts 1, 2 & 3)

**Student Name:** Taahir Basson  
**Student Number:** ST10282092  
**Module:** PROG7312 (Advanced Programming & Data Structures)  
**Framework:** .NET 8.0 Blazor Interactive Server & Minimal REST APIs

---

## 1. Executive Summary & Unified Single-Page Architecture

The **Smart-X Ecosystem** is an enterprise-grade Industrial IoT (IIoT) telemetry ingestion, real-time command orchestration, and self-healing mesh routing platform.

To ensure **zero state loss** across modules, the application registers all state engines (`IngestionEngine`, `CommandStreamService`, `MeshTopologyService`, and `FirebaseAuthService`) as **DI Singletons** and features a **Unified Single-Page Stateful Pillar Switcher** on `Home.razor` (alongside direct routes `/ingestion`, `/command-stream`, and `/topology`).

---

## 2. Pillar Breakdown & Advanced Data Structures

### Pillar 1: Sensor Data Ingestion & Security (`Ingestion.razor`)
- **Generic Telemetry Payloads (`TelemetryPacket<T>`):** Strongly-typed generic packets with regex MAC validation and required field enforcement.
- **Operator Overloading (`PowerMetric`):** Custom `struct` overloading `+`, `>`, and `<` for direct wattage/kilowatt aggregation and threshold comparison.
- **Jagged Array Burst Buffering (`double[][]`):** High-throughput burst buffering inside `IngestionEngine.cs`.
- **Recursive Security Tree (`DeploymentNode`):** N-ary recursive configuration validation (`ValidateConfigurationTree()`).
- **Batch `.txt` Provisioning & AES-256 Encryption (`POST /api/upload`):** Extracts comma-separated IoT devices from `devices.txt` directly into `DeviceRegistry`, `TelemetryQueue`, and `MeshTopologyService` (`HistoricalAvlTree`), while writing an AES-256 encrypted `.enc` archive to `EncryptedLogs/`.

### Pillar 2: Real-Time Command Stream & Predictive Engine (`CommandStream.razor`)
- **Typed Packet Queues (`Queue<QueuedTelemetryPacket>` & `PriorityQueue<QueuedTelemetryPacket, int>`):** Every queued item is a strongly-typed `QueuedTelemetryPacket` carrying `MacAddress`, `DeviceName`, `Category`, `Value`, `Unit`, `Priority`, and `Timestamp`.
- **Automated Background Worker (`TelemetryQueueWorker : BackgroundService`):** Runs continuously on a `1.5s` `PeriodicTimer` and **always drains `CriticalAlertQueue` (Priority 1) completely** before dequeuing standard packets from `TelemetryQueue` (FIFO).
- **LIFO Undo Stack (`Stack<OverrideCommand>`):** Stores operator manual overrides (`CommandUndoStack`) with instant 1-click state rollback.
- **$O(1)$ Device Registry (`Dictionary<string, DeviceState>`):** Case-insensitive MAC address lookup table with real-time Bootstrap progress gauges.
- **Composite-Key Chronological Timeline (`SortedDictionary<TimelineKey, TimelineEntry>`):** Keyed by `TimelineKey` (`DateTime Timestamp` + thread-safe monotonic `long Sequence` via `Interlocked.Increment`) so simultaneous readings never overwrite each other. Powers the live **SVG Telemetry Line Chart**.
- **Dual Unique Sets (`HashSet<string>`):**
  - `DisconnectedNodes`: Suppresses duplicate disconnect alerts.
  - `ActiveErrorStates`: Tracks active fault codes (`ERR_OVERVOLTAGE`, `ERR_LOW_MOISTURE`, `ERR_NODE_OFFLINE`).
- **Count-Computed Predictive Recommendation Engine:** Every recommendation's `ConfidencePercentage` (`double`) is dynamically computed strictly from runtime observation counts (`MatchedCount / TotalObservations * 100.0`).

### Pillar 3: Network Topology & Self-Healing Mesh (`NetworkTopology.razor`)
- **Weighted Graph Adjacency List (`Dictionary<string, List<MeshEdge>>`):** Models physical & wireless ESP32 mesh links with latency/cost weights.
- **BFS & DFS Subnet Traversals:** Breadth-First Search (`Queue<string>`) and Depth-First Search (`Stack<string>`) subnet discovery across online mesh vertices (`MeshVertex`).
- **Prim's Minimum Spanning Tree (`BinaryMinHeap<MeshEdge>`):** Custom generic binary min-heap computing the lowest-cost backbone spanning all active nodes in real time.
- **Self-Healing Multi-Hop Failover Simulator:** Interactive node online/offline toggles that dynamically reroute packets around failed routers/nodes.
- **Self-Balancing AVL Tree (`HistoricalAvlTree`):** Supports $O(\log n)$ insertions with automatic left/right tree rotations (`RotateLeft`, `RotateRight`) and $O(\log n + k)$ time-window range queries (`QueryTimeWindow`), including the **14:00 – 16:00** historical inspection window.

---

## 3. Running the Solution

### Local Execution (.NET 8 CLI / JetBrains Rider / Visual Studio)
```bash
cd Smart-X-Ecosystem
dotnet run --launch-profile http
```
Open **`http://localhost:5000`** in your browser.
- Sign in using the **Firebase Operator Portal** (or click **Instant Demo Operator Login** to authenticate `testoperator@smartx.io` against Google Firebase Identity Toolkit REST API).
- Upload `devices.txt` in **Pillar 1** to test batch provisioning and AES-256 archiving.
