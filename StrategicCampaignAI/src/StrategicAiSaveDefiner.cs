using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace StrategicCampaignAI;

/// <summary>
/// Registers the container types the strategic layer persists.
///
/// Bannerlord's save system refuses to serialise a generic container it has not
/// been told about, and it fails at save time rather than load time -- this is
/// the cause of the original "Can't Save" report. Syncing raw dictionaries
/// without a definer cannot work; the earlier workaround of emptying SyncData
/// removed the symptom along with the feature.
///
/// Only dictionaries of primitives are declared. The strategic layer
/// deliberately never persists its own classes (faction status, army progress),
/// both because they are recomputed within an hour anyway and because keeping
/// custom types out of the save format avoids versioning hazards later.
///
/// The base id must not collide with another mod's definer. Do not change it
/// once released: existing saves reference these type ids.
/// </summary>
public sealed class StrategicAiSaveDefiner : SaveableTypeDefiner
{
    public StrategicAiSaveDefiner()
        : base(2_114_400)
    {
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<string, string>));
        ConstructContainerDefinition(typeof(Dictionary<string, int>));
        ConstructContainerDefinition(typeof(Dictionary<string, double>));
    }
}
