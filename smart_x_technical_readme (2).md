# ⚡ Smart-X Enterprise IoT Ecosystem

**Real-Time Command Orchestration & Predictive Telemetry Processing Engine**

**Developer:** Taahir Basson (ST10282092)

**Tech Stack:** C# | .NET 8 | Blazor Web App | Minimal API | Docker

---

## 1. System Architecture

The Smart-X IoT Ecosystem is a stateful, high-throughput industrial telemetry platform designed to orchestrate live edge-device data. This repository contains the core stream-processing engine, which utilizes advanced data structures and behavioral algorithms to manage live telemetry, prioritize alerts, and execute real-time hardware commands.

* **Secure Zero-Trust Gateway:** Secures the platform using the Google Firebase Identity Toolkit REST API, establishing session-based access without relying on hardcoded credentials.
* **Real-Time Command Stream:** A high-performance processing engine managing live device states, standard telemetry buffering, and critical hardware failures.
* **Predictive Action Engine:** An automated insight module that cross-references out-of-range sensor data with historical operator interactions to surface proactive command recommendations.

---

## 2. Core Data Structures & Algorithmic Complexity

To ensure strict $O(1)$ and $O(\log n)$ performance across the real-time stream, the backend architecture enforces the following dedicated C# collections:

| Instance Name | Data Structure | Complexity | System Implementation |
| :--- | :--- | :--- | :--- |
| `TelemetryQueue` | `Queue<string>` | $O(1)$ | First-In-First-Out (FIFO) processing of standard incoming edge-node telemetry. |
| `CriticalAlertQueue` | `PriorityQueue<string, int>` | $O(\log n)$ | Min-heap priority queue ensuring critical alerts (e.g., voltage spikes) bypass standard telemetry. |
| `CommandUndoStack` | `Stack<OverrideCommand>` | $O(1)$ | First-In-Last-Out (FILO) manual override history allowing safe, one-click state restoration. |
| `DeviceRegistry` | `Dictionary<string, DeviceState>` | $O(1)$ | Hash table keyed on MAC addresses for instantaneous telemetry updates and state mutations. |
| `SensorTimeline` | `SortedDictionary<DateTime, string>` | $O(\log n)$ | Self-balancing binary search tree keyed on UTC timestamps to strictly order timeline event logs. |
| `DisconnectedNodes` | `HashSet<string>` | $O(1)$ | Maintains a unique set of faulty MAC addresses to automatically suppress duplicate alert processing. |

---

## 3. Deployment & Setup

### Firebase Configuration

1. Open `appsettings.json` in the root directory.
2. Insert your Firebase Web API Key:
   ```json
   "Firebase": {
     "WebApiKey": "YOUR_FIREBASE_WEB_API_KEY_HERE"
   }
   ```
3. Ensure **Email/Password** authentication is enabled in your Firebase Console.

### Local Execution (IDE)

1. Open `Smart-X-Ecosystem.slnx` or `Smart-X-Ecosystem.csproj` in JetBrains Rider or Visual Studio 2022.
2. Select the **`http`** launch profile.
3. Run the application (`F5` or `Shift+F10`). The dashboard will launch at `http://localhost:5000`.

### Containerized Execution (Docker)

```bash
docker build -t smart-x-ecosystem .
docker run -p 8080:8080 smart-x-ecosystem
```
Navigate to `http://localhost:8080` in your browser.

---

## 4. Functional Testing Guide

Follow these steps to verify the underlying algorithms and stream-processing mechanics:

### A. Secure Authentication
1. Launch the application to view the enterprise landing page.
2. Enter an email and password in the Operator Portal, then click **Create New Operator Account** (or **Sign In**).
3. Upon successful REST API authentication, the secure dashboard routing will unlock Modules 1 and 2.

### B. Queue Prioritization (PriorityQueue Bypass)
1. Navigate to the **Command Stream** module.
2. Click **+ Normal Packet** twice to load standard telemetry into the FIFO `TelemetryQueue`.
3. Click **⚠️ Moisture Sensor B < 20%** to simulate a high-priority environmental failure.
4. Click **▶ Dequeue & Process Next Packet**.
5. **Expected Result:** The Priority 1 alert explicitly bypasses the standard FIFO queue and processes immediately.

### C. Duplicate Alert Suppression (HashSet)
1. Click **🔌 Simulate Disconnect**. The target MAC address appears in the active errors panel (`DisconnectedNodes`).
2. Click the simulate button a second time to fire the exact same error.
3. **Expected Result:** The `HashSet.Add()` method rejects the duplicate in $O(1)$ time, suppressing the warning and preventing operator UI fatigue.

### D. Safe State Restoration (Stack)
1. In the live device table, click **Toggle Relay** on any online device. The action is executed and pushed onto the `CommandUndoStack`.
2. Click **↩ Undo Last Command** in the top right panel.
3. **Expected Result:** The previous hardware state is popped from the FILO stack and instantly restored on the grid.

### E. Predictive Action Engine (Behavioral Co-occurrence)
1. Search for `Pump A` in the dictionary search bar and click **Record Search**.
2. Trigger the **⚠️ Moisture Sensor B < 20%** anomaly using the simulator buttons.
3. **Expected Result:** The engine mathematically analyzes the out-of-range telemetry against recorded operator workflows and proactively generates an **Automated Insight** card, suggesting you activate Water Pump A.

---

## 5. References

* Jamro, M., 2018. *C# data structures and algorithms*. Birmingham: Packt Publishing.
* Nakov, S., Kolev, V. et al., 2013. *Fundamentals of computer programming with C#*. Sofia: Telerik Software Academy.