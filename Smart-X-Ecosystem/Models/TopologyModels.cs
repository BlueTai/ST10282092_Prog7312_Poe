using System;
using System.Collections.Generic;

namespace Smart_X_Ecosystem.Models
{
    public class MeshNode
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Role { get; set; } = "Leaf Sensor"; // Root Gateway, Area Router, Leaf Sensor
        public string Category { get; set; } = "Power";
        public bool IsOnline { get; set; } = true;
        public double X { get; set; }
        public double Y { get; set; }
        public string? ParentMac { get; set; }
        public List<string> ChildMacs { get; set; } = new();
    }

    public class MeshEdge : IComparable<MeshEdge>
    {
        public string SourceMac { get; set; } = string.Empty;
        public string TargetMac { get; set; } = string.Empty;
        public double LatencyMs { get; set; }
        public double CostWeight { get; set; }
        public bool IsActiveLink { get; set; } = true;

        public int CompareTo(MeshEdge? other)
        {
            if (other == null) return 1;
            return CostWeight.CompareTo(other.CostWeight);
        }
    }

    /// <summary>
    /// Custom Binary Min-Heap used by Prim's Minimum Spanning Tree (MST) and Dijkstra Failover Routing.
    /// </summary>
    public class BinaryMinHeap<T> where T : IComparable<T>
    {
        private readonly List<T> _elements = new();

        public int Count => _elements.Count;

        public void Insert(T item)
        {
            _elements.Add(item);
            int ci = _elements.Count - 1;
            while (ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (_elements[ci].CompareTo(_elements[pi]) >= 0) break;
                (_elements[ci], _elements[pi]) = (_elements[pi], _elements[ci]);
                ci = pi;
            }
        }

        public T ExtractMin()
        {
            if (_elements.Count == 0)
                throw new InvalidOperationException("BinaryMinHeap is empty.");

            T frontItem = _elements[0];
            int li = _elements.Count - 1;
            _elements[0] = _elements[li];
            _elements.RemoveAt(li);

            li--;
            int pi = 0;
            while (true)
            {
                int ci = pi * 2 + 1;
                if (ci > li) break;
                int rc = ci + 1;
                if (rc <= li && _elements[rc].CompareTo(_elements[ci]) < 0)
                    ci = rc;
                if (_elements[pi].CompareTo(_elements[ci]) <= 0) break;
                (_elements[pi], _elements[ci]) = (_elements[ci], _elements[pi]);
                pi = ci;
            }
            return frontItem;
        }
    }

    public class AvlTelemetryRecord : IComparable<AvlTelemetryRecord>
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public long Sequence { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Reading { get; set; }
        public string Unit { get; set; } = string.Empty;

        public int CompareTo(AvlTelemetryRecord? other)
        {
            if (other == null) return 1;
            int cmp = Timestamp.CompareTo(other.Timestamp);
            if (cmp != 0) return cmp;
            cmp = Sequence.CompareTo(other.Sequence);
            if (cmp != 0) return cmp;
            return string.Compare(MacAddress, other.MacAddress, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class AvlNode
    {
        public AvlTelemetryRecord Data { get; set; }
        public AvlNode? Left { get; set; }
        public AvlNode? Right { get; set; }
        public int Height { get; set; } = 1;

        public AvlNode(AvlTelemetryRecord data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Self-Balancing AVL Tree supporting O(log n) insertion and O(log n + k) time-window range queries.
    /// </summary>
    public class HistoricalAvlTree
    {
        public AvlNode? Root { get; private set; }
        public int Count { get; private set; }
        public int RotationsPerformed { get; private set; }

        public int TreeHeight => GetHeight(Root);

        private static int GetHeight(AvlNode? node) => node?.Height ?? 0;

        private static int GetBalance(AvlNode? node) => node == null ? 0 : GetHeight(node.Left) - GetHeight(node.Right);

        public void Insert(AvlTelemetryRecord record)
        {
            Root = InsertNode(Root, record);
            Count++;
        }

        private AvlNode RotateRight(AvlNode y)
        {
            RotationsPerformed++;
            AvlNode x = y.Left!;
            AvlNode? t2 = x.Right;

            x.Right = y;
            y.Left = t2;

            y.Height = Math.Max(GetHeight(y.Left), GetHeight(y.Right)) + 1;
            x.Height = Math.Max(GetHeight(x.Left), GetHeight(x.Right)) + 1;
            return x;
        }

        private AvlNode RotateLeft(AvlNode x)
        {
            RotationsPerformed++;
            AvlNode y = x.Right!;
            AvlNode? t2 = y.Left;

            y.Left = x;
            x.Right = t2;

            x.Height = Math.Max(GetHeight(x.Left), GetHeight(x.Right)) + 1;
            y.Height = Math.Max(GetHeight(y.Left), GetHeight(y.Right)) + 1;
            return y;
        }

        private AvlNode InsertNode(AvlNode? node, AvlTelemetryRecord data)
        {
            if (node == null) return new AvlNode(data);

            int cmp = data.CompareTo(node.Data);
            if (cmp < 0)
                node.Left = InsertNode(node.Left, data);
            else if (cmp > 0)
                node.Right = InsertNode(node.Right, data);
            else
                return node;

            node.Height = 1 + Math.Max(GetHeight(node.Left), GetHeight(node.Right));
            int balance = GetBalance(node);

            if (balance > 1 && data.CompareTo(node.Left!.Data) < 0)
                return RotateRight(node);

            if (balance < -1 && data.CompareTo(node.Right!.Data) > 0)
                return RotateLeft(node);

            if (balance > 1 && data.CompareTo(node.Left!.Data) > 0)
            {
                node.Left = RotateLeft(node.Left!);
                return RotateRight(node);
            }

            if (balance < -1 && data.CompareTo(node.Right!.Data) < 0)
            {
                node.Right = RotateRight(node.Right!);
                return RotateLeft(node);
            }

            return node;
        }

        public List<AvlTelemetryRecord> QueryTimeWindow(DateTime startUtc, DateTime endUtc)
        {
            var results = new List<AvlTelemetryRecord>();
            RangeSearch(Root, startUtc, endUtc, results);
            return results;
        }

        private void RangeSearch(AvlNode? node, DateTime startUtc, DateTime endUtc, List<AvlTelemetryRecord> results)
        {
            if (node == null) return;

            if (node.Data.Timestamp >= startUtc)
            {
                RangeSearch(node.Left, startUtc, endUtc, results);
            }

            if (node.Data.Timestamp >= startUtc && node.Data.Timestamp <= endUtc)
            {
                results.Add(node.Data);
            }

            if (node.Data.Timestamp <= endUtc)
            {
                RangeSearch(node.Right, startUtc, endUtc, results);
            }
        }

        public List<AvlTelemetryRecord> GetInOrderRecords()
        {
            var list = new List<AvlTelemetryRecord>();
            InOrderTraversal(Root, list);
            return list;
        }

        private void InOrderTraversal(AvlNode? node, List<AvlTelemetryRecord> result)
        {
            if (node == null) return;
            InOrderTraversal(node.Left, result);
            result.Add(node.Data);
            InOrderTraversal(node.Right, result);
        }
    }
}
