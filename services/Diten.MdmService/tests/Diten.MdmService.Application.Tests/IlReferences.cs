using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Diten.MdmService.Tests.Shared;

/// <summary>
/// MOD-0290-FU04 S3b FIX1 — what compiled code reaches, read from its IL instead of its source text: every method and
/// field a type's methods call, take a delegate to or read, following the compiler-made parts (async / iterator state
/// machines, lambdas) and, when asked, transitively every method of the same assembly they reach. A rename, an alias or a helper in
/// between does not hide a call from it; nor does a call through an interface or a virtual method (S3b FIX2: followed to
/// what implements it in the same assembly). A <c>typeof(T)</c> (ldtoken) is reached as the type itself. Not seen: a
/// member found by a string at run time (Type.GetType("…"), reflection by name) — no IL names it.
/// </summary>
internal static class IlReferences
{
    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(x => (OpCode)x.GetValue(null)!)
        .ToDictionary(x => x.Value);

    // transitive: false reads only the type's own methods (and their compiler-made parts) — "does THIS type call it".
    public static IReadOnlyCollection<MemberInfo> MembersReachedFrom(Type type, bool transitive = true)
    {
        var reached = new HashSet<MemberInfo>();
        var visited = new HashSet<MethodBase>();
        var pending = new Stack<MethodBase>(MethodsOf(type));
        while (pending.Count > 0)
        {
            var method = pending.Pop();
            if (!visited.Add(method)) continue;
            foreach (var stateMachine in method.GetCustomAttributes<StateMachineAttribute>())
                foreach (var part in MethodsOf(stateMachine.StateMachineType)) pending.Push(part);
            foreach (var member in Referenced(method))
            {
                reached.Add(member);
                if (transitive && member is MethodBase callee && callee.Module.Assembly == type.Assembly)
                {
                    pending.Push(callee);
                    // S3b FIX2: a call through an interface or a virtual method reaches what implements / overrides it
                    // in the same assembly (the IL names only the declared method).
                    if (callee is MethodInfo { IsVirtual: true } declared)
                        foreach (var implementation in Implementations(declared, type.Assembly)) pending.Push(implementation);
                }
            }
        }
        return reached;
    }

    private static IEnumerable<MethodBase> Implementations(MethodInfo declared, Assembly assembly)
    {
        var owner = declared.DeclaringType!;
        foreach (var candidate in assembly.GetTypes().Where(x => x is { IsInterface: false, ContainsGenericParameters: false }))
        {
            if (owner.IsInterface)
            {
                if (!owner.IsAssignableFrom(candidate) || candidate.IsAbstract) continue;
                var map = candidate.GetInterfaceMap(owner);
                var index = Array.IndexOf(map.InterfaceMethods, declared);
                if (index >= 0) yield return map.TargetMethods[index];
            }
            else if (candidate.IsSubclassOf(owner))
            {
                foreach (var method in candidate.GetMethods(Everything)
                             .Where(x => x != declared && x.GetBaseDefinition() == declared.GetBaseDefinition()))
                    yield return method;
            }
        }
    }

    private static IEnumerable<MethodBase> MethodsOf(Type type) =>
        type.GetMethods(Everything).Cast<MethodBase>()
            .Concat(type.GetConstructors(Everything))
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(MethodsOf));

    private static IEnumerable<MemberInfo> Referenced(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;
        var genericTypeArguments = method.DeclaringType is { IsGenericType: true } owner ? owner.GetGenericArguments() : null;
        var genericMethodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var position = 0;
        while (position < il.Length)
        {
            var value = il[position] == 0xFE ? (short)(0xFE00 | il[position + 1]) : il[position];
            position += il[position] == 0xFE ? 2 : 1;
            var opCode = OpCodesByValue[value];
            switch (opCode.OperandType)
            {
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                case OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, position);
                    MemberInfo? member = null;
                    try { member = method.Module.ResolveMember(token, genericTypeArguments, genericMethodArguments); }
                    catch (ArgumentException) { }
                    if (member is not null) yield return member;
                    position += 4;
                    break;
                case OperandType.InlineSwitch:
                    position += 4 + 4 * BitConverter.ToInt32(il, position);
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    position += 1;
                    break;
                case OperandType.InlineVar:
                    position += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    position += 8;
                    break;
                default:
                    position += 4;
                    break;
            }
        }
    }
}
