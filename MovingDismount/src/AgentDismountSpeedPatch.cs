using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace MovingDismount;

[HarmonyPatch(typeof(Agent), "GetInteractionDistanceToUsable")]
internal static class AgentDismountSpeedPatch
{
    private const float VanillaDismountVelocityLimit = 0.5f;
    private const float MovingDismountVelocityLimit = 100f;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_R4 &&
                instruction.operand is float value &&
                value == VanillaDismountVelocityLimit)
            {
                instruction.operand = MovingDismountVelocityLimit;
            }

            yield return instruction;
        }
    }
}
