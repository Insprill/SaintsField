using System.Collections.Generic;
using System.Reflection;
using SaintsField.Interfaces;
using UnityEditor;
using UnityEngine;

namespace SaintsField.Editor.AutoRunner
{
    public interface IAutoRunnerFixDrawer
    {
        AutoRunnerFixerResult AutoRunFix(PropertyAttribute propertyAttribute, ISaintsAttribute[] allAttributes,
            SerializedProperty property,
            MemberInfo memberInfo, object parent);
    }
}
