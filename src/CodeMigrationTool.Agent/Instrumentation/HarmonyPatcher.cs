using System.Reflection;
using HarmonyLib;

namespace CodeMigrationTool.Agent.Instrumentation;

/// <summary>
/// Uses Harmony to dynamically patch methods at runtime for interception.
/// Installs Prefix/Postfix hooks that feed data into the ExecutionTracer.
/// </summary>
public class HarmonyPatcher
{
    private readonly Harmony _harmony;
    private readonly ExecutionTracer _tracer;
    private readonly HashSet<string> _patchedMethods = [];

    // Static reference so Harmony patches (which must be static) can access the tracer
    private static ExecutionTracer? s_activeTracer;

    public HarmonyPatcher(ExecutionTracer tracer)
    {
        _harmony = new Harmony("com.codemigrationtool.agent");
        _tracer = tracer;
        s_activeTracer = tracer;
    }

    public void PatchMethod(MethodInfo targetMethod)
    {
        var methodKey = $"{targetMethod.DeclaringType?.FullName}.{targetMethod.Name}";

        if (!_patchedMethods.Add(methodKey))
            return; // Already patched

        var prefix = typeof(HarmonyPatcher).GetMethod(nameof(PrefixHook),
            BindingFlags.Static | BindingFlags.NonPublic);
        var postfix = typeof(HarmonyPatcher).GetMethod(nameof(PostfixHook),
            BindingFlags.Static | BindingFlags.NonPublic);

        _harmony.Patch(targetMethod,
            prefix: prefix != null ? new HarmonyMethod(prefix) : null,
            postfix: postfix != null ? new HarmonyMethod(postfix) : null);
    }

    public void PatchAllMethodsInType(Type type)
    {
        var methods = type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            if (method.IsAbstract || method.IsGenericMethod)
                continue;

            try
            {
                PatchMethod(method);
            }
            catch (Exception ex)
            {
                // Some methods may not be patchable; log and continue
                Console.Error.WriteLine($"Warning: Could not patch {method.Name}: {ex.Message}");
            }
        }
    }

    public void UnpatchAll()
    {
        _harmony.UnpatchAll(_harmony.Id);
        _patchedMethods.Clear();
    }

    private static void PrefixHook(MethodBase __originalMethod, object?[]? __args)
    {
        s_activeTracer?.OnMethodEntry(__originalMethod, __args);
    }

    private static void PostfixHook(MethodBase __originalMethod, object? __result, Exception? __exception)
    {
        s_activeTracer?.OnMethodExit(__originalMethod, __result, __exception);
    }
}
