using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Patches;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LoadModXmlTimingTests
{
    // The timing used to swap the call to LoadDefs in LoadModXML for a wrapper of its own, so
    // another mod's transpiler on LoadModXML that looks for the call, applied after this one,
    // would not find it. The call now stays, and the defs it returns are read through the
    // timing, which is handed the same mod.
    [Test]
    public static void TheTranspiledLoopStillCallsLoadDefs()
    {
        var method = AccessTools.Method(
            typeof(LoadedModManager),
            nameof(LoadedModManager.LoadModXML)
        );
        var loadDefs = AccessTools.Method(typeof(ModContentPack), nameof(ModContentPack.LoadDefs));
        var readDefsTimed = AccessTools.Method(
            typeof(LoadedModManager_LoadModXML),
            nameof(LoadedModManager_LoadModXML.ReadDefsTimed)
        );
        var original = PatchProcessor.GetOriginalInstructions(method, out var generator);

        var patched = LoadedModManager_LoadModXML.Transpiler(original, generator).ToList();

        var call = patched.FindIndex(instruction => instruction.Calls(loadDefs));
        Expect.GreaterThanOrEqualTo(call, 2);
        var loadMod = patched[call - 2];
        Expect.IsTrue(loadMod.IsLdloc());
        Expect.AreEqual(loadMod.opcode, patched[call + 1].opcode);
        Expect.AreEqual(loadMod.operand, patched[call + 1].operand);
        Expect.IsTrue(patched[call + 2].Calls(readDefsTimed));
    }
}
