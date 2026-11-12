using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Smart_X_Ecosystem.Models;

namespace Smart_X_Ecosystem.Services
{
    public class MeshTopologyService
    {
        private readonly object _lock = new();
        private long _avlSequence = 0;

        public event Action? OnTopologyChanged;

        // N-ary Hierarchical Tree & Weighted Graph Adjacency List
        public Dictionary<string, MeshNode> Nodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<MeshEdge>> AdjacencyList { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<MeshEdge> Edges { get; } = new();

        // Self-Balancing AVL Tree for Historical Telemetry Range Queries O(log n + k)
        public HistoricalAvlTree HistoricalAvlTree { get; } = new();
        public int AvlRecordCount => HistoricalAvlTree.Count;
        public int AvlRotationsPerformed => HistoricalAvlTree.RotationsPerformed;

        public MeshTopologyService()
        {
            SeedDefaultTopology();
        }

        private void SeedDefaultTopology()
        {
            var initialNodes = new[]
            {
                new MeshNode { MacAddress = "00:1B:44:00:00:01", DeviceName = "Core Cloud Gateway", Role = "Root Gateway", Category = "Core", IsOnline = true, X = 400, Y = 50 },
                new MeshNode { MacAddress = "00:1B:44:00:00:02", DeviceName = "Sector A Mesh Router", Role = "Area Router", Category = "Hydroponics", IsOnline = true, X = 200, Y = 150, ParentMac = "00:1B:44:00:00:01" },
                new MeshNode { MacAddress = "00:1B:44:00:00:03", DeviceName = "Sector B Grid Router", Role = "Area Router", Category = "Power", IsOnline = true, X = 600, Y = 150, ParentMac = "00:1B:44:00:00:01" },
                new MeshNode { MacAddress = "00:1B:44:11:3A:B7", DeviceName = "Moisture Sensor B", Role = "Leaf Sensor", Category = "Hydroponics", IsOnline = true, X = 110, Y = 270, ParentMac = "00:1B:44:00:00:02" },
                new MeshNode { MacAddress = "00:1B:44:11:3A:B8", DeviceName = "Pump A", Role = "Leaf Sensor", Category = "Hydroponics", IsOnline = true, X = 260, Y = 270, ParentMac = "00:1B:44:00:00:02" },
                new MeshNode { MacAddress = "00:1B:44:11:3A:D9", DeviceName = "Greenhouse HVAC", Role = "Leaf Sensor", Category = "HVAC", IsOnline = true, X = 400, Y = 270, ParentMac = "00:1B:44:00:00:02" },
                new MeshNode { MacAddress = "00:1B:44:11:3A:C1", DeviceName = "Smart Relay Grid 1", Role = "Leaf Sensor", Category = "Power", IsOnline = true, X = 540, Y = 270, ParentMac = "00:1B:44:00:00:03" },
                new MeshNode { MacAddress = "00:1B:44:11:3A:C2", DeviceName = "Substation Meter X", Role = "Leaf Sensor", Category = "Power", IsOnline = false, X = 690, Y = 270, ParentMac = "00:1B:44:00:00:03" }
            };

            foreach (var n in initialNodes)
            {
                Nodes[n.MacAddress] = n;
                AdjacencyList[n.MacAddress] = new List<MeshEdge>();
            }

            foreach (var n in initialNodes.Where(x => !string.IsNullOrEmpty(x.ParentMac)))
            {
                if (Nodes.TryGetValue(n.ParentMac!, out var parent))
                {
                    parent.ChildMacs.Add(n.MacAddress);
                }
            }

            // Primary & redundant self-healing mesh links
            AddLink("00:1B:44:00:00:01", "00:1B:44:00:00:02", 4.2);
            AddLink("00:1B:44:00:00:01", "00:1B:44:00:00:03", 5.1);
            AddLink("00:1B:44:00:00:02", "00:1B:44:00:00:03", 8.5);
            AddLink("00:1B:44:00:00:02", "00:1B:44:11:3A:B7", 6.0);
            AddLink("00:1B:44:00:00:02", "00:1B:44:11:3A:B8", 5.5);
            AddLink("00:1B:44:00:00:02", "00:1B:44:11:3A:D9", 7.2);
            AddLink("00:1B:44:00:00:03", "00:1B:44:11:3A:D9", 9.0);
            AddLink("00:1B:44:00:00:03", "00:1B:44:11:3A:C1", 4.8);
            AddLink("00:1B:44:00:00:03", "00:1B:44:11:3A:C2", 6.4);
            AddLink("00:1B:44:11:3A:B7", "00:1B:44:11:3A:B8", 3.5);
            AddLink("00:1B:44:11:3A:B8", "00:1B:44:11:3A:D9", 6.8);
            AddLink("00:1B:44:11:3A:D9", "00:1B:44:11:3A:C1", 7.5);
            AddLink("00:1B:44:11:3A:C1", "00:1B:44:11:3A:C2", 5.9);
        }

        private void AddLink(string src, string dst, double cost)
        {
            var edge = new MeshEdge
            {
                SourceMac = src,
                TargetMac = dst,
                LatencyMs = cost,
                CostWeight = cost,
                IsActiveLink = true
            };
            Edges.Add(edge);

            if (!AdjacencyList.ContainsKey(src)) AdjacencyList[src] = new List<MeshEdge>();
            if (!AdjacencyList.ContainsKey(dst)) AdjacencyList[dst] = new List<MeshEdge>();

            AdjacencyList[src].Add(edge);
            AdjacencyList[dst].Add(new MeshEdge
            {
                SourceMac = dst,
                TargetMac = src,
                LatencyMs = cost,
                CostWeight = cost,
                IsActiveLink = true
            });
        }

        public void SetNodeOnlineState(string macAddress, bool isOnline)
        {
            lock (_lock)
            {
                if (Nodes.TryGetValue(macAddress, out var node))
                {
                    node.IsOnline = isOnline;
                }
            }
            OnTopologyChanged?.Invoke();
        }

        public void RecordLiveTelemetryInAvlTree(DeviceState device)
        {
            lock (_lock)
            {
                if (!Nodes.ContainsKey(device.MacAddress))
                {
                    string parentRouter = device.Category.Equals("Power", StringComparison.OrdinalIgnoreCase)
                        ? "00:1B:44:00:00:03"
                        : "00:1B:44:00:00:02";

                    int leafIndex = Nodes.Count;
                    var newNode = new MeshNode
                    {
                        MacAddress = device.MacAddress,
                        DeviceName = device.DeviceName,
                        Role = "Leaf Sensor",
                        Category = device.Category,
                        IsOnline = device.Status != "Offline",
                        X = 100 + ((leafIndex * 95) % 620),
                        Y = 270,
                        ParentMac = parentRouter
                    };

                    Nodes[device.MacAddress] = newNode;
                    AdjacencyList[device.MacAddress] = new List<MeshEdge>();

                    if (Nodes.TryGetValue(parentRouter, out var pNode) && !pNode.ChildMacs.Contains(device.MacAddress))
                    {
                        pNode.ChildMacs.Add(device.MacAddress);
                    }
                    AddLink(parentRouter, device.MacAddress, 6.2);
                }

                long seq = Interlocked.Increment(ref _avlSequence);
                var record = new AvlTelemetryRecord
                {
                    Timestamp = device.LastUpdated,
                    Sequence = seq,
                    MacAddress = device.MacAddress,
                    DeviceName = device.DeviceName,
                    Category = device.Category,
                    Reading = device.LastReading,
                    Unit = device.Unit
                };

                HistoricalAvlTree.Insert(record);
            }

            OnTopologyChanged?.Invoke();
        }

        public List<AvlTelemetryRecord> GetAvlInOrderRecords()
        {
            lock (_lock)
            {
                return HistoricalAvlTree.GetInOrderRecords();
            }
        }

        public List<AvlTelemetryRecord> QueryAvlTimeWindow(DateTime startUtc, DateTime endUtc)
        {
            lock (_lock)
            {
                return HistoricalAvlTree.QueryTimeWindow(startUtc, endUtc);
            }
        }

        // BFS Subnet Discovery Traversal
        public List<MeshNode> TraverseSubnetBfs(string startMac)
        {
            var result = new List<MeshNode>();
            lock (_lock)
            {
                if (!Nodes.ContainsKey(startMac)) return result;

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<string>();

                visited.Add(startMac);
                queue.Enqueue(startMac);

                while (queue.Count > 0)
                {
                    string currentMac = queue.Dequeue();
                    if (Nodes.TryGetValue(currentMac, out var node))
                    {
                        result.Add(node);
                    }

                    if (AdjacencyList.TryGetValue(currentMac, out var neighbors))
                    {
                        foreach (var edge in neighbors.Where(e => e.IsActiveLink))
                        {
                            if (Nodes.TryGetValue(edge.TargetMac, out var targetNode) && targetNode.IsOnline && visited.Add(edge.TargetMac))
                            {
                                queue.Enqueue(edge.TargetMac);
                            }
                        }
                    }
                }
            }
            return result;
        }

        // DFS Deep Diagnostic Subnet Traversal
        public List<MeshNode> TraverseSubnetDfs(string startMac)
        {
            var result = new List<MeshNode>();
            lock (_lock)
            {
                if (!Nodes.ContainsKey(startMac)) return result;

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var stack = new Stack<string>();

                stack.Push(startMac);

                while (stack.Count > 0)
                {
                    string currentMac = stack.Pop();
                    if (!visited.Add(currentMac)) continue;

                    if (Nodes.TryGetValue(currentMac, out var node))
                    {
                        result.Add(node);
                    }

                    if (AdjacencyList.TryGetValue(currentMac, out var neighbors))
                    {
                        foreach (var edge in neighbors.Where(e => e.IsActiveLink).Reverse())
                        {
                            if (Nodes.TryGetValue(edge.TargetMac, out var targetNode) && targetNode.IsOnline && !visited.Contains(edge.TargetMac))
                            {
                                stack.Push(edge.TargetMac);
                            }
                        }
                    }
                }
            }
            return result;
        }

        // Prim's Minimum Spanning Tree (MST) using BinaryMinHeap<MeshEdge>
        public List<MeshEdge> ComputeMinimumSpanningTree()
        {
            var mst = new List<MeshEdge>();
            lock (_lock)
            {
                var onlineMacs = Nodes.Values.Where(n => n.IsOnline).Select(n => n.MacAddress).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (onlineMacs.Count == 0) return mst;

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var minHeap = new BinaryMinHeap<MeshEdge>();

                string startNode = onlineMacs.Contains("00:1B:44:00:00:01") ? "00:1B:44:00:00:01" : onlineMacs.First();
                visited.Add(startNode);

                if (AdjacencyList.TryGetValue(startNode, out var startEdges))
                {
                    foreach (var edge in startEdges.Where(e => e.IsActiveLink && onlineMacs.Contains(e.TargetMac)))
                    {
                        minHeap.Insert(edge);
                    }
                }

                while (minHeap.Count > 0 && visited.Count < onlineMacs.Count)
                {
                    var minEdge = minHeap.ExtractMin();
                    if (visited.Contains(minEdge.TargetMac)) continue;

                    visited.Add(minEdge.TargetMac);

                    var canonicalEdge = Edges.FirstOrDefault(e =>
                        (string.Equals(e.SourceMac, minEdge.SourceMac, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(e.TargetMac, minEdge.TargetMac, StringComparison.OrdinalIgnoreCase)) ||
                        (string.Equals(e.SourceMac, minEdge.TargetMac, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(e.TargetMac, minEdge.SourceMac, StringComparison.OrdinalIgnoreCase)));

                    if (canonicalEdge != null)
                    {
                        mst.Add(canonicalEdge);
                    }

                    if (AdjacencyList.TryGetValue(minEdge.TargetMac, out var nextEdges))
                    {
                        foreach (var nextEdge in nextEdges.Where(e => e.IsActiveLink && onlineMacs.Contains(e.TargetMac) && !visited.Contains(e.TargetMac)))
                        {
                            minHeap.Insert(nextEdge);
                        }
                    }
                }
            }
            return mst;
        }

        // Self-Healing Multi-Hop Failover Routing between two Mesh Nodes
        public (List<string> Path, double TotalCost) FindShortestPath(string startMac, string targetMac)
        {
            lock (_lock)
            {
                var dist = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var prev = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                var unvisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var node in Nodes.Values.Where(n => n.IsOnline))
                {
                    dist[node.MacAddress] = double.PositiveInfinity;
                    prev[node.MacAddress] = null;
                    unvisited.Add(node.MacAddress);
                }

                if (!unvisited.Contains(startMac) || !unvisited.Contains(targetMac))
                    return (new List<string>(), 0);

                dist[startMac] = 0;

                while (unvisited.Count > 0)
                {
                    string? u = unvisited.OrderBy(m => dist[m]).FirstOrDefault();
                    if (u == null || double.IsPositiveInfinity(dist[u])) break;
                    unvisited.Remove(u);

                    if (string.Equals(u, targetMac, StringComparison.OrdinalIgnoreCase)) break;

                    if (AdjacencyList.TryGetValue(u, out var neighborEdges))
                    {
                        foreach (var edge in neighborEdges.Where(e => e.IsActiveLink))
                        {
                            string v = edge.TargetMac;
                            if (!unvisited.Contains(v)) continue;

                            double alt = dist[u] + edge.CostWeight;
                            if (alt < dist[v])
                            {
                                dist[v] = alt;
                                prev[v] = u;
                            }
                        }
                    }
                }

                var path = new List<string>();
                string? curr = targetMac;
                if (prev.GetValueOrDefault(curr) != null || string.Equals(curr, startMac, StringComparison.OrdinalIgnoreCase))
                {
                    while (curr != null)
                    {
                        path.Insert(0, curr);
                        curr = prev.GetValueOrDefault(curr);
                    }
                }

                return (path, path.Count > 0 ? Math.Round(dist[targetMac], 1) : 0);
            }
        }
    }
}
