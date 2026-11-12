# Engineering Update & Iterative Feedback Incorporation Ledger (Parts 1, 2 & 3)

**Student Name:** Taahir Basson  
**Student Number:** ST10282092  
**Module:** PROG7312 (Final Portfolio of Evidence)

---

## 1. Part 1 Feedback Refinements Applied

| Feedback Area | Original Critique | Code & Architectural Refinement Implemented |
| :--- | :--- | :--- |
| **Firebase Authentication** | Hardcoded credentials were not permitted; real authentication was required. | Implemented `Services/FirebaseAuthService.cs` integrating the Google Firebase Identity Toolkit REST API (`accounts:signInWithPassword` and `accounts:signUp`). |
| **Batch `.txt` Device Upload** | Uploading a file only showed a banner without extracting devices into the system. | Upgraded `POST /api/upload` and `ImportDevicesFromText()` to parse `.txt` files line-by-line into `DeviceRegistry`, `MeshTopologyService`, and `HistoricalAvlTree`, and write AES-256 `.enc` archives. |
| **Visual Telemetry Gauges** | Dashboard required clearer visual indicators for live readings. | Added dynamic Bootstrap 5 progress gauges across `Ingestion.razor` and `CommandStream.razor`. |

---

## 2. Part 2 Feedback Refinements Applied (64% → 100% Engineering Upgrade)

| Part 2 Rubric Criterion | Lecturer Feedback Quote | Exact Code & Architectural Fix Implemented |
| :--- | :--- | :--- |
| **Main Menu Navigation & Active Pillar State (22/30)** | *"This is a web submission with a home page that presents modules as cards and a Network Topology card marked as a later module. The pages are separate routes, so state is not kept when you move."* | 1. Unlocked **Pillar 3 (`NetworkTopology.razor`)** across the entire application.<br>2. Upgraded `Home.razor` into a **Unified Single-Page Stateful Workspace** where Pillars 1, 2, and 3 switch inline via `ActivePillar` state tabs without route reloads, and registered `FirebaseAuthService`, `IngestionEngine`, `CommandStreamService`, and `MeshTopologyService` as DI **Singletons** in `Program.cs`. |
| **Advanced Collections: Stacks & Queues (10/15)** | *"The command stream service has a priority queue and a standard queue, but both hold plain text. Strings cannot carry the device, the value or the time, so use a packet type. The queues are also emptied by the ProcessNextQueuedItem method, which runs when the user clicks a button on the command stream page. Move this to a background service that always empties the priority queue first."* | 1. Created the typed `QueuedTelemetryPacket` class carrying `MacAddress`, `DeviceName`, `Category`, `Value`, `Unit`, `Priority`, and `Timestamp`, and upgraded the queues to `Queue<QueuedTelemetryPacket>` and `PriorityQueue<QueuedTelemetryPacket, int>`.<br>2. Implemented `Services/TelemetryQueueWorker.cs` (`BackgroundService` registered via `AddHostedService`) that runs continuously in the background and **always drains `CriticalAlertQueue` first** before dequeuing from `TelemetryQueue`. |
| **Lookup Structures: Dictionaries & Key-Value Maps (10/15)** | *"The registry is a dictionary and the timeline is a sorted dictionary of text by timestamp, so readings at the same time overwrite each other and nothing can be charted from a text value. Store the whole reading and use time plus sequence as the key."* | 1. Created `readonly struct TimelineKey : IComparable<TimelineKey>` combining `DateTime Timestamp` and a thread-safe monotonic `long Sequence` (`Interlocked.Increment`) so simultaneous readings never overwrite each other.<br>2. Upgraded `SensorTimeline` to `SortedDictionary<TimelineKey, TimelineEntry>` storing the full numeric `Value`, `Unit`, `MacAddress`, and `DeviceName`, and added a live SVG telemetry line chart on `CommandStream.razor` plotted directly from `TimelineEntry.Value`. |
| **Unique Collections: Sets (7/10)** | *"The disconnected node set is used to suppress duplicate disconnect alerts, which is correct. Add a set for active error states."* | Added `public HashSet<string> ActiveErrorStates { get; }` alongside `DisconnectedNodes` in `CommandStreamService.cs` and rendered a dedicated **ActiveErrorStates (`HashSet<string>`)** panel on `CommandStream.razor`. |
| **Predictive Action & Suggestion Engine (15/30)** | *"The confidence is typed as text such as a learned pattern with a percentage built from a fixed starting number, and fault detection is given a fixed ninety six percent. Compute every confidence from counts."* | Replaced the string confidence field with `public double ConfidencePercentage { get; set; }`, `MatchedCount`, and `TotalObservations`. Every recommendation's confidence is now dynamically computed strictly from runtime counts: `Math.Round((double)MatchedCount / TotalObservations * 100.0, 1)` (co-occurrence transition counts, device fault packet ratios, and operator search query frequencies). |
