using System.Collections.Generic;

public class BTSequence : BTComposite
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        foreach (var child in _children)
        {
            var status = child.Tick(bb);
            if (status != BTStatus.Success) return status;
        }
        return BTStatus.Success;
    }
}