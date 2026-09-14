Smart-X Data Ingestion and Validation Gateway

The Smart-X Gateway is a centralized, high-throughput administration dashboard built with .NET 8 Blazor. It is designed to manage complex Internet of Things (IoT) ecosystems by providing developers with robust tools for sensor data ingestion, encrypted diagnostic logging, and dynamic anomaly tracking.

This project was developed for PROG7312 Formative 1.

🏗️ Architectural Pillars

The ecosystem interface is divided into three core architectural pillars (Note: Currently, only Pillar 1 is active for the Formative 1 assessment):

Sensor Data Ingestion & Telemetry (Active):

Ingests diverse IoT telemetry via a generic .NET packet wrapper (TelemetryPacket<T>).

Validates incoming edge-device connections using strict DataAnnotations (e.g., MAC Address Regex).

Secures sensitive diagnostic logs via full server-side AES-256 Encryption (CryptoStream).

Real-Time Command Stream (Part 2 - Locked):

Planned implementation for bidirectional command execution.

Network Topology & Mesh Routing (Final PoE - Locked):

Planned implementation for spatial visualization and node management.

🚀 Technical Highlights

This application demonstrates advanced C# Object-Oriented Programming principles to handle high-velocity data efficiently:

Generics: Utilizes <T> to wrap variable payload types (integers, floats, booleans) without incurring the performance penalty of boxing and unboxing.

Operator Overloading: The PowerMetric class overloads the +, >, and < operators, allowing the system to aggregate raw node data mathematically.

Multi-Dimensional Arrays: Implements jagged arrays (double[][]) within the IngestionEngine to temporarily buffer burst telemetry before processing.

Recursive Validation: The DeploymentNode class utilizes a recursive function ValidateConfigurationTree() to traverse nested node relationships and verify safe configurations across the entire mesh hierarchy.

🛠️ Setup and Installation

Prerequisites

.NET 8.0 SDK

Visual Studio 2022 (or newer)

Optional: Docker Desktop (for containerized deployment)

Option 1: Running locally via Visual Studio

Clone this repository to your local machine:

git clone https://github.com/your-username/smart-x-ecosystem.git


Navigate to the project directory and open the solution file:

cd smart-x-ecosystem
start Smart-X-Ecosystem.sln


In Visual Studio, ensure the http profile is selected in the launch configurations dropdown (next to the green Play button).

Click Start Debugging (F5) or Start Without Debugging (Ctrl+F5) to launch the web application.

Option 2: Running via Docker

Open a terminal in the root directory of the project (where the Dockerfile is located).

Build the Docker image:

docker build -t smart-x-gateway .


Run the container, mapping port 8080 to your local machine:

docker run -d -p 8080:8080 --name smart-x-app smart-x-gateway


Open a web browser and navigate to http://localhost:8080.

🔒 Security

All diagnostic file uploads are encrypted server-side before being written to disk. The system utilizes standard AES symmetric encryption, with the EncryptedLogs directory generated dynamically upon the first successful upload.
