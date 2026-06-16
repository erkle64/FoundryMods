using PlanIt;
using System.Collections.Generic;
using System.Linq;

namespace Planit
{
    internal static class DictionaryExtensions
    {
        public static HashSet<TValueElement> GetOrAddHashSet<TKey, TValueElement>(this IDictionary<TKey, HashSet<TValueElement>> dictionary, TKey key)
        {
            if (!dictionary.TryGetValue(key, out HashSet<TValueElement> set))
            {
                set = new HashSet<TValueElement>();
                dictionary.Add(key, set);
            }
            return set;
        }
    }

    public static class TopoSort<T> where T : struct
    {
        public static List<HashSet<T>> AcyclicTopoSort(HashSet<(T, T)> edges)
        {
            var sortedResult = new List<HashSet<T>>();
            var nodeIns = new Dictionary<T, HashSet<T>>();
            var nodeOuts = new Dictionary<T, HashSet<T>>();
            var allNodes = new HashSet<T>();

            foreach (var edge in edges)
            {
                T startNode = edge.Item1;
                T endNode = edge.Item2;

                allNodes.Add(startNode);
                allNodes.Add(endNode);

                nodeIns.GetOrAddHashSet(endNode).Add(startNode);
                nodeOuts.GetOrAddHashSet(startNode).Add(endNode);

                nodeIns.GetOrAddHashSet(startNode);
                nodeOuts.GetOrAddHashSet(endNode);
            }

            var inDegree = allNodes.ToDictionary(node => node, node => nodeIns.GetOrAddHashSet(node).Count);

            var queue = new Queue<T>(allNodes.Where(node => inDegree[node] == 0));

            int processedNodes = 0;
            while (queue.Count > 0)
            {
                var currentLevelNodes = new HashSet<T>();
                int levelSize = queue.Count;
                for (int i = 0; i < levelSize; i++)
                {
                    T node = queue.Dequeue();
                    currentLevelNodes.Add(node);
                    processedNodes++;

                    if (nodeOuts.TryGetValue(node, out var neighbors))
                    {
                        foreach (T neighbor in neighbors)
                        {
                            if (inDegree.ContainsKey(neighbor))
                            {
                                inDegree[neighbor]--;
                                if (inDegree[neighbor] == 0)
                                {
                                    queue.Enqueue(neighbor);
                                }
                            }
                        }
                    }
                }
                if (currentLevelNodes.Count > 0)
                {
                    sortedResult.Add(currentLevelNodes);
                }
            }

            if (processedNodes != allNodes.Count)
            {
                PlanItSystem.log.LogWarning("Warning: Cycle detected in supposedly acyclic graph input to PerformAcyclicTopoSort.");
            }

            return sortedResult;
        }

        // NEW: Returns a single ordered list, biased to keep connected nodes close.
        public static List<T> CyclicTopoSort(HashSet<(T, T)> edges, T? startNode = null)
        {
            var nodeIns = new Dictionary<T, HashSet<T>>();
            var nodeOuts = new Dictionary<T, HashSet<T>>();
            var cyclicEdgesForced = new HashSet<(T, T)>();

            foreach (var edge in edges)
            {
                T edgeStart = edge.Item1;
                T edgeEnd = edge.Item2;

                if (edgeStart.Equals(edgeEnd))
                    continue;

                nodeIns.GetOrAddHashSet(edgeStart);
                nodeOuts.GetOrAddHashSet(edgeEnd);
                nodeIns.GetOrAddHashSet(edgeEnd);
                nodeOuts.GetOrAddHashSet(edgeStart);

                if (startNode.HasValue && startNode.Value.Equals(edgeEnd))
                {
                    cyclicEdgesForced.Add((edgeStart, edgeEnd));
                    continue;
                }

                nodeIns.GetOrAddHashSet(edgeEnd).Add(edgeStart);
                nodeOuts.GetOrAddHashSet(edgeStart).Add(edgeEnd);
            }

            List<HashSet<(T, T)>> cyclicEdges = CyclicTopoSortRecursive(nodeIns, nodeOuts);

            if (cyclicEdgesForced.Any())
            {
                foreach (var cyclicEdgesSet in cyclicEdges)
                {
                    cyclicEdgesSet.UnionWith(cyclicEdgesForced);
                }
            }

            if (!cyclicEdges.Any())
            {
                return new List<T>();
            }

            List<T> bestOrder = null;
            long bestScore = long.MaxValue;

            foreach (var cyclicEdgesSet in cyclicEdges)
            {
                var currentAcyclicEdges = new HashSet<(T, T)>(edges);
                currentAcyclicEdges.ExceptWith(cyclicEdgesSet);

                var order = AcyclicTopoSortLinearPreferNearby(currentAcyclicEdges);

                // Primary: fewer “breaks” between consecutive connected nodes.
                // Secondary: prefer shorter forward distances for edges.
                long score = ScoreLinearCloseness(order, currentAcyclicEdges);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestOrder = order;
                }
            }

