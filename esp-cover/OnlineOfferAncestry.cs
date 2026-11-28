using System;
using System.Collections.Generic;

namespace LocalSpt5Esp;

internal static class OnlineOfferAncestry
{
    // EFT roots belong to an ItemController whose ParentItem is the root itself.
    // A root is an ownership boundary, not another parent to traverse.
    internal static void Validate<T>(IReadOnlyList<T> selected, Func<T, string> id,
        Func<T, bool> isRoot, Func<T, T?> parent) where T : class
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in selected)
            if (!ids.Add(id(item))) throw new ArgumentException("上架物品重复，请重新选择。");
        foreach (var item in selected)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { id(item) };
            var current = item;
            int depth = 0;
            while (!isRoot(current))
            {
                var next = parent(current);
                if (next == null) break;
                string key = id(next);
                if (!seen.Add(key)) throw new ArgumentException("物品容器关系存在循环，请关闭报价窗口后重新选择。");
                if (++depth > 64) throw new ArgumentException("物品容器嵌套过深，无法上架。");
                if (ids.Contains(key)) throw new ArgumentException("不能同时上架容器及其内部物品。");
                current = next;
            }
        }
    }
}
