using System.Collections.Generic;

public enum BTStatus { Success, Failure, Running }

public abstract class BTNode
{
    public abstract BTStatus Tick(UnitBlackboard bb);
}

public abstract class BTComposite : BTNode
{
    protected readonly List<BTNode> _children = new();

    public BTComposite Add(BTNode node)
    {
        _children.Add(node);
        return this;
    }
}