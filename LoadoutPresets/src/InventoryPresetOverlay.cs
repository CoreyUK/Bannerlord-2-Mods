using SandBox.GauntletUI;
using System.Reflection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace LoadoutPresets;

internal sealed class InventoryPresetOverlay
{
    private static readonly FieldInfo? DataSourceField = typeof(GauntletInventoryScreen).GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? GauntletLayerField = typeof(GauntletInventoryScreen).GetField("_gauntletLayer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? MovieIdentifiersField = typeof(GauntletLayer).GetField("_movieIdentifiers", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? UpdateCharacterEquipmentMethod = typeof(SPInventoryVM).GetMethod("UpdateCharacterEquipment", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? InitializeInventoryMethod = typeof(SPInventoryVM).GetMethod("InitializeInventory", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? RefreshInformationValuesMethod = typeof(SPInventoryVM).GetMethod("RefreshInformationValues", BindingFlags.Instance | BindingFlags.NonPublic);

    private GauntletInventoryScreen? _screen;
    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private LoadoutPresetsVM? _vm;

    public void Tick()
    {
        ScreenBase? topScreen = ScreenManager.TopScreen;
        if (topScreen is GauntletInventoryScreen inventoryScreen)
        {
            if (_screen != inventoryScreen)
            {
                Detach();
                Attach(inventoryScreen);
            }

            if (_vm != null)
            {
                _vm.IsSuppressed = IsInventoryTooltipActive(inventoryScreen);
                _vm.Refresh();
            }
            return;
        }

        Detach();
    }

    public void Dispose() => Detach();

    private void Attach(GauntletInventoryScreen screen)
    {
        _screen = screen;
        _vm = new LoadoutPresetsVM(LoadoutPresetService.Instance, () => GetEquipmentMode(screen), () => RefreshInventoryVM(screen));
        _layer = GauntletLayerField?.GetValue(screen) as GauntletLayer;
        if (_layer == null)
        {
            return;
        }

        _movie = _layer.LoadMovie("LoadoutPresets", _vm);
        MoveMovieBehindInventory(_layer, _movie);
        _vm.Refresh();
    }

    private void Detach()
    {
        if (_screen != null && _layer != null)
        {
            if (_movie != null)
            {
                _layer.ReleaseMovie(_movie);
            }
        }

        _movie = null;
        _layer = null;
        _vm = null;
        _screen = null;
    }

    private static int GetEquipmentMode(GauntletInventoryScreen screen)
    {
        return DataSourceField?.GetValue(screen) is SPInventoryVM inventoryVM
            ? inventoryVM.EquipmentMode
            : 1;
    }

    private static bool IsInventoryTooltipActive(GauntletInventoryScreen screen)
    {
        if (DataSourceField?.GetValue(screen) is not SPInventoryVM inventoryVM)
        {
            return false;
        }

        if (inventoryVM.CurrentFocusedItem != null)
        {
            return true;
        }

        float mouseX = Input.MousePositionRanged.X;
        float mouseY = Input.MousePositionRanged.Y;
        return mouseX >= 0.31f && mouseX <= 0.46f && mouseY >= 0.64f && mouseY <= 0.86f;
    }

    private static void RefreshInventoryVM(GauntletInventoryScreen screen)
    {
        if (DataSourceField?.GetValue(screen) is not SPInventoryVM inventoryVM)
        {
            return;
        }

        UpdateCharacterEquipmentMethod?.Invoke(inventoryVM, null);
        InitializeInventoryMethod?.Invoke(inventoryVM, null);
        RefreshInformationValuesMethod?.Invoke(inventoryVM, null);
        inventoryVM.RefreshValues();
    }

    private static void MoveMovieBehindInventory(GauntletLayer layer, GauntletMovieIdentifier movie)
    {
        if (MovieIdentifiersField?.GetValue(layer) is not System.Collections.IList movies)
        {
            return;
        }

        int index = movies.IndexOf(movie);
        if (index <= 0)
        {
            return;
        }

        movies.RemoveAt(index);
        movies.Insert(0, movie);
    }
}