            return bestOrder ?? new List<T>();
        }

        private static List<T> AcyclicTopoSortLinearPreferNearby(HashSet<(T, T)> edges)
        {
            var nodeIns = new Dictionary<T, HashSet<T>>();
            var nodeOuts = new Dictionary<T, HashSet<T>>();
            var allNodes = new HashSet<T>();

            // Also build undirected adjacency just for the “nearby” heuristic.
            var adjacency = new Dictionary<T, HashSet<T>>();

            foreach (var (startNode, endNode) in edges)
            {
                allNodes.Add(startNode);
                allNodes.Add(endNode);

                nodeIns.GetOrAddHashSet(endNode).Add(startNode);
                nodeOuts.GetOrAddHashSet(startNode).Add(endNode);

                nodeIns.GetOrAddHashSet(startNode);
                nodeOuts.GetOrAddHashSet(endNode);

                adjacency.GetOrAddHashSet(startNode).Add(endNode);
                adjacency.GetOrAddHashSet(endNode).Add(startNode);
            }

            var inDegree = allNodes.ToDictionary(node => node, node => nodeIns.GetOrAddHashSet(node).Count);

            // Available nodes with inDegree==0
            var available = new HashSet<T>(allNodes.Where(n => inDegree[n] == 0));
            var result = new List<T>(allNodes.Count);

            // Track a small window of recent nodes to keep chains tight.
            var recent = new Queue<T>(8);

            while (available.Count > 0)
            {
                // Pick the next node: prefer one adjacent to something recently emitted;
                // within that, prefer one that unlocks many nodes (out-degree).
                T next = ChooseNext(available, recent, adjacency, nodeOuts);

                available.Remove(next);
                result.Add(next);

                recent.Enqueue(next);
                while (recent.Count > 8)
                    recent.Dequeue();

                if (nodeOuts.TryGetValue(next, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        inDegree[neighbor]--;
                        if (inDegree[neighbor] == 0)
                        {
                            available.Add(neighbor);
                        }
                    }
                }
            }

            if (result.Count != allNodes.Count)
            {
                PlanItSystem.log.LogWarning("Warning: Cycle detected in supposedly acyclic graph input to AcyclicTopoSortLinearPreferNearby.");
            }

            return result;
        }

        private static T ChooseNext(
            HashSet<T> available,
            Queue<T> recent,
            Dictionary<T, HashSet<T>> adjacency,
            Dictionary<T, HashSet<T>> nodeOuts)
        {
            // 1) If something is adjacent to the most recent nodes, pick from that set.
            foreach (var r in recent.Reverse())
            {
                if (!adjacency.TryGetValue(r, out var neigh))
                    continue;

                // Among adjacent candidates, prefer higher out-degree (unlocks more).
                bool foundAny = false;
                T best = default;
                int bestOut = -1;

                foreach (var c in available)
                {
                    if (!neigh.Contains(c))
                        continue;

                    foundAny = true;
                    int outDeg = nodeOuts.TryGetValue(c, out var outs) ? outs.Count : 0;
                    if (outDeg > bestOut)
                    {
                        bestOut = outDeg;
                        best = c;
                    }
                }

                if (foundAny)
                    return best;
            }

            // 2) Otherwise pick a node that unlocks many nodes.
            return available
                .OrderByDescending(c => nodeOuts.TryGetValue(c, out var outs) ? outs.Count : 0)
                .First();
        }

        private static long ScoreLinearCloseness(List<T> order, HashSet<(T, T)> edges)
        {
            var indexOf = new Dictionary<T, int>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                indexOf[order[i]] = i;
            }

            // Undirected edges for “consecutive connectivity”
            var undirected = new HashSet<(T, T)>();
            foreach (var (a, b) in edges)
            {
                undirected.Add((a, b));
                undirected.Add((b, a));
            }

            long score = 0;

            // Penalize when consecutive nodes are not connected.
            for (int i = 0; i + 1 < order.Count; i++)
            {
                if (!undirected.Contains((order[i], order[i + 1])))
                    score += 10;
            }

            // Penalize long forward spans (keeps directly connected nodes close in the order).
            foreach (var (from, to) in edges)
            {
                if (!indexOf.TryGetValue(from, out int a) || !indexOf.TryGetValue(to, out int b))
                    continue;

                int span = b - a;
                if (span > 1)
                    score += (long)span * (long)span;
            }

