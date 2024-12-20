using System;
using System.Collections.Generic;
using System.Linq;
using SaintsField.Editor.Core;
using SaintsField.Editor.Drawers.AdvancedDropdownDrawer;
using UnityEditor;

namespace SaintsField.Editor.Drawers.EnumFlagsDrawers
{
    [CustomPropertyDrawer(typeof(FlagsDropdownAttribute))]
    public partial class FlagsDropdownAttributeDrawer: SaintsPropertyDrawer
    {
        private static string GetSelectedNames(IReadOnlyDictionary<int, EnumFlagsUtil.EnumDisplayInfo> bitValueToName, int selectedInt)
        {
            string[] names = bitValueToName.Where(kv => EnumFlagsUtil.isOn(selectedInt, kv.Key)).Select(kv => kv.Value.HasRichName? kv.Value.RichName.Split('/').Last(): kv.Value.Name).ToArray();
            return names.Length == 0? "-":  string.Join(",", names);
        }

        private static AdvancedDropdownMetaInfo GetMetaInfo(int curMask, IReadOnlyDictionary<int, EnumFlagsUtil.EnumDisplayInfo> bitValueToName)
        {
            AdvancedDropdownList<object> dropdownListValue = new AdvancedDropdownList<object>();
            foreach (KeyValuePair<int, EnumFlagsUtil.EnumDisplayInfo> kv in bitValueToName)
            {
                dropdownListValue.Add(kv.Value.HasRichName? kv.Value.RichName: kv.Value.Name, kv.Key);
            }

            #region Get Cur Value

            IReadOnlyList<object> curValues = bitValueToName.Keys
                .Where(kv => EnumFlagsUtil.isOn(curMask, kv))
                .Cast<object>()
                .ToArray();

            IReadOnlyList<AdvancedDropdownAttributeDrawer.SelectStack> curSelected;
            if (curValues.Count == 0)
            {
                curSelected = Array.Empty<AdvancedDropdownAttributeDrawer.SelectStack>();
            }
            else
            {
                (IReadOnlyList<AdvancedDropdownAttributeDrawer.SelectStack> stacks, string _) = AdvancedDropdownUtil.GetSelected(curValues[curValues.Count - 1], Array.Empty<AdvancedDropdownAttributeDrawer.SelectStack>(), dropdownListValue);
                curSelected = stacks;
            }

            // string curDisplay = "";

            #endregion

            return new AdvancedDropdownMetaInfo
            {
                Error = "",
                // FieldInfo = field,
                // CurDisplay = display,
                CurValues = curValues,
                DropdownListValue = dropdownListValue,
                SelectStacks = curSelected,
            };
        }
    }
}
