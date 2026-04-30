public class MineJob : JobBase
{
    public override JobCategory Category => JobCategory.Mining;

    readonly ResourceNode _node;

    public MineJob(ResourceNode node)
    {
        _node = node;
        TileCoord = node.TileCoord;
        Priority = 1;
    }

    protected override void OnStart()
    {
        UnityEngine.Debug.Log($"[MineJob] 채굴 시작: {_node?.TileCoord}");
    }

    protected override void OnCancelled()
    {
        if (_node != null)
            _node.IsDesignated = false;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_node == null)
        {
            UnityEngine.Debug.Log($"[MineJob] 채굴 완료: {TileCoord}");
            Status = JobStatus.Done;
            return;
        }
        _node.Work(unit.Stats.Mining);
    }
}