            return score;
        }

        private static List<HashSet<(T, T)>> CyclicTopoSortRecursive(Dictionary<T, HashSet<T>> nodeIns, Dictionary<T, HashSet<T>> nodeOuts)
        {
            var current_node_ins = nodeIns.ToDictionary(kvp => kvp.Key, kvp => new HashSet<T>(kvp.Value));
            var current_node_outs = nodeOuts.ToDictionary(kvp => kvp.Key, kvp => new HashSet<T>(kvp.Value));

            var cyclicEdges = new List<HashSet<(T, T)>> { new HashSet<(T, T)>() };

            while (true)
            {
                var dependencyless = current_node_ins.Where(kvp => kvp.Value.Count == 0)
                                                     .Select(kvp => kvp.Key)
                                                     .ToHashSet();

                if (dependencyless.Count == 0)
                {
                    while (true)
                    {
                        var followerless = current_node_outs.Where(kvp => kvp.Value.Count == 0)
                                                            .Select(kvp => kvp.Key)
                                                            .ToHashSet();

                        if (followerless.Count == 0)
                        {
                            int minNumberCyclicEdges = int.MaxValue;

                            var currentEdges = new HashSet<(T, T)>();
                            foreach (var kvp in current_node_ins)
                            {
                                T edgeEnd = kvp.Key;
                                foreach (T edgeStart in kvp.Value)
                                {
                                    currentEdges.Add((edgeStart, edgeEnd));
                                }
                            }

                            if (!currentEdges.Any())
                            {
                                return cyclicEdges;
                            }

                            var resolvedCyclicEdges = new List<HashSet<(T, T)>>();

                            foreach (var reductionResult in GenerateReducedInsOuts(currentEdges, current_node_ins, current_node_outs))
                            {
                                var reducedNodeIns = reductionResult.Item1;
                                var reducedNodeOuts = reductionResult.Item2;
                                var forcedCyclicEdges = reductionResult.Item3;

                                if (forcedCyclicEdges.Count > minNumberCyclicEdges && minNumberCyclicEdges != int.MaxValue)
                                {
                                    break;
                                }

                                List<HashSet<(T, T)>> reducedCyclicEdges = CyclicTopoSortRecursive(reducedNodeIns, reducedNodeOuts);

                                int totalCyclicEdges = forcedCyclicEdges.Count + (reducedCyclicEdges.Any() ? reducedCyclicEdges[0].Count : 0);

                                if (totalCyclicEdges < minNumberCyclicEdges)
                                {
                                    minNumberCyclicEdges = totalCyclicEdges;
                                    resolvedCyclicEdges.Clear();
                                    foreach (var reducedCyclicEdgesSet in reducedCyclicEdges)
                                    {
                                        var combinedSet = new HashSet<(T, T)>(reducedCyclicEdgesSet);
                                        combinedSet.UnionWith(forcedCyclicEdges);
                                        resolvedCyclicEdges.Add(combinedSet);
                                    }
                                }
                                else if (totalCyclicEdges == minNumberCyclicEdges)
                                {
                                    foreach (var reducedCyclicEdgesSet in reducedCyclicEdges)
                                    {
                                        var combinedSet = new HashSet<(T, T)>(reducedCyclicEdgesSet);
                                        combinedSet.UnionWith(forcedCyclicEdges);
                                        resolvedCyclicEdges.Add(combinedSet);
                                    }
                                }
                            }

                            return resolvedCyclicEdges;
                        }

                        foreach (T node in followerless)
                        {
                            current_node_ins.Remove(node);
                            current_node_outs.Remove(node);
                        }

                        var keysToUpdateOuts = current_node_outs.Keys.ToList();
                        foreach (var node in keysToUpdateOuts)
                        {
                            if (current_node_outs.TryGetValue(node, out var outgoings))
                            {
                                outgoings.ExceptWith(followerless);
                            }
                        }
                    }
                }

                foreach (T node in dependencyless)
                {
                    current_node_ins.Remove(node);
                    current_node_outs.Remove(node);
                }

                if (current_node_ins.Count == 0)
                {
                    break;
                }

                var keysToUpdateIns = current_node_ins.Keys.ToList();
                foreach (var node in keysToUpdateIns)
                {
                    if (current_node_ins.TryGetValue(node, out var incomings))
                    {
                        incomings.ExceptWith(dependencyless);
                    }
                }
            }

            return cyclicEdges;
        }

        private static IEnumerable<(Dictionary<T, HashSet<T>>, Dictionary<T, HashSet<T>>, HashSet<(T, T)>)> GenerateReducedInsOuts(HashSet<(T, T)> edges, Dictionary<T, HashSet<T>> nodeIns, Dictionary<T, HashSet<T>> nodeOuts)
        {
            foreach (var edgeToRemove in edges)
            {
                var forcedCyclicEdges = new HashSet<(T, T)> { edgeToRemove };

                var reducedNodeIns = nodeIns.ToDictionary(kvp => kvp.Key, kvp => new HashSet<T>(kvp.Value));
                var reducedNodeOuts = nodeOuts.ToDictionary(kvp => kvp.Key, kvp => new HashSet<T>(kvp.Value));

                T startNode = edgeToRemove.Item1;
                T endNode = edgeToRemove.Item2;

                if (reducedNodeIns.TryGetValue(endNode, out var endIns))
                {
                    endIns.Remove(startNode);
                }
                if (reducedNodeOuts.TryGetValue(startNode, out var startOuts))
                {
                    startOuts.Remove(endNode);
                }

                yield return (reducedNodeIns, reducedNodeOuts, forcedCyclicEdges);
            }
        }
    }
}