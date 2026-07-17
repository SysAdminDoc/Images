namespace Images.Services;

public sealed record FaceClusterMember(
    string SourcePath,
    int FaceIndex,
    FaceDetection Detection);

public sealed record FaceCluster(
    int ClusterId,
    IReadOnlyList<FaceClusterMember> Members);

public sealed record FaceClusterResult(
    IReadOnlyList<FaceCluster> Clusters,
    IReadOnlyList<FaceEmbedding> RejectedFaces);

public static class FaceClusterService
{
    // OpenCV's SFace model card publishes 0.363 cosine similarity as the LFW same-identity
    // operating threshold. Keep it visible and caller-adjustable instead of hiding a magic gate.
    public const double DefaultSimilarityThreshold = 0.363;

    public static FaceClusterResult Cluster(
        IEnumerable<FaceEmbedding> embeddings,
        double similarityThreshold = DefaultSimilarityThreshold)
    {
        ArgumentNullException.ThrowIfNull(embeddings);
        if (similarityThreshold is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(similarityThreshold));

        var all = embeddings.ToArray();
        var accepted = all.Where(face => face.IsAccepted).ToArray();
        var parents = Enumerable.Range(0, accepted.Length).ToArray();
        for (var left = 0; left < accepted.Length; left++)
        {
            for (var right = left + 1; right < accepted.Length; right++)
            {
                if (FaceRecognitionService.CosineSimilarity(
                        accepted[left].Vector!,
                        accepted[right].Vector!) >= similarityThreshold)
                {
                    Union(parents, left, right);
                }
            }
        }

        var groups = accepted
            .Select((face, index) => (Face: face, Root: Find(parents, index)))
            .GroupBy(item => item.Root)
            .OrderBy(group => group.Min(item => Array.IndexOf(accepted, item.Face)))
            .Select((group, clusterIndex) => new FaceCluster(
                clusterIndex + 1,
                group.Select(item => new FaceClusterMember(
                    item.Face.SourcePath,
                    item.Face.FaceIndex,
                    item.Face.Detection)).ToArray()))
            .ToArray();

        return new FaceClusterResult(groups, all.Where(face => !face.IsAccepted).ToArray());
    }

    private static int Find(int[] parents, int value)
    {
        while (parents[value] != value)
        {
            parents[value] = parents[parents[value]];
            value = parents[value];
        }
        return value;
    }

    private static void Union(int[] parents, int left, int right)
    {
        var leftRoot = Find(parents, left);
        var rightRoot = Find(parents, right);
        if (leftRoot != rightRoot)
            parents[rightRoot] = leftRoot;
    }
}
