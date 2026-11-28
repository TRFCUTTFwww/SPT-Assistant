using LocalSpt5Esp;

int checks = 0, reads = 0;
void Validate(params Node[] items)
{
    reads = 0;
    OnlineOfferAncestry.Validate(items, item => item.Id, item => item.IsRoot, item =>
    {
        // Deterministically fail a regression instead of letting the test process hang.
        if (++reads > 200) throw new InvalidOperationException("Unbounded ancestry walk");
        if (item.IsRoot) throw new InvalidOperationException("Read parent past inventory ownership boundary");
        return item.Parent;
    });
    checks++;
}
void Reject(string message, params Node[] items)
{
    try { Validate(items); }
    catch (ArgumentException error) when (error.Message.Contains(message)) { checks++; return; }
    throw new Exception("Expected rejection: " + message);
}

// The native ItemController assigns an address to RootItem and returns that same
// RootItem from IContainer.ParentItem; reaching it is normal even for one item.
var root = new Node("inventory", true);
root.Parent = root;
var bag = new Node("bag") { Parent = root };
var first = new Node("first") { Parent = bag };
var second = new Node("second") { Parent = bag };
Validate(first);
if (reads != 2) throw new Exception("Single item must terminate at root");
Validate(first, second);
Validate(root);
Validate(new Node("detached"));
Reject("重复", first, first);
Reject("内部物品", bag, first);
Reject("内部物品", first, bag);
Reject("内部物品", root, first);
var loop = new Node("loop");
loop.Parent = loop;
Reject("循环", loop);
var a = new Node("a");
var b = new Node("b") { Parent = a };
a.Parent = b;
Reject("循环", new Node("child") { Parent = a });
var tooDeep = root;
for (int i = 0; i < 70; i++) tooDeep = new Node("deep" + i) { Parent = tooDeep };
Reject("嵌套过深", tooDeep);
Console.WriteLine($"PASS: {checks} online offer ancestry checks");

sealed class Node(string id, bool isRoot = false)
{
    internal string Id = id;
    internal bool IsRoot = isRoot;
    internal Node? Parent;
}
