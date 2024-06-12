using SaintsField.Playa;
using UnityEngine;

namespace SaintsField.Samples.Scripts.SaintsEditor
{
    public class LayoutGroupExample : MonoBehaviour
    {
        // end group
        public string beforeGroup;

        [LayoutGroup("Group", ELayout.Background | ELayout.TitleOut)]
        public string group1;
        public string group2;  // starts from this will be automatically grouped into "Group"
        public string group3;

        [LayoutEnd("Group")]  // this will end the "Group"
        public string afterGroup;

        [SepTitle(EColor.Gray)]

        // break group
        public string breakBefore;

        [LayoutGroup("break", ELayout.Background | ELayout.TitleOut)]
        public string breakGroup1;
        public string breakGroup2;

        // this group will stop the grouping of "break"
        [LayoutGroup("breakIn", ELayout.Background | ELayout.TitleOut)]
        public string breakIn1;
        public string breakIn2;

        [LayoutGroup("break")]  // this will be grouped into "break", and also end the "breakIn" group
        public string breakGroup3;
        public string breakGroup4;

        [LayoutEnd("break")]  // end, it will not be grouped
        public string breakAfter;
    }
}
