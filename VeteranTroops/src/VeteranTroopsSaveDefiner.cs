using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace VeteranTroops;

/// <summary>
/// Registers the containers the behavior persists. Only dictionaries of
/// primitives are written, keyed by troop StringId, so the save format never
/// references a mod type and the mod can be removed mid-save.
///
/// The base id must not collide with another mod's definer. Do not change it
/// once released: existing saves reference these type ids.
/// </summary>
public sealed class VeteranTroopsSaveDefiner : SaveableTypeDefiner
{
    public VeteranTroopsSaveDefiner()
        : base(2_114_800)
    {
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<string, int>));
    }
}
