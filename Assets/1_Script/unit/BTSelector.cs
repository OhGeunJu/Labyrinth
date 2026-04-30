using System.Collections.Generic;

public class BTSelector : BTComposite
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        foreach (var child in _children)
        {
            var status = child.Tick(bb);
            if (status != BTStatus.Failure) return status;
        }
        return BTStatus.Failure;
    }
}