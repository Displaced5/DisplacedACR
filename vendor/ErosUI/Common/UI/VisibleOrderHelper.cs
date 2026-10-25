using System.Numerics;

namespace ErosUI;

// 面板拖拽排序共用小件：拖拽中的显示序列换算 + 格位让位动画。
// QT 面板与热键面板共用。
public static class VisibleOrderHelper
{
    // 槽位让位动画的默认指数阻尼速度（每秒）。
    public const float DefaultAnimationSpeed = 18f;

    // 拖拽中的显示序列：result[displayIndex] = itemIndex；把拖拽源抽出插入目标位，其余保持相对顺序
    // （与 MoveQt/MoveHotkey 的可见子序列插入语义一致，拖拽视觉与松手落位不位移）。
    // 无拖拽（source/target 为负或相等）时返回恒等序列。scratch 长度不等于 count 时重新分配。
    public static int[] BuildDisplayOrder(int count, int sourceIndex, int targetIndex, int[] scratch)
    {
        if (scratch.Length != count)
            scratch = new int[count];

        var dragging = sourceIndex >= 0 && targetIndex >= 0 && targetIndex != sourceIndex
            && sourceIndex < count;
        if (!dragging)
        {
            for (var index = 0; index < count; index++)
                scratch[index] = index;
            return scratch;
        }

        var write = 0;
        for (var index = 0; index < count; index++)
        {
            if (index != sourceIndex)
                scratch[write++] = index;
        }

        var target = Math.Clamp(targetIndex, 0, count - 1);
        for (var index = count - 1; index > target; index--)
            scratch[index] = scratch[index - 1];
        scratch[target] = sourceIndex;
        return scratch;
    }

    // 位置指数阻尼收敛：返回逼近 target 的下一帧位置，距离小于 0.1px 时直接吸附。
    public static Vector2 AnimatePosition(Vector2 current, Vector2 target, float deltaTime, float speed = DefaultAnimationSpeed)
    {
        var step = 1f - MathF.Exp(-MathF.Max(0.01f, speed) * Math.Clamp(deltaTime, 0f, 0.1f));
        var next = current + (target - current) * step;
        return Vector2.DistanceSquared(next, target) < 0.01f ? target : next;
    }
}
